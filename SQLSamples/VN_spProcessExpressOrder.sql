USE [db]
GO
SET ANSI_NULLS OFF
GO
SET QUOTED_IDENTIFIER ON
GO


ALTER PROCEDURE [dbo].[VN_spProcessExpressOrder]	
	@UserId INT,		--order is placed by this user
	@OrderUserId int,	--order is applied to this user
	@PurchaseOrder nvarchar(50),
	@Comments nvarchar(1000),
	@StockThreshold int

AS

declare @RightNow datetime
set @RightNow = GETDATE()

declare @NewOrderId int

declare @NewCartId int

declare @UserName nvarchar(50)

BEGIN TRANSACTION;

BEGIN TRY

--Get user name

set @UserName = 
	(select UserName 
	from dbo.VN_tblUser with (nolock)
	where UserId = @UserId)
	

--Add new cart row here  - all Cart table interaction handled in this proc
    
INSERT INTO [dbo].[VN_tblCart]
([UserId]
,[DateAdded]
,[DateModified]
,[AddedBy]
,[ModifiedBy])
VALUES
(@OrderUserId
,@RightNow
,null
,@UserId
,null)

set @NewCartId = cast(@@IDENTITY as int)


declare @OrderNumber varchar(50)

--set temp value for order number
set @OrderNumber = ltrim(rtrim(str(@NewCartId)))	--'W' + ltrim(rtrim(STR(@NewCartId))) -- + 'E'


--bring express cart item rows into cart items table


INSERT INTO [dbo].[VN_tblCartItem]
([CartId]
,[ProductId]
,[PartNum]
,[MfgPartNum]
,[Quantity]
,[DateAdded]
,[DateModified]
,[AddedBy]
,[ModifiedBy])

SELECT
@NewCartId	
,[ProductId]
,[PartNum]
,[MfgPartNum]
,[Quantity]
,@RightNow
,null
,@UserId
,null
		
from dbo.[VN_tblExpressCartItem] er with (nolock)

where er.ProcessedYN <> 1
and er.UserId = @OrderUserId


--now process the express cart item records so they aren't processed again

update dbo.[VN_tblExpressCartItem]
set ProcessedYN  = 1,
DateModified = @RightNow,
ModifiedBy = @UserId
where UserId = @OrderUserId
and ProcessedYN <> 1




--Add row to Order table

INSERT INTO [dbo].[VN_tblOrder]
	([CartId]
   ,[UserId]
   ,[OrderDate]
   ,[PurchaseOrder]
   ,[OrderNumber]
   ,[PlasticsId]
   ,[Comments]   
   ,Subtotal
   ,Tax
   ,Shipping
   ,Total
   ,[DateAdded]
   ,[DateModified]
   ,[AddedBy]
   ,[ModifiedBy])
VALUES
	(@NewCartId,
	@OrderUserId,
	@RightNow,
	@PurchaseOrder,
	@OrderNumber,	--order number that user will see
	null,
	@Comments,
	0,
	0,
	0,
	0,
	@RightNow,
	null,
	@UserId,
	null)	
	
set @NewOrderId = cast(@@IDENTITY as int)


--Populate the order items

insert into dbo.VN_tblOrderItem
(OrderId,
MfgPartNum,
PartNum,
ProductId,
ProductName,
Quantity,
Price,
StatusText,
DateAdded,
DateModified,
AddedBy,
ModifiedBy)

SELECT
@NewOrderId,	
c.MfgPartNum, 	
c.PartNum,     	
c.ProductId,     
c.ProductName, 
ci.Quantity,
cp.Price,
dbo.[VN_fnGetProductStatusText](u.Warehouse, c.Stock, c.StockNL, @StockThreshold),
@RightNow,
null,
@UserId, 			
null
		
from dbo.VN_tblCart cr with (nolock)
	
inner join dbo.VN_tblUser u with (nolock)
	on cr.UserId = u.UserId	
		
