/*
	EGGMAIN.C (Extension Gatherer & Grouper)

	Main source code file
*/

//#include <alloc.h>
#include <ctype.h>
#include <io.h>
#include <conio.h>
#include <dos.h>
#include <string.h>
//#include <dir.h>
#include <stdio.h>
#include <stdlib.h>
#include <direct.h>
#include "egg.h"


char *argv_path = NULL;
struct summary_type path_summary;		/* no need to set to zero */
struct summary_type master_summary;
char current_path[MAX_PATH_LENGTH + 5];
char base_path[MAX_PATH_LENGTH + 5];
int active_drive;	/* drive active when program was run */
char *work_path_buffer;
int paths_exist['Z'];


/* Gets current path summary structure */
struct summary_type *get_path_summary(void)
{
	return(&path_summary);
}/* get_path_summary() */


/* Gets the base path */
char *get_base_path(void)
{
	return(base_path);
}/* get_base_path() */


/* Gets the work path */
char *get_work_path(void)
{
	return(work_path_buffer);
}/* get_work_path() */


/* Gets bytes_total from path summary structure */
double get_path_t_size(void)
{
	return(path_summary.bytes_total);
}/* get_path_t_size() */


/* Gets clusters_total from path summary structure */
double get_path_t_clusters(void)
{
	return(path_summary.clusters_total);
}/* get_path_t_clusters() */

/* Gets bytes_search from path summary structure */
double get_path_s_size(void)
{
	return(path_summary.bytes_search);
}/* get_path_s_size() */


/* Gets clusters_search from path summary structure */
double get_path_s_clusters(void)
{
	return(path_summary.clusters_search);
}/* get_path_s_clusters() */


/* Gets bytes_search from master summary structure */
double get_master_s_size(void)
{
	return(master_summary.bytes_search);
}/* get_master_s_size() */


/* Gets clusters_search from master summary structure */
double get_master_s_clusters(void)
{
	return(master_summary.clusters_search);
}/* get_master_s_clusters() */


/* Gets bytes_total from master summary structure */
double get_master_t_size(void)
{
	return(master_summary.bytes_total);
}/* get_master_t_size() */


/* Gets clusters_total from master summary structure */
double get_master_t_clusters(void)
{
	return(master_summary.clusters_total);
}/* get_master_t_clusters() */


/* Gets files_search from path summary structure */
long get_path_s_files(void)
{
	return(path_summary.files_search);
}/* get_path_s_files() */


/* Gets files_search from master summary structure */
long get_master_s_files(void)
{
	return(master_summary.files_search);
}/* get_master_s_files() */


/* Gets numexts_search from path summary structure */
unsigned int get_path_s_numexts(void)
{
	return(path_summary.numexts_search);
}/* get_path_s_files() */


/* Gets numexts_search from master summary structure */
unsigned int get_master_s_numexts(void)
{
	return(master_summary.numexts_search);
}/* get_master_s_files() */


/* Gets files_total from path summary structure */
long get_path_t_files(void)
{
	return(path_summary.files_total);
}/* get_path_t_files() */


/* Gets files_total from master summary structure */
long get_master_t_files(void)
{
	return(master_summary.files_total);
}/* get_master_t_files() */


/* Gets numexts_total from master summary structure */
unsigned int get_master_t_numexts(void)
{
	return(master_summary.numexts_total);
}/* get_master_t_numexts() */


/* Gets numexts_total from path summary structure */
unsigned int get_path_t_numexts(void)
{
	return(path_summary.numexts_total);
}/* get_path_t_numexts() */


/* Update path summary structure for item found and meeting criteria */
void update_path_s_summary(struct node_gen_type *data)
{
	path_summary.files_search += data->how_many;
	path_summary.bytes_search += data->bytes;
	path_summary.numexts_search++;
	path_summary.clusters_search += data->clusters;
}/* update_path_s_summary() */


/* Update master summary structure for item found and meeting criteria */
void update_master_s_summary(struct node_gen_type *data)
{
	master_summary.files_search += data->how_many;
	master_summary.bytes_search += data->bytes;
	master_summary.numexts_search++;
	master_summary.clusters_search += data->clusters;
}/* master_path_s_summary() */


/* Check if extension meets criteria */
int post_process_file(struct node_gen_type *data, double size)
{
	if (egg_extension_quantity(0) && !match_extension_quantity(0, data, size))
		return(0);

	if (egg_extension_quantity(1) && !match_extension_quantity(1, data, size))
		return(0);

	return(1);
}/* post_process_file() */


/* Check if file meets criteria */
int pre_process_file(struct _finddata_t *fb, struct node_gen_type *data)
{
	if (egg_extensions(0) && !match_extensions(0, data->extension))
		return(0);

	if (egg_extensions(1) && !match_extensions(1, data->extension))
		return(0);

	if (egg_file_quantity(0) && !match_file_quantity(0, fb))
		return(0);

	if (egg_file_quantity(1) && !match_file_quantity(1, fb))
		return(0);

	if (egg_attribs(0) && !match_attribs(0, fb->attrib))
		return(0);

	if (egg_attribs(1) && !match_attribs(1, fb->attrib))
		return(0);

	if (egg_dates() && !match_dates(fb->time_write))	//Cliff: issues?
		return(0);

	return(1);
}/* pre_process_file() */


