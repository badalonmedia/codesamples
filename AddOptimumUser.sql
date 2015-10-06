
CREATE PROCEDURE [dbo].[AddOptimumUser]
	@IXML NVARCHAR(MAX) 	
AS
BEGIN
	
	DECLARE @idoc int
EXEC sp_xml_preparedocument @idoc OUTPUT, @IXML

-----------------------------------------------------------------
SELECT 	
	[Login],
	LastName,
	FirstName,
	StatusId,
	FacilityGroupName,	
	FacilityGroupId,
	Templates,
	TitlePositionId,
	UserFacilities,
	Comments,
	AddedBy
	
INTO #t
FROM OPENXML (@idoc, '/*[1]',2)
WITH (	
	[Login] nvarchar(100) 'Login',
	LastName  nvarchar(200) 'LastName',
	FirstName  nvarchar(200) 'FirstName',
	StatusId int 'StatusId',
	FacilityGroupName varchar(100) 'FacilityGroupName',
	FacilityGroupId int 'FacilityGroupId',
	Templates varchar(500) 'Templates',
	TitlePositionId int 'TitlePositionId',	
	UserFacilities varchar(500) 'UserFacilities',
	Comments nvarchar(3000),
	AddedBy nvarchar(128) 'AddedBy'
)

EXEC sp_xml_removedocument @idoc
-----------------------------------------------------------------

DECLARE @Login nvarchar(100)
DECLARE @LastName nvarchar(200)
DECLARE @FirstName nvarchar(200)
DECLARE @StatusId int
declare @TitlePositionId int
DECLARE @Templates varchar(500)
DECLARE @AddedBy nvarchar(128)
DECLARE @FacilityGroupName varchar(100)
declare @FacilityGroupId int
DECLARE @UserFacilities varchar(500)
declare @Comments nvarchar(3000)

SET NOCOUNT ON

SET @Login = (SELECT TOP 1 [Login] FROM #t)
SET @LastName = (SELECT TOP 1 LastName FROM #t)
SET @FirstName = (SELECT TOP 1 FirstName FROM #t)
SET @StatusId = (SELECT TOP 1 StatusId FROM #t)
SET @Templates = (SELECT TOP 1 Templates FROM #t)
SET @AddedBy = (SELECT TOP 1 AddedBy FROM #t)
SET @FacilityGroupName = (SELECT TOP 1 FacilityGroupName FROM #t)
set @FacilityGroupId = (SELECT TOP 1 FacilityGroupId FROM #t)
SET @TitlePositionId = (SELECT TOP 1 TitlePositionId FROM #t)
SET @UserFacilities = (SELECT TOP 1 UserFacilities FROM #t)
SET @Comments = (SELECT TOP 1 Comments FROM #t)

declare @RightNow datetime
set @RightNow = getdate()

declare @addresult int
set @addresult = 1

begin transaction;

begin try

--USER

--because this is adding a user to a facility group, the user might already exist for another facility group,
--and hence in the master user table, so need to check for that.

declare @UserId int
set @UserId =
	(select UserId from dbo.OptUser with (nolock) where [Login] = @Login)

if @UserId is null
begin
	--add the user
	INSERT INTO [dbo].[OptUser]
           ([Login]
           ,[LastName]
           ,[FirstName]
		   ,[AddedByFromXLS]           
           ,[DateAdded]
           ,[AddedBy]
           ,[DateModified]
           ,[ModifiedBy])
     VALUES
           (@Login,
           @LastName,
           @FirstName,
		   null,		   
		   @RightNow,
		   @AddedBy,
		   null,
		   null)

	set @UserId = SCOPE_IDENTITY()
end
else
begin

--update existing user (user is only new in terms of not being in the facility group)

	update [dbo].[OptUser]
    set [Login] = @Login,
		LastName = @LastName,
        FirstName = @FirstName,		       		
		DateModified = @RightNow,
		ModifiedBy = @AddedBy
	where UserId = @UserId

end

--Proceed with remaining table updates
	
	
	--FACILITY GROUP

	declare @UserFacilityGroupId int

	INSERT INTO [dbo].[OptUserFacilityGroup]
		(UserId,
		FacilityGroupId,
		TitlePositionId,
		Comments)
	VALUES
		(@UserId,
		@FacilityGroupId,
		@TitlePositionId,
		@Comments)		

	set @UserFacilityGroupId = SCOPE_IDENTITY()	
		

	--USER FACILITY GROUP STATUS TABLE		
	
	INSERT INTO [dbo].[OptUserFacilityGroupStatus]
		(UserFacilityGroupId,
		StatusId,
		DateChanged,
		ChangedBy)
	VALUES
		(@UserFacilityGroupId,
		@StatusId,
		@RightNow,
		@AddedBy)
	

	--TEMPLATES		

	--Build scratch table of template numbers in the @TemplateNumbers param, e.g. 12 or 12,13

	DECLARE @templatenums as TABLE
	(   
		TemplateNumber varchar(25)
	);
 
	INSERT INTO @templatenums (TemplateNumber) SELECT * FROM dbo.fnSplitString(@Templates, ',');

	INSERT INTO [dbo].[OptUserFacilityGroupTemplate]
		(UserFacilityGroupId,
		TemplateNumber)
	select @UserFacilityGroupId, tt.TemplateNumber
	from @templatenums tt

	
	--FACILITIES
	
	--Build scratch table of facilities in the @Facilities param, e.g. 851 or 851 and 951

	DECLARE @facnums as TABLE
	(   
		FacilityNumber varchar(25)
	);
 
	INSERT INTO @facnums (FacilityNumber) SELECT * FROM dbo.fnSplitString(@UserFacilities, ',');

	INSERT INTO [dbo].OptUserFacilityGroupFacility
		(UserFacilityGroupId,
		FacilityId)
	select @UserFacilityGroupId, f.FacilityId
	from @facnums ff
	inner join dbo.OptFacility f with (nolock) 
		on f.FacilityNumber = ff.FacilityNumber


	--TITLE / POSITION

	update dbo.OptUserFacilityGroup
	set TitlePositionId = @TitlePositionId
	where UserFacilityGroupId = @UserFacilityGroupId

	--return the user corresponding to the login

	set @addresult = @UserId

	select *
	from dbo.OptUser with (nolock)
	where UserId = @UserId

end try

begin catch
IF @@TRANCOUNT > 0
	ROLLBACK TRANSACTION;

return -1

end catch

commit transaction;

return @addresult
    
END