inner join dbo.VN_tblCartItem ci with (nolock)
	on cr.CartId = ci.CartId	
		
inner join dbo.VN_tblCatalog  c with (nolock)
	on (ci.ProductId = c.ProductId and c.CurrentYN = 1)
		
inner join dbo.VN_tblAssortmentProduct ap with (nolock)	--added
	on (ap.ProductId = c.ProductId 
		and ap.AssortmentId = u.AssortmentId)
		
inner join dbo.VN_tblCatalogPricing cp  with (nolock)
	ON (cp.ProductId = ap.ProductId and cp.PriceLevelId = u.PriceLevelId)
	
where cr.CartId = @NewCartId


--update order with totals, etc.

declare @FreightPct tinyint

set @FreightPct = isnull(
	(select FreightPct
	from dbo.VN_tblUser
	where UserId = @UserId), 0)
	
declare @Subtotal decimal(20,5)
declare @Tax decimal(20,5)
declare @Shipping decimal(20,5)
declare @Total decimal(20,5)


set @Subtotal = dbo.[VN_fnGetOrderSubtotalByOrderId](@NewOrderId)
set @Tax = 0	--for now
set @Shipping = @Subtotal * cast(@FreightPct as decimal(20,5)) / 100.0
set @Total = @Subtotal + @Tax + @Shipping 

update dbo.VN_tblOrder
set Subtotal = @Subtotal,
Tax = @Tax, 
Shipping =  @Shipping, 
Total = @Total 
where OrderId = @NewOrderId


--Populate order addresses

INSERT INTO [dbo].[VN_tblOrderAddress]
	([OrderId]
    ,[AddressType]
	,[CompanyName]
    ,[AddressLine1]
    ,[AddressLine2]
    ,[City]
    ,[State]
    ,[Country]
    ,[PostalCode]
    ,[DateAdded]
    ,[DateModified]
    ,[AddedBy]
    ,[ModifiedBy])
select top 1
@NewOrderId,
AddressType,
[CompanyName],
[AddressLine1],
[AddressLine2],
[City],
[State],
[Country],
[PostalCode],
@RightNow,
null,
@UserId,
null
from dbo.VN_tblUserAddress
where UserId = @OrderUserId
and AddressType = 'B'
and IsPrimaryYN = 1
and IsDeletedYN <> 1


INSERT INTO [dbo].[VN_tblOrderAddress]
	([OrderId]
    ,[AddressType]
	,[CompanyName]
    ,[AddressLine1]
    ,[AddressLine2]
    ,[City]
    ,[State]
    ,[Country]
    ,[PostalCode]
    ,[DateAdded]
    ,[DateModified]
    ,[AddedBy]
    ,[ModifiedBy])
select top 1
@NewOrderId,
AddressType,
[CompanyName],
[AddressLine1],
[AddressLine2],
[City],
[State],
[Country],
[PostalCode],
@RightNow,
null,
@UserId,
null

from dbo.VN_tblUserAddress
where UserId = @OrderUserId
and AddressType = 'S'
and IsPrimaryYN = 1
and IsDeletedYN <> 1


--finally set the order number
update dbo.VN_tblOrder
set OrderNumber = 'W' + ltrim(rtrim(STR(@NewOrderId)))
where OrderId = @NewOrderId


IF @@TRANCOUNT > 0
    COMMIT TRANSACTION;

select
OrderNumber = @OrderNumber,	
OrderId = @NewOrderId,
NewCartId = @NewCartId,		
UserName = @UserName


END TRY
BEGIN CATCH

	print ERROR_NUMBER()
	print ERROR_MESSAGE() 
	
	set @NewCartId = 0
	set @OrderNumber = 0
	
	IF @@TRANCOUNT > 0
        ROLLBACK TRANSACTION;
	
	select
	OrderNumber = @OrderNumber,	
	OrderId = @NewOrderId,
	NewCartId = @NewCartId,		
	UserName = @UserName
	
    
        
END CATCH;





