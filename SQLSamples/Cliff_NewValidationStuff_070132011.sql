USE [db]
GO
SET ANSI_NULLS ON
GO
SET QUOTED_IDENTIFIER ON
GO
-- =============================================
-- Author:		Cliff Spielman
-- Create date: 
-- Description:	
-- =============================================
CREATE FUNCTION [dbo].[fnIsValidPostalCode] 
(	
	@PostalCode varchar(50)
	
)
RETURNS varchar(50)
AS
BEGIN
	DECLARE @Result varchar(50)
	DECLARE @PostalCodeUse varchar(50)

	set @Result = ''
	--set @PhoneNumberUse = ltrim(rtrim(isnull(@PhoneNumber,'')))
	set @PostalCodeUse = isnull(@PostalCode,'')

	if @PostalCodeUse = ''
	begin
		set @Result = 'Postal code is blank'	
	end	
	else if len(@PostalCodeUse) < 5 
	begin
		set @Result = 'Postal code length is less than 5'
	end
	--check for patterns, add others as needed.
	--not pretty code, but I opted not to do a .NET CLR stored proc,
	--and to keep the code simpler, though not elegant.  At least for now.
	else if patindex('%12345%', @PostalCodeUse) > 0		
		or patindex('%54321%', @PostalCodeUse) > 0		
		
	begin
		set @Result = 'Postal code contains suspicious pattern'
	end	
	else
	begin

		--check for non digits
		declare @CharCounter integer
		declare @CurrentChar char

		set @CharCounter = 1

		while(@CharCounter <= len(@PostalCodeUse))
		begin
			set @CurrentChar = substring(@PostalCodeUse, @CharCounter, 1) 

			if @CurrentChar < '0' or @CurrentChar > '9'		--check if digit
			begin
				set @Result = 'Postal code contains non-digits'
			end 

			set @CharCounter = @CharCounter + 1

		end

	end 
		
	
	RETURN @Result

END
GO
SET ANSI_NULLS ON
GO
SET QUOTED_IDENTIFIER ON
GO
-- =============================================
-- Author:		Cliff Spielman
-- Create date: 
-- Description:	
-- =============================================
CREATE FUNCTION [dbo].[fnIsValidPhoneNumber] 
(	
	@PhoneNumber varchar(50),
	@EmptyOkYN bit
)
RETURNS varchar(50)
AS
BEGIN
	DECLARE @Result varchar(50)
	DECLARE @PhoneNumberUse varchar(50)

	set @Result = ''	
	set @PhoneNumberUse = isnull(@PhoneNumber,'')

	--check for empty
	if @PhoneNumberUse = ''
	begin
		if @EmptyOkYN <> 1
		begin
			set @Result = 'Phone number is blank'
		end
	end
	else if len(@PhoneNumberUse) < 7 
	begin
		set @Result = 'Phone number length is less than 7'
	end
	--check for patterns, add others as needed.
	--not pretty code, but I opted not to do a .NET CLR stored proc,
	--and to keep the code simpler, though not elegant.  At least for now.
	else if patindex('%12345%', @PhoneNumberUse) > 0
		or patindex('%23456%', @PhoneNumberUse) > 0
		or patindex('%34567%', @PhoneNumberUse) > 0
		or patindex('%45678%', @PhoneNumberUse) > 0
		or patindex('%567890%', @PhoneNumberUse) > 0
		or patindex('%54321%', @PhoneNumberUse) > 0		
		
	begin
		set @Result = 'Phone number contains suspicious pattern'
	end	
	else
	begin

		--check for non digits
		declare @CharCounter integer
		declare @CurrentChar char

		set @CharCounter = 1

		while(@CharCounter <= len(@PhoneNumberUse))
		begin
			set @CurrentChar = substring(@PhoneNumberUse, @CharCounter, 1) 

			if @CurrentChar < '0' or @CurrentChar > '9'		--check if digit
			begin
				set @Result = 'Phone number contains non-digits'
			end 

			set @CharCounter = @CharCounter + 1

		end

	end 
		
	
	RETURN @Result

END
GO

