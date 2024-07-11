-- =============================================
-- Author:		Clifford Spielman
-- Create date: 8/2021
-- Description:	Check the user's current request against the number of results permitted according to their quota.
-- =============================================
CREATE PROCEDURE [dbo].[uspPerformUserQuotaCheck]
	@ApiUserId int,
	@ResourceTypeCode char(4),
	@EndPoint varchar(500),
	@PageSizeActual int,	--num records we want to see if can be loaded
	@UpdateQuota bit = 0,	--flag to indicate whether quota should be updated
	@MaxResultsPerPeriod int output,	
	@ResultsRemainingInPeriod int output,
	@CurrentPeriodStartDate date output,
	@CurrentPeriodEndDate date output,
	@QuotaUpdateResult int output
	
AS
BEGIN	
	SET NOCOUNT ON;	
	
	declare @Now datetime;
	set @Now = getdate();

	declare @ResultsUsedSoFar int;
	
	set @MaxResultsPerPeriod = isnull((select uq.MaxResults
	from dbo.UserQuota uq
	inner join dbo.ResourceType rt
		on uq.ResourceTypeId = rt.ResourceTypeId
	where uq.UserId = @ApiUserId
	and rt.ResourceTypeCode = @ResourceTypeCode), 0);

	print 'MaxRecordsPeriod: ' + str(@MaxResultsPerPeriod);
	
    set @ResultsUsedSoFar = isnull((select sum(ul.NumResults)
	from dbo.UserRequestLog ul
	inner join dbo.ResourceType rt
		on ul.ResourceTypeId = rt.ResourceTypeId
	where UserId = @ApiUserId
	and rt.ResourceTypeCode = @ResourceTypeCode
	and month(@Now) = month(ul.RequestDate)				--These  criteria for "period" may change
	and year(@Now) = year (ul.RequestDate)), 0);	

	set @ResultsRemainingInPeriod = (
		case	--@MaxAllowedRecords of 0 means no quota to enforce		
			when @MaxResultsPerPeriod > 0 then 
				case 
					when @PageSizeActual > 0 then iif(@MaxResultsPerPeriod - @ResultsUsedSoFar - @PageSizeActual >= 0, @MaxResultsPerPeriod - @ResultsUsedSoFar - @PageSizeActual, 0)
					else iif(@MaxResultsPerPeriod - @ResultsUsedSoFar > 0, @MaxResultsPerPeriod - @ResultsUsedSoFar, 0)
				end
			else 1
		end
	);

	--TODO: Generalize this - will also need it to work with other billing period configs, e.g. 3 month periods
	--Currently this works just for a calendar month billing period
	set @CurrentPeriodStartDate = DATEFROMPARTS(year(@Now), month(@Now), 1); 
	set @CurrentPeriodEndDate = EOMONTH(@Now);

	if @UpdateQuota = 1
	begin		
		exec @QuotaUpdateResult = dbo.uspAddUserRequestLog
			@ApiUserId = @ApiUserId, 
			@ResourceTypeCode = 'CASE',		
			@EndPoint = @EndPoint,
			@PageSizeActual = @PageSizeActual;			
	end

	--select "TotalResults" = @RecordsSoFar, "MaxAllowedRecords" = @MaxAllowedRecords, "QuotaRecordsRemaining" = @QuotaRecordsRemaining;
	   	
END