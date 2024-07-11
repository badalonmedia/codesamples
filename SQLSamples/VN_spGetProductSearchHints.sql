USE [db]
GO
SET ANSI_NULLS ON
GO
SET QUOTED_IDENTIFIER ON
GO

ALTER procEDURE [dbo].[VN_spGetProductSearchHints]
	@UserId int,
	@SearchText varchar(100),
	@HowMany int
	--@SortBy1 varchar(50) = null

AS

SET NOCOUNT ON;

declare @AssortmentId int

declare @HowManyPer_1_1 int
declare @HowManyPer_1_2 int
--declare @HowManyPer_1_3 int
declare @HowManyPer_2_1 int
declare @HowManyPer_2_2 int
declare @HowManyPer_3_1 int
declare @HowManyPer_3_2 int

set @HowManyPer_1_1 = 6
set @HowManyPer_1_2 = 6
--set @HowManyPer_1_3 = 5
set @HowManyPer_2_1 = 5
set @HowManyPer_2_2 = 5
set @HowManyPer_3_1 = 5
set @HowManyPer_3_2 = 3



set @AssortmentId = 
	(select AssortmentId
	from dbo.VN_tblUser with (nolock)
	where UserId = @UserId)


	--MFG PART NUM	
	
	SELECT top (@HowManyPer_2_1)
  
	SearchHint = c.MfgPartNum,
	HintType = 2,		--mfgpartnum
	HintRanking = 1		--starts with
	
	FROM dbo.VN_tblCatalog  c with (nolock)
		
	inner join dbo.VN_tblAssortmentProduct ap with (nolock)
		on (ap.ProductId = c.ProductId 
			and ap.AssortmentId = @AssortmentId)				
	
	where c.CurrentYN = 1
	
	and c.MfgPartNum LIKE (@SearchText + '%')
	
	union --distinct	
	
	SELECT top (@HowManyPer_2_2)
  
	SearchHint = c.MfgPartNum,
	HintType = 2,		--mfgpartnum
	HintRanking = 2		--contains
	
	FROM dbo.VN_tblCatalog  c with (nolock)
		
	inner join dbo.VN_tblAssortmentProduct ap with (nolock)
		on (ap.ProductId = c.ProductId 
			and ap.AssortmentId = @AssortmentId)					
	
	where c.CurrentYN = 1
	
	and c.MfgPartNum not LIKE (@SearchText + '%')
	and c.MfgPartNum LIKE ('%' + @SearchText + '%')
	
	union 
	
	
	--MFG NAME
	
	SELECT top (@HowManyPer_3_1)
  
	SearchHint = m.MfgName,
	HintType = 3,	--mfgname
	HintRanking = 1		--starts with
	
	FROM dbo.VN_tblCatalog  c with (nolock)
		
	inner join dbo.VN_tblAssortmentProduct ap with (nolock)
		on (ap.ProductId = c.ProductId 
			and ap.AssortmentId = @AssortmentId)		
		
	INNER JOIN dbo.VN_tblMfg m with (nolock)		--needed for searching on mfg info
		ON m.MfgId = c.MfgId 					
	
	where c.CurrentYN = 1
	
	and m.MfgName LIKE (@SearchText + '%')
	
	union --distinct	
	
	SELECT top (@HowManyPer_3_2)
  
	SearchHint = m.MfgName,
	HintType = 3,	--mfgname
	HintRanking = 2		--contains
	
	FROM dbo.VN_tblCatalog  c with (nolock)
		
	inner join dbo.VN_tblAssortmentProduct ap with (nolock)
		on (ap.ProductId = c.ProductId 
			and ap.AssortmentId = @AssortmentId)		
		
	INNER JOIN dbo.VN_tblMfg m with (nolock)		--needed for searching on mfg info
		ON m.MfgId = c.MfgId 					
	
	where c.CurrentYN = 1
	
	and m.MfgName not LIKE (@SearchText + '%')
	and m.MfgName LIKE ('%' + @SearchText + '%')
	
	
	union 
	
	
	--PRODUCT NAME
	
	SELECT top (@HowManyPer_1_1)
  
	SearchHint = c.ProductName,
	HintType = 1,	--product name
	HintRanking = 1		--starts with
	
	FROM dbo.VN_tblCatalog  c with (nolock)
		
	inner join dbo.VN_tblAssortmentProduct ap with (nolock)
		on (ap.ProductId = c.ProductId 
			and ap.AssortmentId = @AssortmentId)				
		
	where c.CurrentYN = 1
	
	and Replace(isnull(c.ProductName,''), '  ', ' ') LIKE (@SearchText + '%')
	
	union 
	
	SELECT top (@HowManyPer_1_2)
  
	SearchHint = c.ProductName,
	HintType = 1,	--product name
	HintRanking = 2		--contains
	
	FROM dbo.VN_tblCatalog  c with (nolock)
		
	inner join dbo.VN_tblAssortmentProduct ap with (nolock)
		on (ap.ProductId = c.ProductId 
			and ap.AssortmentId = @AssortmentId)				
		
	where c.CurrentYN = 1
	
	and Replace(isnull(c.ProductName,''), '  ', ' ') not  like (@SearchText + '%')
	and Replace(isnull(c.ProductName,''), '  ', ' ') LIKE ('%' + @SearchText + '%')		
		
		
	order by HintType, HintRanking, SearchHint
	
	