SET ANSI_NULLS ON
GO
SET QUOTED_IDENTIFIER ON
GO
-- =============================================
-- Author:		Cliff Spielman
-- Create date: 
-- Description:	
-- =============================================
CREATE FUNCTION [dbo].[fnIsValidEmailAddress] 
(	
	@EmailAddress varchar(150)
	
)
RETURNS varchar(50)
AS
BEGIN
	DECLARE @Result varchar(50)
	DECLARE @EmailAddressUse varchar(50)

	set @Result = ''
	--set @PhoneNumberUse = ltrim(rtrim(isnull(@PhoneNumber,'')))
	set @EmailAddressUse = isnull(@EmailAddress,'')

	--check for empty
	if @EmailAddressUse = ''
	begin		
		set @Result = 'Email address is blank'		
	end
	else if charindex('@', @EmailAddressUse) < 1
	begin
		set @Result = 'Email address is missing @ or . char'
	end
	else if charindex('.', @EmailAddressUse) < 1
	begin
		set @Result = 'Email address is missing @ or . char'
	end		
		
	
	RETURN @Result

END
GO

SET ANSI_NULLS ON
GO
SET QUOTED_IDENTIFIER ON
GO
-- //// Insert Stored procedure.
---------------------------------------------------------------------------------
-- Stored procedure that will insert 1 row in the table 'tmc_Leads'
-- Gets: @FirstName varchar(50)
-- Gets: @LastName varchar(50)
-- Gets: @HomePhone varchar(50)
-- Gets: @OfficePhone varchar(50)
-- Gets: @Email varchar(50)
-- Gets: @MovingFromStateID int
-- Gets: @MovingFromCity varchar(100)
-- Gets: @MovingToStateID int
-- Gets: @MovingToCity varchar(100)
-- Gets: @MovingDate varchar(50)
-- Gets: @EstimateMoveWeightID int
-- Gets: @Comments text
-- Gets: @DateArrived datetime
-- Gets: @IPAddress varchar(50)
-- Returns: @LeadID int
-- Returns: @ErrorCode int
---------------------------------------------------------------------------------
CREATE  PROCEDURE [dbo].[pr_tmc_Leads_Insert_1]
	@FirstName varchar(50),
	@LastName varchar(50),
	@HomePhone varchar(50),
	@OfficePhone varchar(50),
	@Email varchar(50),
	@MovingFromStateID int,
	@MovingFromCity varchar(100),
	@MovingToStateID int,
	@MovingToCity varchar(100),
	@MovingDate varchar(50),
	@EstimateMoveWeightID int,
	@Comments text,
	@DateArrived datetime,
	@IPAddress varchar(50),
	@FromZipCode varchar(10),
	@ToZipCode varchar(10),
	@LeadID int OUTPUT,
	@ErrorCode int OUTPUT
AS
SET NOCOUNT ON

declare @pendingflagcharlimit int
declare @pendingcharrepeatcount int
SELECT 
	@pendingflagcharlimit = PendingFlagCharLimit, 
	@pendingcharrepeatcount = PendingCharRepeatCount 
FROM tmc_SiteSettings

declare @status varchar(50)
declare @pendingreason varchar(50)
SET @pendingreason = ''
SET @status = 'Approved'

declare @HomePhoneResult varchar(50)
set  @HomePhoneResult = dbo.[fnIsValidPhoneNumber](@HomePhone,0)	--0 means blank not ok
declare @OfficePhoneResult varchar(50)
set  @OfficePhoneResult = dbo.[fnIsValidPhoneNumber](@OfficePhone,1)
declare @ToPostalCodeResult varchar(50)
set  @ToPostalCodeResult = dbo.[fnIsValidPostalCode](@ToZipCode)
declare @FromPostalCodeResult varchar(50)
set  @FromPostalCodeResult = dbo.[fnIsValidPostalCode](@FromZipCode)
declare @EmailResult varchar(50)
set  @EmailResult = dbo.[fnIsValidEmailAddress](@Email)


if @HomePhoneResult <> ''
begin
set @pendingreason = @HomePhoneResult
end
else if @OfficePhoneResult <> ''
begin
set @pendingreason = @OfficePhoneResult
end
else if @FromPostalCodeResult <> ''
begin
set @pendingreason = @FromPostalCodeResult
end
else if @ToPostalCodeResult <> ''
begin
set @pendingreason = @ToPostalCodeResult
end
else if @EmailResult <> ''
begin
set @pendingreason = @EmailResult
end