/* Quit program */
void shutdown(int how)
{
	destroy_app_list();
	destroy_token_list();
	destroy_param_lists();
	destroy_extrema();
	destroy_path_array();
	destroy_master_array();
	destroy_tree();
	destroy_list();
	fcloseall();

	if (active_drive)
		_chdrive(active_drive);

	#ifdef EGGBUG
	//printf("\nafter shutdown: %lu %lu", coreleft(), farcoreleft());
	//getch();
	#endif

	if (how != NO_EXIT)
		exit(EXIT_SUCCESS);
}/* shutdown() */


/* Perform EGG functions on a path */
void process_path(char *path)
{
	struct _finddata_t file_block;	//Cliff
	struct node_gen_type data;
	char path_plus_buffer[MAX_PATH_LENGTH + 5];	/* needs to fit \*.* */
	char *per_ptr;
	long hFile;		//Cliff


	strcpy(path_plus_buffer, path);

	if (strlen(path) > 3 || path[2] == '.')	 /* not the root or used . */
		strcat(path_plus_buffer, "\\*.*");
	else
		strcat(path_plus_buffer, "*.*");

	//file_block.attrib =  _A_SYSTEM | _A_HIDDEN | _A_SUBDIR	//Cliff ??

	hFile = _findfirst(path_plus_buffer, &file_block);

	if (hFile != -1L)
	{		
		check_key();	/* quit if ESC pressed */

		do
		{
			/* check for subdirectories */
			if (file_block.attrib & _A_SUBDIR)
			{
				if (*file_block.name != '.')
				{
					if (egg_subdirs())
					{
						if (strlen(path) > MAX_PATH_LENGTH - 5)	/* for sure */
							egg_error(PATH_TOOLONG_ERROR);

						add_to_list(path, file_block.name);
					}/* if */
				}/* if */
			}
			else
			{
			/*
				Some settings are processed while entries are being
				added to tree.
			*/
				per_ptr = strchr(file_block.name, '.');

				if (per_ptr != NULL)
					strcpy(data.extension, per_ptr + 1);
				else
					*data.extension = '\0';

				data.bytes = (long unsigned) file_block.size;

				/* add to extension trees */
				/* call this before adding to the non-extension trees */
				add_to_tree(EMASTER, &data);	/* also adds to EPATH */

				if (pre_process_file(&file_block, &data))
					add_to_tree(MASTER, &data);		/* also PATH */

				path_summary.bytes_total += data.bytes;
				path_summary.files_total++;		/* total files found */
				path_summary.clusters_total += to_clusters(data.bytes);

				master_summary.bytes_total += data.bytes;
				master_summary.files_total++;		/* total files found */
				master_summary.clusters_total += to_clusters(data.bytes);
			}/* else */

			check_key();

		}while (!_findnext(hFile, &file_block));

	}/* if */

    _findclose( hFile );	//Cliff: needed?

}/* process_path() */


/*
	Returns 0 for root, 1 for 1st level , etc.
	Assumes that path is in form C:\....
*/
int directory_level(char *active_path)
{
		return(strlen(active_path) < 4 ? 0 : char_count(active_path, '\\'));
}/* directory_level() */


/* Determines if EGG has any valid paths to work on */
int no_paths_exist(char *list)
{
	char *ptr;


	ptr = list;
	while (*ptr)
	{
		if (paths_exist[*ptr])
			return(0);	/* at least one drive has the start path */

		ptr++;
	}/* while */

	return(1);
}/* no_paths_exist() */


/* Performs initial processing of output file */
//void handle_outfile(int vert_pos)
void handle_outfile(void)
{
	int key;


	if (egg_outfile() || egg_outfile_scr())/* try to create the output file */
	{
		/* see if file already exists */
		if (!egg_override() && !access(egg_outfile_name(), 0))
		{
			cprintf("%s already exists.  Continue? (Y or N): ",
				egg_outfile_name());

			key = toupper(getch());

			if (key != 'Y')
				shutdown(EXIT);	/* quit */

//			gotoxy(1, vert_pos);	//Cliff
//			clreol();				//Cliff
		}/* if */

		/* now try to open the file */

		if (set_outfile(fopen(egg_outfile_name(), "wt")) == NULL)
			egg_error(OUTFILE_CREATE_ERROR);

		cputs("EGG is working...");
	}/* if */
	else if (egg_subdirs() && egg_detail() != ALL_DETAIL)
	{
		/* just in case EGG doesn't find anything right away */
		//gotoxy(1, vert_pos);	//Cliff
		//clreol();				//Cliff

		cputs("EGG is searching...");
	}/* if */
}/* handle_outfile() */


