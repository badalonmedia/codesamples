﻿USE [db]
GO
set ANSI_NULLS ON
GO
set QUOTED_IDENTIFIER ON
GO
-- =============================================
-- Author:		Cliff Spielman
-- Create date: 
-- Description:	Adds new optimum user to the db
-- =============================================
ALTER PROCEDURE [dbo].[AddOptimumUser]
	@IXML NVARCHAR(MAX) 	
AS
BEGIN
	
declare @idoc int;

EXEC sp_xml_preparedocument @idoc OUTPUT, @IXML;

-----------------------------------------------------------------
select 	
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
into #t
from OPENXML (@idoc, '/*[1]',2)
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
);

EXEC sp_xml_removedocument @idoc;
-----------------------------------------------------------------

declare @Login nvarchar(100);
declare @LastName nvarchar(200);
declare @FirstName nvarchar(200);
declare @StatusId int;
declare @TitlePositionId int;
declare @Templates varchar(500);
declare @AddedBy nvarchar(128);
declare @FacilityGroupName varchar(100);
declare @FacilityGroupId int;
declare @UserFacilities varchar(500);
declare @Comments nvarchar(3000);

set NOCOUNT ON;

set @Login = (select TOP 1 [Login] from #t);
set @LastName = (select TOP 1 LastName from #t);
set @FirstName = (select TOP 1 FirstName from #t);
set @StatusId = (select TOP 1 StatusId from #t);
set @Templates = (select TOP 1 Templates from #t);
set @AddedBy = (select TOP 1 AddedBy from #t);
set @FacilityGroupName = (select TOP 1 FacilityGroupName from #t);
set @FacilityGroupId = (select TOP 1 FacilityGroupId from #t);
set @TitlePositionId = (select TOP 1 TitlePositionId from #t);
set @UserFacilities = (select TOP 1 UserFacilities from #t);
set @Comments = (select TOP 1 Comments from #t);

declare @RightNow datetime;
set @RightNow = getdate();

declare @addresult int;
set @addresult = 1;

begin transaction

begin try

--USER

--because this is adding a user to a facility group, the user might already exist for another facility group,
--and hence in the master user table, so need to check for that.

declare @UserId int;
set @UserId = (select UserId from dbo.OptUser with (nolock) where [Login] = @Login);

if @UserId is null
begin
	--add the user
	insert into [dbo].[OptUser]
           ([Login]
           ,[LastName]
           ,[FirstName]
	   ,[AddedByFromXLS]           
           ,[DateAdded]
           ,[AddedBy]
           ,[DateModified]
           ,[ModifiedBy])
     values
           (@Login,
           @LastName,
           @FirstName,
	   null,		   
	   @RightNow,
	   @AddedBy,
	   null,
	   null);

	set @UserId = SCOPE_IDENTITY();
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
	where UserId = @UserId;

end

--Proceed with remaining table updates
	
	
--FACILITY GROUP

declare @UserFacilityGroupId int;

insert into [dbo].[OptUserFacilityGroup]
	(UserId,
	FacilityGroupId,
	TitlePositionId,
	Comments)
values
	(@UserId,
	@FacilityGroupId,
	@TitlePositionId,
	@Comments);		

set @UserFacilityGroupId = SCOPE_IDENTITY();
		
--USER FACILITY GROUP STATUS TABLE		
	
insert into [dbo].[OptUserFacilityGroupStatus]
	(UserFacilityGroupId,
	StatusId,
	DateChanged,
	ChangedBy)
values
	(@UserFacilityGroupId,
	@StatusId,
	@RightNow,
	@AddedBy);
	
--TEMPLATES		
--Build scratch table of template numbers in the @TemplateNumbers param, e.g. 12 or 12,13

declare @templatenums as TABLE
(   
	TemplateNumber varchar(25)
);
 
insert into @templatenums (TemplateNumber) select * from dbo.fnSplitString(@Templates, ',');

insert into [dbo].[OptUserFacilityGroupTemplate]
	(UserFacilityGroupId,
	TemplateNumber)
select @UserFacilityGroupId, tt.TemplateNumber
from @templatenums tt;

	
--FACILITIES
	
--Build scratch table of facilities in the @Facilities param, e.g. 851 or 851 and 951

declare @facnums as TABLE
(   
	FacilityNumber varchar(25)
);
 
insert into @facnums (FacilityNumber) select * from dbo.fnSplitString(@UserFacilities, ',');

insert into [dbo].OptUserFacilityGroupFacility
	(UserFacilityGroupId,
	FacilityId)
select @UserFacilityGroupId, f.FacilityId
from @facnums ff
inner join dbo.OptFacility f with (nolock) 
	on f.FacilityNumber = ff.FacilityNumber;


--TITLE / POSITION

update dbo.OptUserFacilityGroup
set TitlePositionId = @TitlePositionId
where UserFacilityGroupId = @UserFacilityGroupId;

--return the user corresponding to the login

set @addresult = @UserId;

select *
from dbo.OptUser with (nolock)
where UserId = @UserId;

end try

begin catch
	
IF @@TRANCOUNT > 0
	ROLLBACK TRANSACTION;

return -1;

end catch;

commit transaction;

return @addresult;
    
END
