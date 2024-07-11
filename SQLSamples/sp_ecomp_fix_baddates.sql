CREATE PROCEDURE sp_ecomp_fix_baddates 

@ekval VARCHAR(30),
@epeople INT,
@show_progress INTEGER

AS


SET NOCOUNT ON		--suppress output

DECLARE @eideb INT
DECLARE @edbeg DATETIME
DECLARE @edend DATETIME
DECLARE @eidebprev INT
DECLARE @emid INT
DECLARE @edendprev DATETIME
DECLARE @num_cursor_rows INT
DECLARE @current_row INT

SET @current_row = 1

DECLARE ecomp_cursor CURSOR LOCAL FOR 		--contains EComp records for specific EmKind value from other cursor		
	SELECT EmFlxIdEb, EmDateBeg, EmDateEnd = ISNULL(EmDateEnd, CONVERT(DATETIME, '1/1/9999')), EmFlxId	--ISNULL there because NULL screws up Ascending order
	FROM VHR_DATASQL..EComp	
	WHERE EmKind = @ekval
	AND (EmFlxIdEb = @epeople OR @epeople = -1)
	ORDER BY EmFlxIdEb ASC, EmDateBeg ASC, EmDateEnd ASC

	OPEN ecomp_cursor

	SET @num_cursor_rows = @@CURSOR_ROWS

	FETCH NEXT FROM ecomp_cursor INTO @eideb, @edbeg, @edend, @emid	--go to first record and store values
	
	IF (@edbeg > @edend)
	BEGIN
		UPDATE VHR_DATASQL..EComp
		SET EmDateEnd = EmDateBeg		
		WHERE EmFlxId = @emid
	END

	IF (@show_progress <> 0) 
		PRINT RTRIM(CONVERT(VARCHAR(7), @current_row)) + ' OF ' + RTRIM(CONVERT(VARCHAR(7), @num_cursor_rows)) + ' Rows Complete'

	IF (@num_cursor_rows > 1)
	BEGIN

	SET @eidebprev = @eideb	--save first record contents
	SET @edendprev = @edend

	WHILE (@@FETCH_STATUS = 0)	--scan cursor records
	BEGIN
		FETCH NEXT FROM ecomp_cursor INTO @eideb, @edbeg, @edend, @emid	--go to next record

		IF (@@FETCH_STATUS = 0)
		BEGIN				
			
			IF (@eideb = @eidebprev)	--check if still on same person as previous record
			BEGIN
				IF (@edbeg <= @edendprev)	--check DateBeg to prior DateEnd, assumes that DateBeg should be greater
				BEGIN
					--set DateBeg to prior DateEnd plus 1 day, then check current DateEnd for conflicts
					UPDATE VHR_DATASQL..EComp
					SET EmDateBeg = DATEADD(day, 1, @edendprev)					
					WHERE EmFlxId = @emid
					
					SET @edbeg = DATEADD(day, 1, @edendprev)	--bump it up for use in next IF
			
				END
			END

			IF (@edbeg > @edend)
			BEGIN
				--PRINT "BEG > END: " + CONVERT(VARCHAR(20), @edbeg) + " > " + CONVERT(VARCHAR(20),@edend)

				UPDATE VHR_DATASQL..EComp
				SET EmDateEnd = EmDateBeg				
				WHERE EmFlxId = @emid

				SET @edend = @edbeg
				
			END		
			
			SET @eidebprev = @eideb			--save current EmFlxIdEb
			SET @edendprev = @edend		--save current EmDateEnd

			SET @current_row = @current_row + 1

			IF (@show_progress <> 0) 		
				PRINT RTRIM(CONVERT(VARCHAR(7), @current_row)) + ' OF ' + RTRIM(CONVERT(VARCHAR(7), @num_cursor_rows)) + ' Rows Complete'
		
		END
	
	END
	END

	CLOSE ecomp_cursor		--done with cursor
	DEALLOCATE ecomp_cursor


SET NOCOUNT OFF