int main(int argc, char *argv[])
{
	char active_path_buffer[MAX_PATH_LENGTH + 1];
	char start_path_buffer[MAX_PATH_LENGTH + 1];
	struct summary_type init = {0, 0, 0, 0, 0, 0, 0, 0, 0, 1, 1, 0, 0, 0, 0};
//	int vert_pos;
	int curr_dir_level;
	int path_count = 0;
	char work_drive;
	struct disk_info_type disk_info;


	init_tree();	/* initialize binary trees */

	//textbackground(MAIN_BACK);	/* screen attribs */
	//textcolor(MAIN_FORE);

	find_current_path(active_path_buffer);
	curr_dir_level = directory_level(active_path_buffer);

	get_screen_rows();	

	process_command_line(argc, argv, &argv_path); /* process args, quit if bad */
	set_sort();		/* set compare() function */
//	harderr(handler);	/* regsiter hardware error handler */	//Cliff

	#ifdef EGGBUG
	printf("\nbefore processing: %lu %lu", coreleft(), farcoreleft());
	getch();
	#endif

//	vert_pos = wherey();		/* get current vertical position */

//	handle_outfile(vert_pos);	/* set up output file */
	handle_outfile();	/* set up output file */	//Cliff

	print_line("\n\r");
	print_line("EGG 0.9 Beta Release, Copyright 1995 Clifford M. Spielman\n\r");

	if (egg_today())	/* add date to output */
		print_date_time();

	active_drive = _getdrive();

	#ifdef EGGBUG
		printf("\nbefore drive: %lu %lu", coreleft(), farcoreleft());
		if (getch() == 27)
			shutdown(!NO_EXIT);
	#endif

	while (!drive_list_empty())		/* loop through drive list */
	{
		work_drive = next_drive();

		if (argv_path != NULL)
			strcpy(start_path_buffer, argv_path);
		else
			strcpy(start_path_buffer, active_path_buffer);

		start_path_buffer[0] = work_drive;
		add_to_list("", start_path_buffer);

		master_summary.drives_total++;

		/* check to see if the start directory exists */
		if (chdir(start_path_buffer))
		{
			remove_drive();		/* remove drive from list */
			continue;			/* skip that drive */
		}

		paths_exist[work_drive] = 1;	//path exists on the drive

		/*
			get the disk information such as cluster size for
			each drive. Think about this!
		*/

		find_disk_info(&disk_info, work_drive);		/* get disk stats */

		/* these next four lines are for multiple drives */
		master_summary.num_bytes += disk_info.num_bytes;
		master_summary.num_clusters += disk_info.num_clusters;
		master_summary.used_bytes += disk_info.used_bytes;
		master_summary.used_clusters += disk_info.used_clusters;

		while (!path_list_empty())		/* loop through paths */
		{
			path_summary = init;   	/* reset fields to 0 */
			work_path_buffer = next_path();

			if (!path_count)
				strcpy(base_path, work_path_buffer);

			if (!egg_subdirs() || directory_level(work_path_buffer) <= egg_subdirs() + curr_dir_level
				|| egg_subdirs() == ALL_DIRS)
			{
				path_summary.dirs_total++;
				master_summary.dirs_total++;
				process_path(work_path_buffer);
				path_summary.numexts_total = get_epath_tree_size();
				print_path_results(work_path_buffer, start_path_buffer, &path_summary, &disk_info);	//Cliff
				reset_trees();		/* reset trees for next path */
				destroy_path_array();
			}/* if */

			delete_from_list();		/* remove path */

			path_count++;
		}/* while */

		/* restore drive path before done with drive */
		chdir(active_path_buffer);

		master_summary.drives_search++;

		#ifdef EGGBUG
			//printf("\nafter drive: %lu %lu", coreleft(), farcoreleft());
			//if (getch() == 27)
			//	shutdown(!NO_EXIT);
		#endif
	}/* while */

	if (egg_detail() == SUMMARY_DETAIL && !egg_outfile() && egg_subdirs())
		cputs("\n\r");		/* skip one line */		//Cliff: try this instead
		//clear_line(vert_pos);

	master_summary.numexts_total = get_emaster_tree_size();

	/* always display master results if /s was used */
	if (egg_drives() && no_paths_exist(egg_drive_list()))
		print_line("EGG Listing: NO OUTPUT\n\r");
	else if (egg_subdirs() || egg_drives())
		print_master_results(&master_summary, &disk_info);

	if (egg_command())	/* display command-line */
	{
		print_line("\n\r");
		print_line("EGG Command: %s %s\n\r", argv[0], argv[1]);
	}/* if */

	if (egg_outfile())
		cputs("\n\r");		/* skip one line */

	#ifdef EGGBUG
	printf("\nafter processing: %lu %lu", coreleft(), farcoreleft());
	getch();
	#endif

	shutdown(EXIT);

	return(0);	/* shut up warning */
}/* main() */
