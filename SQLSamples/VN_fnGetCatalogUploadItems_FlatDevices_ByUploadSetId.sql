USE [db]
GO
SET ANSI_NULLS ON
GO
SET QUOTED_IDENTIFIER ON
GO

ALTER FUNCTION [dbo].[VN_fnGetCatalogUploadItems_FlatDevices_ByUploadSetId]
(	
	@CatalogUploadSetId int
	
)  
RETURNS @FlatDevices table 
(
	--Could also store category here if needed
	Device varchar(50),
	Model varchar(50),
	PartNum char(6)	
) 
AS  
BEGIN 	
		
	declare @PartNum varchar(255)
	declare @Devices varchar(2500)
	
	declare @DeviceContent varchar(2500)
	declare @DeviceModelContent varchar(2500)
	declare @ModelContent varchar(2500)	
	
	declare @Device varchar(2500)
	

	declare @CatalogUploadItemCursor cursor
	set @CatalogUploadItemCursor  = cursor for
		select PartNo, Devices
		from dbo.VN_tblCatalogUploadItem
		with (nolock)
		where CatalogUploadSetId = @CatalogUploadSetId
		
		
	open @CatalogUploadItemCursor
	
	fetch next
	from @CatalogUploadItemCursor 
	into @PartNum, @Devices
	
	while @@fetch_status = 0	--@CatalogUploadItemCursor
	begin
	
		declare @DeviceCursor cursor
		set @DeviceCursor  = cursor for
			select Content
			from dbo.VN_fnSplitString(@Devices, '|')
			
		open @DeviceCursor
	
		fetch next
		from @DeviceCursor 
		into @DeviceContent
		
		while @@fetch_status = 0	--@DeviceCursor
		begin
						
			declare @DeviceModelCursor cursor
			set @DeviceModelCursor  = cursor for
				select Content
				from dbo.VN_fnSplitString(@DeviceContent, ':')
			
			open @DeviceModelCursor
			
			fetch next					--no loop needed
			from @DeviceModelCursor 
			into @DeviceModelContent	--will store Device, e.g. iPod
			
			set @Device = @DeviceModelContent	--save device
			
			fetch next					
			from @DeviceModelCursor 
			into @DeviceModelContent	--will store comma delim Models, e.g iPod 2, iPod 3
			
			close @DeviceModelCursor	--can close this cursor because no need to iterate
			deallocate @DeviceModelCursor											
						
			declare @ModelCursor cursor
			set @ModelCursor  = cursor for
				select Content
				from dbo.VN_fnSplitString(@DeviceModelContent, ',')
			
			open @ModelCursor
			
			fetch next					
			from @ModelCursor 
			into @ModelContent	--will store a model, e.g iPod 2
			
			while @@fetch_status = 0	--@ModelCursor
			begin
			
				insert into @FlatDevices
				(Device, Model, PartNum)
				values
				(@Device, @ModelContent, @PartNum)				
							
				fetch next					
				from @ModelCursor 
				into @ModelContent	--will store a model, e.g iPod 2									
			
			end			
			
			close @ModelCursor
			deallocate @ModelCursor			
			
			--drop #DeviceModelContentTemp
			
		
			fetch next
			from @DeviceCursor 
			into @DeviceContent
		
		end
		
		close @DeviceCursor
		deallocate @DeviceCursor	
			
		fetch next
		from @CatalogUploadItemCursor 
		into @PartNum, @Devices		
	
	end	
		
	close @CatalogUploadItemCursor
	deallocate @CatalogUploadItemCursor
	
	Return
END