--continue with existing code
else
IF (LEN(convert(varchar(1000), @Comments)) > @pendingflagcharlimit)  
BEGIN SET @pendingreason = 'Exceeds character limit' END
else IF ((LEN(@FirstName) < 2) OR (LEN(@LastName) < 2) OR (LEN(@HomePhone) < 2) OR (LEN(@Email) < 2)) 
BEGIN SET @pendingreason = 'Below min characters' END
else IF (CHARINDEX('http://', @Comments) > 0) OR (CHARINDEX('https://', @Comments) > 0) 
BEGIN SET @pendingreason = 'Contains URL' END
else IF (topmovingcompany.mfn_HasMaxRepeats(@comments, @pendingcharrepeatcount) > 0) 
BEGIN SET @pendingreason = 'Comment contains repeat characters.' END
else IF (topmovingcompany.mfn_HasMaxRepeats(@HomePhone, @pendingcharrepeatcount) > 0) 
BEGIN SET @pendingreason = 'Home phone contains repeat characters.' END
else IF (topmovingcompany.mfn_HasMaxRepeats(@OfficePhone, @pendingcharrepeatcount) > 0)
BEGIN SET @pendingreason = 'Office phone contains repeat characters.' END
else IF (topmovingcompany.mfn_HasMaxRepeats(@Email, @pendingcharrepeatcount) > 0)
BEGIN SET @pendingreason = 'Email contains repeat characters.' END




IF (@pendingreason <> '') BEGIN SET @status = 'Pending' END

declare @repeatlead int
IF (@pendingreason = '')
BEGIN
	SELECT @repeatlead = count(*) FROM tmc_Leads 
	WHERE (HomePhone = @HomePhone) 
		AND (MovingFromCity = @MovingFromCity) 
		AND DateArrived > getdate() - 2
	IF (@repeatlead > 0) BEGIN SET @status = 'Pending' SET @pendingreason = 'Duplicate from past 48 hours: home phone' END

	SELECT @repeatlead = count(*) FROM tmc_Leads 
	WHERE ((FirstName = @FirstName) and (LastName = @LastName)) 
		AND (MovingFromCity = @MovingFromCity) 
		AND DateArrived > getdate() - 2
	IF (@repeatlead > 0) BEGIN SET @status = 'Pending' SET @pendingreason = 'Duplicate from past 48 hours: full name' END

	SELECT @repeatlead = count(*) FROM tmc_Leads 
	WHERE (Email = @Email) 
		AND (MovingFromCity = @MovingFromCity) 
		AND DateArrived > getdate() - 2
	IF (@repeatlead > 0) BEGIN SET @status = 'Pending' SET @pendingreason = 'Duplicate from past 48 hours: email' END
END

IF (@pendingreason = '')
BEGIN
	if (((rtrim(@FirstName) = '') AND (rtrim(@LastName) = '')) or 
		((rtrim(@Email) = '') AND (rtrim(@HomePhone) = '') AND (rtrim(@OfficePhone) = '')))
	BEGIN
		SET @status	= 'Invalid'
	END
END

if (@status <> 'Invalid')
BEGIN
INSERT [dbo].[tmc_Leads]
(
	[FirstName],
	[LastName],
	[HomePhone],
	[OfficePhone],
	[Email],
	[MovingFromStateID],
	[MovingFromCity],
	[MovingToStateID],
	[MovingToCity],
	[MovingDate],
	[EstimateMoveWeightID],
	[Comments],
	[DateArrived],
	[IPAddress],
	[MovingFromZip],
	[MovingToZip],
	Status,
	PendingReason
)
VALUES
(
	@FirstName,
	@LastName,
	@HomePhone,
	@OfficePhone,
	@Email,
	@MovingFromStateID,
	@MovingFromCity,
	@MovingToStateID,
	@MovingToCity,
	@MovingDate,
	@EstimateMoveWeightID,
	@Comments,
	@DateArrived,
	@IPAddress,
	@FromZipCode,
	@ToZipCode,
	@status,
	@pendingreason
)

-- Get the Error Code for the statement just executed.
SELECT @ErrorCode=@@ERROR
-- Get the IDENTITY value for the row just inserted.
SELECT @LeadID=SCOPE_IDENTITY()
END
ELSE
BEGIN
-- Get the Error Code for the statement just executed.
SELECT @ErrorCode=@@ERROR
-- Get the IDENTITY value for the row just inserted.
SELECT @LeadID=0
END
GO
