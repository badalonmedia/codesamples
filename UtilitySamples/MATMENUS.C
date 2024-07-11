/*
	MATMENUS.C

	This module contains the menu routines for the MouseMatician
*/

#include <share.h>
#include <sys\stat.h>
#include <io.h>
#include <time.h>
#include <stdlib.h>
#include <dir.h>
#include <string.h>
#include <conio.h>
#include <stdio.h>
#include <ctype.h>
#include "mmpack.h"
#include "matmenus.h"
#include "mouselkp.h"
#include "keys.h"
#ifdef MMDEMO
	#include"mmdemo.h"
#endif

extern int times_list[];
extern struct color_type colors[];
extern int text_type;
extern char program_path[];
extern char local_path[];
extern char setup_fpath[];
extern char setup_path[];
extern char high_fpath[];
extern char *mess_lines[];
extern char misc_buffer[];
extern char skill_buffer[];
extern char player_name[];
extern int graph_type;
extern struct setup_type setup;
extern int setup_changed;
extern int shoot_time;
extern int game_count;
extern int exam_count;
extern struct game_type temp_game;
extern struct game_type game_stats;
extern struct exam_type exstats;

char eg_buffer[EXAM_GAMES_MAX_CHARS + CGETS_OFFS];	/* exam games */
char si_buffer[SCAN_INT_MAX_CHARS + CGETS_OFFS];	/* scan interval */

int men_choice_color;
int men_num_color;
int men_name_color;
int high_score_color;
int setup_color;

struct win_attribs_type main_attribs = {
	SINGLE_LINE, MAIN_FOREGRND, MAIN_BACKGRND, MAIN_FOREGRND, MAIN_BACKGRND,
	MAIN_INPUT_FOREGRND, MAIN_INPUT_BACKGRND, MAIN_LEFT, MAIN_TOP, MAIN_RIGHT,
	MAIN_BOTTOM, MIN_HORZ, MIN_VERT, PROGRAM_NAME
};

#ifdef MOUSEMAT
struct menu_type main_menu = {1, 6, "Main Menu",
	{{"Play Game", 0},
	{"Progress Report", 9},
	{"High Scores", 0},
	{"Administer Exam", 11},
	{"Game Setup Menu", 0},
	{"Quit", 0}}
};
#endif
#ifdef MMDEMO
struct menu_type main_menu = {1, 7, "Main Menu",
	{{"Play a Game", 0},
	{"Progress Report", 9},
	{"High Scores", 0},
	{"Administer Exam", 11},
	{"Game Setup Menu", 0},
	{SELF_DEMO_NAME, 7},
	{"Quit", 0}}
};
#endif

struct menu_type color_menu = {0, 8, "Game Colors Menu",
	{{"Single Target Color", 0},
	{"Double Target Color", 0},
	{"Drag Target Color", 1},
	{"Spoiler Target Color", 2},
	{"Explosion Color", 0},
	{"Scoreboard Color", 1},
	{"Font Color", 0},
	{"Previous Menu", 9}}
};

struct menu_type setup_menu = {0, 10, "Game Setup Menu",
	{{"Toggle Sound", 7},
	{"Toggle Lefty/Righty", 7},
	{"Toggle Scan", 9},
	{"Exam Games", 5},
	{"Scan Interval", 5},
	{"Current Settings", 1},
	{"Default Settings", 0},
	{"Path Menu", 0},
	{"Game Colors Menu", 5},
	{"Previous Menu", 9}}
};

struct menu_type path_menu = {0, 4, "Path Menu",
	{{"Setup Path", 0},
	{"High Score Path", 0},
	{"Help Path", 1},
	{"Previous Menu", 9}}
};


int match_option(struct menu_type *menu, int key)
{
	int count;
	int offset;

	for (count = 0; count < menu->num_options; count++)
	{
		offset = menu->options[count].high_char;
		if (key == toupper(menu->options[count].text[offset]))
			return(count);
	}

	return(-1);	/* not found */
}/* match_option() */


void display_menu(struct menu_type *menu, WINHANDLE main_handle)
{
	int count;
	int minc;


	hide_mouse_cursor();

	clrscr();

	textcolor(men_name_color);
	gotoxy(win_center(menu->name), 5);
	cputs(menu->name);

	minc = MENU_START_ROW;

	for (count = 0; count < menu->num_options; count++)
	{
		if (count == menu->num_options - 1)
			minc++;

		textcolor(men_num_color);
		if (text_type == TEXT_MONO)
			lowvideo();
		gotoxy(MENU_START_COL, minc + count);
		if (count + 1 == menu->num_options)
			putch('0');
		else
			cprintf("%d", count + 1);
/*		if (text_type == TEXT_MONO)
			normvideo();*/

		textcolor(men_choice_color);
		gotoxy(MENU_START_COL + 1, minc + count);
		cprintf(") %s",menu->options[count].text);
		textcolor(men_num_color);
		if (text_type == TEXT_MONO)
			lowvideo();
		gotoxy(MENU_START_COL + 3 + menu->options[count].high_char,
			minc + count);
		putch(menu->options[count].text[menu->options[count].high_char]);
	}

	textcolor(WHITE);
	gotoxy(get_x_winmax(main_handle) - strlen(HELP_PROMPT) - 2,
		get_y_winmax(main_handle) - 2);
	cputs(HELP_PROMPT);

	textcolor(men_choice_color);
	gotoxy(PROMPT_COL, PROMPT_ROW);
	clreol();
	cputs(MENU_PROMPT);

	display_mouse_cursor();	/* reset_mouse() hides the cursor */
	display_mouse_cursor();
}/* display_menu() */


/*
	The keys Q and M are reserved for Quit and Previous Menu
*/
void menu_process( int *key, struct button_info_type *binfo,
	struct menu_type *menu, int *ympos, int *xmpos)
{
	int key_pos;
	int offset;

	if (isalpha(*key))		/* character is a letter */
	{
		if (*key == 'Q' && menu->level == 1)
			*key = '0';
		else if (*key == 'M' && menu->level == 0)
			*key = 0;
		else if ((key_pos = match_option(menu, *key)) != -1)
			*key = '0' + key_pos + 1;
	}

	if (binfo->vert != -1)		/* left button was pressed */
	{
		*ympos = binfo->vert / MOUSE_PIXELS - MENU_START_ROW + 1;

		if ((*ympos >= 1 && *ympos < menu->num_options) ||
			*ympos == menu->num_options + 1)
		{
			*xmpos = binfo->horz / MOUSE_PIXELS;

			if (*ympos == menu->num_options + 1)
			{
				*ympos = 0;
				offset = menu->num_options - 1;
			}
			else
				offset = *ympos - 1;

			if (*xmpos >= MENU_START_COL &&
				*xmpos <= MENU_START_COL + strlen(menu->options[offset].text) + 2)
				*key = '0' + *ympos;
		}/* if */
	}/* if */
}/* menu_process() */


void menu_finish(int *refresh, int *key, struct menu_type *menu,
	struct button_info_type *binfo, WINHANDLE main_handle)
{
	if (*refresh)
	{
		clrscr();		/* check */
		display_menu(menu, main_handle);
		display_mouse_cursor();
		*refresh = 0;
	}
	else 	/* restore cursor position at prompt */
	{
		gotoxy(5 + strlen(MENU_PROMPT), PROMPT_ROW);
		#ifdef MMDEMO
		if (self_status() == OFF)	/* clreol screws up guided tour */
		#endif
		clreol();		/* does not move the cursor, try this */
		display_mouse_cursor();
	}

	if (*key != '0' && *key != ESC)
		*key = toupper(wait_inert(binfo));
}/* menu_finish() */


void do_path_menu(WINHANDLE main_handle)
{
	int key;
	struct button_info_type button_info;
	int refresh = 0;	/* flag to redraw menu or not */
	int xmpos, ympos;
	static char high_back[MYPATH_MAX + 1];		/* current directory settings */
	static char setup_fback[MAXPATH + 1];
	static char high_fback[MAXPATH + 1];
	static char setup_back[MYPATH_MAX + 1];
	static char setup_orig[MYPATH_MAX + 1];
	static char high_orig[MYPATH_MAX + 1];
	char *dir_prompt = "Enter new directory: ";
	int result;	/* for mycgets() */
	int dresult, eresult;
	int key2;		/* for win_message() */
	int setup_found;	/* flag for whether MOUSEMAT.CFG exists in new path */


	set_help_topic(PATH_MENU_TOPIC);

	display_menu(&path_menu, main_handle);

	key = toupper(wait_inert(&button_info));		/* start loop */

	while (key != '0' && key != PREVIOUS_MENU_KEY && key != ESC)
	{
		menu_process(&key, &button_info, &setup_menu, &ympos, &xmpos);

		switch(key)
		{
			case '0':		/* will quit */
				break;

			case '1': 	/* setup directory */
				hide_mouse_cursor();

				strcpy(setup_back, &setup_path[2]);
				strcpy(setup_orig, &setup_path[2]);

				dresult = 0;
				eresult = 0;

				do
				{
					result = win_mycgets(15, dir_prompt, setup_path, setup_back,
						NOCLEAR, EMPTY, TRIM, UPPER, RESTORE, "Setup Path");

					if (result != ESCAPE_ERROR)
					{
						strcpy(setup_back, &setup_path[2]);

						dresult = valid_path_syntax(&setup_path[2]);
						eresult = access(&setup_path[2], 0);

						if (!dresult && eresult)
							myerror(NONFATAL_PAUSE, PATH_EXIST_ERROR);

						dresult = dresult || eresult;
					}/* if */

				}while (result != ESCAPE_ERROR && dresult);

				if (result == ESCAPE_ERROR)
				{
					strcpy(&setup_path[2], setup_orig);
					display_mouse_cursor();
					break;	/* leave this case */
				}

				if (!strcmp(&setup_path[2], setup_orig))
				{
					display_mouse_cursor();
					break;
				}

				/* first check to see if there is a MOUSEMAT.CFG there */
				strcpy(setup_fback, setup_fpath);	/* save full path */

				/* form new full path */
				strcpy(setup_fpath, &setup_path[2]);
				if (strlen(setup_fpath) > 3)	/* not a root directory */
					strcat(setup_fpath, "\\");	/* add a slash */
				strcat(setup_fpath, SETUP_FILE);	/* add the filename */

				if ((setup_found = access(setup_fpath, 0)) != 0)
				{
					mess_lines[0] = "There is currently no MOUSEMAT.CFG setup";
					mess_lines[1] = "file in the specified path.";
					mess_lines[2] = "";
					mess_lines[3] = "Are you sure about this path? (Y or N): ";

					win_error(win_message(mess_lines, 4, "", &key2, CENTER,
						WAIT, STRETCH));

					if (toupper(key2) != 'Y')
					{
						strcpy(&setup_path[2], setup_orig);
						strcpy(setup_fpath, setup_fback);
						display_mouse_cursor();
						break;
					}
				}/* if */

				/* try to write LOCAL.CFG again */

				/* change file attrib */
				if (chmod(local_path, S_IWRITE))
				{
					myerror(NONFATAL_PAUSE, LOCAL_ATTRIB_ERROR);
					strcpy(&setup_path[2], setup_back);
					display_mouse_cursor();
					break;
				}/* if */

				if (write_local() == FAILURE)
				{
					strcpy(&setup_path[2], setup_back);
					display_mouse_cursor();
					break;
				}/* if */

				if (chmod(local_path, S_IREAD))
					myerror(NONFATAL_PAUSE, LOCAL_ATTRIB_ERROR);

				/* LOCAL.CFG has been successfully rewritten */

				/* see if user wants to load the setup file */
				if (!setup_found)
				{
					mess_lines[0] = "Do you want to load the new setup file? (Y or N): ";
					win_error(win_message(mess_lines, 1, "", &key2, CENTER,
						WAIT, STRETCH));

					if (toupper(key2) == 'Y')	/* load new setup file */
					{
						if (read_setup() == FAILURE)		/* use defaults */
						{
							setup_changed = 1;		/* changed */
							get_graph_drmd();
							set_defaults(!NO_PATHS);

						}/* if */
					}/* if */
				}/* if */

				display_mouse_cursor();

				break;

			case '2':		/* high score directory */
				hide_mouse_cursor();

				strcpy(high_back, &setup.high_path[2]);
				strcpy(high_orig, &setup.high_path[2]);

				dresult = 0;
				eresult = 0;

				do
				{
					result = win_mycgets(15, dir_prompt, setup.high_path, high_back,
						NOCLEAR, EMPTY, TRIM, UPPER, RESTORE, "High Score Path");

					if (result != ESCAPE_ERROR)
					{
						strcpy(high_back, &setup.high_path[2]);

						dresult = valid_path_syntax(&setup.high_path[2]);
						eresult = access(&setup.high_path[2], 0);

						if (!dresult && eresult)
							myerror(NONFATAL_PAUSE, PATH_EXIST_ERROR);

						dresult = dresult || eresult;
					}/* if */

				}while (result != ESCAPE_ERROR && dresult);

				if (result == ESCAPE_ERROR)
				{
					strcpy(&setup.high_path[2], high_orig);
					display_mouse_cursor();
					break;	/* leave this case */
				}

				if (!strcmp(&setup.high_path[2], high_orig))
				{
					display_mouse_cursor();
					break;
				}

				/* first check to see if there is a MOUSEMAT.HI there */
				strcpy(high_fback, high_fpath);	/* save full path */

				/* form new full path */
				strcpy(high_fpath, &setup.high_path[2]);
				if (strlen(high_fpath) > 3)	/* not a root directory */
					strcat(high_fpath, "\\");	/* add a slash */
				strcat(high_fpath, SCORE_FILE);	/* add the filename */

				setup_changed = 1;

				if (access(high_fpath, 0))
				{
					mess_lines[0] = "There is currently no MOUSEMAT.HI high score";
					mess_lines[1] = "file in the specified path.";
					mess_lines[2] = "";
					mess_lines[3] = "Are you sure about this path? (Y or N): ";

					win_error(win_message(mess_lines, 4, "", &key2, CENTER,
						WAIT, STRETCH));

					if (toupper(key2) != 'Y')
					{
						strcpy(&setup.high_path[2], high_orig);
						strcpy(high_fpath, high_fback);
						display_mouse_cursor();
						setup_changed = 0;
						break;
					}
				}/* if */

				display_mouse_cursor();

				break;

			case '3':  	/* help directory */

				break;

			default:
				#ifdef MMDEMO
				if (key == TEXT_START_KEY)	/* ignore in demo */
					break;
				#endif
				myerror(NONFATAL_PAUSE, MENU_CHOICE_ERROR);

		}/* switch */

		menu_finish(&refresh, &key, &setup_menu, &button_info, main_handle);
	}/* while */

	unset_help_topic();
}/* do_path_menu() */


void do_color_menu(WINHANDLE main_handle)
{
	int key;
	int key2;		/* for win_message() */
	struct button_info_type button_info;
	int refresh = 0;	/* flag to redraw menu or not */
	int xmpos, ympos;
	int result;
	int color_index;
	char *win_title;


	set_help_topic(COLOR_MENU_TOPIC);

	display_menu(&color_menu, main_handle);

	key = toupper(wait_inert(&button_info));		/* start loop */

	while (key != '0' && key != PREVIOUS_MENU_KEY && key != ESC)
	{
		menu_process(&key, &button_info, &color_menu, &ympos, &xmpos);

		color_index = -1;		/* invalid */

		switch(key)
		{
			case '0':		/* will quit */
				break;

			case '1':
				color_index = SINGLE_TARG_IDX;
				win_title = "Single Target";
				break;

			case '2':
				color_index = DOUBLE_TARG_IDX;
				win_title = "Double Target";
				break;

			case '3':
				color_index = DRAG_TARG_IDX;
				win_title = "Drag Target";
				break;

			case '4':
				color_index = SPOIL_TARG_IDX;
				win_title = "Spoiler Target";
				break;

			case '5':
				color_index = EXPLOSION_ITEM_IDX;
				win_title = "Explosion";
				break;

			case '6':
				color_index = LINE_ITEM_IDX;
				win_title = "Scoreboard";
				break;

			case '7':
				color_index = FONT_ITEM_IDX;
				win_title = "Font";
				break;

			default:
				#ifdef MMDEMO
				if (key == TEXT_START_KEY)	/* ignore in demo */
					break;
				#endif
				myerror(NONFATAL_PAUSE, MENU_CHOICE_ERROR);
		}/* switch */

		if (color_index != -1)
		{
			#ifdef MMDEMO
			/* allow colors to change in guided tour */
			/* this is even though the changes are ignored */
			if (self_status() == OFF && graph_type != GRAPH_COLOR)
				myerror(NONFATAL_PAUSE, COLOR_CHANGE_ERROR);
			#endif
			#ifdef MOUSEMAT
			if (graph_type != GRAPH_COLOR)
				myerror(NONFATAL_PAUSE, COLOR_CHANGE_ERROR);
			#endif
			else
			{
				refresh = 1;
				sprintf(misc_buffer, "Current color is %s",
					color_word(setup.colors[color_index]));
				mess_lines[0] = misc_buffer;
				mess_lines[1] = "";
				mess_lines[2] = "Select a new one...";

				win_error(win_message(mess_lines, 3, win_title, &key2, 15,
					NOWAIT, STRETCH));

				if (key2 == ESC)	/* don't do lookup */
					result = -1;	/* fool the function */
				else
					result = window_lookup(colors, 15,	NUM_COLORS,
						"COLORS");

				/* something was selected */
				if (result != -1 && graph_type == GRAPH_COLOR)
				{
					setup.colors[color_index] = colors[result].value;
					setup_changed = 1;
				}
			}/* else */
		}/* if */

		menu_finish(&refresh, &key, &color_menu, &button_info, main_handle);
	}/* while */

	unset_help_topic();
}/* do_color_menu() */


void do_setup_menu(WINHANDLE main_handle)
{
	int key;
	struct button_info_type button_info;
	int refresh = 0;	/* flag to redraw menu or not */
	int xmpos, ympos;
	int mouse_toggles = 0, sound_toggles = 0, high_toggles = 0;
	int eg_result, si_result, valid_result;


	set_help_topic(SETUP_MENU_TOPIC);

	display_menu(&setup_menu, main_handle);

	key = toupper(wait_inert(&button_info));		/* start loop */

	while (key != '0' && key != PREVIOUS_MENU_KEY && key != ESC)
	{
		menu_process(&key, &button_info, &setup_menu, &ympos, &xmpos);

		switch(key)
		{
			case '0':		/* will quit */
				break;

			case '1':	/* sound flag */
				refresh = 1;
				sound_toggles++;

				/* only offer to write setup file if odd toggles */
				if (sound_toggles & 1)	/* odd */
					setup_changed = 1;
				else
					setup_changed = 0;

				if (setup.sound_val != SOUND_OFF)		/* turn of sound */
				{
					setup.sound_val = SOUND_OFF;
					mess_lines[0] = "Game sound has been turned OFF";
				}
				else
				{
					setup.sound_val = SOUND_ON;
					mess_lines[0] = "Game sound has been turned ON";
				}

				mess_lines[1] = "";
				mess_lines[2] = "Any key to continue...";

				hide_mouse_cursor();
				win_error(win_message(mess_lines, 3, "", NULL, CENTER,
					WAIT, STRETCH));
				display_mouse_cursor();

				break;

			case '2':	/* lefty/righty flag */
				refresh = 1;
				setup_changed = 1;
				mouse_toggles++;

				/* only offer to write setup file if odd toggles */
				if (mouse_toggles & 1)	/* odd */
					setup_changed = 1;
				else
					setup_changed = 0;

				if (setup.lr_val == RIGHTY)
				{
					setup.lr_val = LEFTY;
					mess_lines[0] = "The mouse is now in LEFTY mode";
					mess_lines[1] = "The left and right buttons are reversed";
				}
				else
				{
					setup.lr_val = RIGHTY;
					mess_lines[0] = "The mouse is now in RIGHTY mode";
					mess_lines[1] = "The left and right buttons are restored";
				}

				mess_lines[2] = "";
				mess_lines[3] = "Any key to continue...";

				hide_mouse_cursor();
				win_error(win_message(mess_lines, 4, "", NULL, CENTER,
					WAIT, STRETCH));
				display_mouse_cursor();

				break;

			case '3':	/* high score scan */
				refresh = 1;
				setup_changed = 1;
				high_toggles++;

				/* only offer to write setup file if odd toggles */
				if (high_toggles & 1)	/* odd */
					setup_changed = 1;
				else
					setup_changed = 0;

				if (setup.high_scan == SCAN_ON)
				{
					setup.high_scan = SCAN_OFF;
					mess_lines[0] = "The high score file WILL NOT be scanned";
				}
				else
				{
					setup.high_scan = SCAN_ON;
					mess_lines[0] = "The high score file WILL be scanned";
				}

				mess_lines[1] = "";
				mess_lines[2] = "Any key to continue...";

				hide_mouse_cursor();
				win_error(win_message(mess_lines, 3, "", NULL, CENTER,
					WAIT, STRETCH));
				display_mouse_cursor();

				break;

			case '4':		/* exam games */
				refresh = 1;

				sprintf(&eg_buffer[2], "%d", setup.exam_games);

				do
				{
					eg_result = get_exam_games(eg_buffer);

					if (eg_result != ESCAPE_ERROR)
					{
						valid_result = valid_exam_games(&eg_buffer[2]);

						if (!valid_result)
							myerror(NONFATAL_PAUSE, EXAM_GAMES_ERROR);
					}/* if */
				}while (eg_result != ESCAPE_ERROR && !valid_result);

				if (eg_result != ESCAPE_ERROR)	/* valid */
					setup.exam_games = atoi(&eg_buffer[2]);

				break;

			case '5':		/* scan interval */
				refresh = 1;

				sprintf(&si_buffer[2], "%d", setup.scan_interval);

				do
				{
					si_result = get_scan_interval(si_buffer);

					if (si_result != ESCAPE_ERROR)
					{
						valid_result = valid_scan_int(&si_buffer[2]);

						if (!valid_result)
							myerror(NONFATAL_PAUSE, SCAN_INT_ERROR);
					}/* if */
				}while (si_result != ESCAPE_ERROR && !valid_result);

				if (si_result != ESCAPE_ERROR)	/* valid */
					setup.scan_interval = atoi(&si_buffer[2]);

				break;

			case '7':  	/* list and use default values */
				refresh = 1;
				hide_mouse_cursor();

				change_active_title("Default MouseMatician Settings");
				clrscr();

				see_defaults();

				change_active_title(PROGRAM_NAME);

				break;

			case '6':  	/* list current values */
				refresh = 1;
				hide_mouse_cursor();

				change_active_title("Current MouseMatician Settings");
				clrscr();

				see_current();

				change_active_title(PROGRAM_NAME);

				break;

			case '8':		/* path menu */
				refresh = 1;
				do_path_menu(main_handle);
				break;

			case '9':		/* game colors menu */
				refresh = 1;
				do_color_menu(main_handle);
				break;

			default:
				#ifdef MMDEMO
				if (key == TEXT_START_KEY)	/* ignore in demo */
					break;
				#endif
				myerror(NONFATAL_PAUSE, MENU_CHOICE_ERROR);

		}/* switch */

		menu_finish(&refresh, &key, &setup_menu, &button_info, main_handle);
	}/* while */

	unset_help_topic();
}/* do_setup_menu() */


void do_main_menu(WINHANDLE *main_handle, struct high_type highs[])
{
	int key, key2;
	struct button_info_type button_info;
	int refresh = 0;	/* flag to redraw menu or not */
	int xmpos, ympos;
	int read_result;		/* for result of call to read_highs() */
	int valid_result, skill_result;


	set_help_topic(MAIN_MENU_TOPIC);

	display_menu(&main_menu, *main_handle);

	key = toupper(wait_inert(&button_info));		/* start loop */

	while (key != '0' && key != QUIT_KEY && key != ESC)
	{
		menu_process(&key, &button_info, &main_menu, &ympos, &xmpos);

		switch(key)
		{
			case '0':		/* will quit */
				break;

			case '1':		/* play a game */
				refresh = 1;

				hide_mouse_cursor();

				clrscr();

				#ifdef MMDEMO	/* no defaults if self demo */
				if (self_status() == ON)
					temp_game.skill_buffer[2] = '\0';
				#endif

				do
				{
					skill_result = get_skill_level(temp_game.skill_buffer,
						GAME);

					if (skill_result != ESCAPE_ERROR)
					{
						valid_result = valid_level(&temp_game.skill_buffer[2]);

						if (!valid_result)
							myerror(NONFATAL_PAUSE, SKILL_LEVEL_ERROR);
					}/* if */
				}while (skill_result != ESCAPE_ERROR && !valid_result);

				if (skill_result == ESCAPE_ERROR)
				{
					display_mouse_cursor();
					break;
				}

				temp_game.skill_level = atoi(&temp_game.skill_buffer[2]);
				shoot_time = times_list[temp_game.skill_level - 1];

				#ifdef MMDEMO
				if (self_status() == ON)
				{
					KEY_HANDLER(NULL);

					if (self_status() == OFF)
						break;
				}
				#endif

				/* get the user's name now */
				#ifdef MMDEMO	/* no defaults if self demo */
				if (self_status() == ON)
					temp_game.player_name[2] = '\0';
				#endif

				if (get_player_name(temp_game.player_name, GAME) == ESCAPE_ERROR)
					break;

				if (pause(2, 22, NO_HELP_TOPIC) != ESC)
				{
					win_error(close_window(*main_handle, PRESERVE));
					display_mouse_cursor();
					play_game(&temp_game, highs);
					game_stats = temp_game;	/* save in structure */
					win_error(open_window(&main_attribs, main_handle));
				}

				display_mouse_cursor();

				break;

			case '2':		/* exam or game progress report */
				/* ask user whether GAME or EXAM */
				mess_lines[0] = "(G)ame Progress Report";
				mess_lines[1] = "-or-";
				mess_lines[2] = "(E)xam Progress Report";
				mess_lines[3] = "";
				mess_lines[4] = "Make your selection or press ESCAPE to cancel.";

				set_help_topic(PROGRESS_INTRO_TOPIC);

				hide_mouse_cursor();
				win_error(win_message(mess_lines, 5, "", &key2, 8, WAIT,
					STRETCH));
				display_mouse_cursor();

				if (key2 == ESC)
				{
					unset_help_topic();
					break;	/* return to menu */
				}

				key2 = toupper(key2);

				if (key2 != 'E' && key2 != 'G')
				{
					unset_help_topic();
					break;   	/* return to menu */
				}

				if (key2 == 'G')	/* game progress report */
				{
					if (!game_count)	/* no games */
					{
						myerror(NONFATAL_PAUSE, NOGAMES_ERROR);
						unset_help_topic();
						break;	/* leave switch */
					}

					refresh = 1;

					read_result = read_one_high(highs, game_stats.skill_level);

					hide_mouse_cursor();

					change_active_title(GAME_PROGRESS);

					if (!game_stats.player_name[1])		/* no player name */
					{
						strcpy(&game_stats.player_name[2], "NONAME");
						progress_screen(highs, read_result, 0, &game_stats);
						game_stats.player_name[2] = '\0';
					}
					else		/* just do the report, don't worry about name */
						progress_screen(highs, read_result, 0, &game_stats);

					change_active_title(PROGRAM_NAME);
				}/* if */
				else		/* exam progress report */
				{
					if (!exam_count)	/* no exams */
					{
						myerror(NONFATAL_PAUSE, NOEXAMS_ERROR);
						unset_help_topic();
						break;	/* leave switch */
					}

					refresh = 1;

					hide_mouse_cursor();

					if (!exstats.taker_name[1])		/* no player name */
					{
						strcpy(&exstats.taker_name[2], "NONAME");
						exam_screen(&exstats);	/* display progress */
						exstats.taker_name[2] = '\0';
					}
					else		/* just do the report, don't worry about name */
						exam_screen(&exstats);	/* display progress */

					change_active_title(PROGRAM_NAME);
				}/* else */

				unset_help_topic();

				break;

			case '3':		/* see high scores */
				refresh = 1;
				hide_mouse_cursor();
				set_help_topic(HIGH_SCORES_TOPIC);
				see_highs(highs);
				unset_help_topic();
				break;

			case '5':		/* go to setup menu */
				refresh = 1;
				hide_mouse_cursor();
				do_setup_menu(*main_handle);
				break;

			case '4':		/* administer exam */
				refresh = 1;
				hide_mouse_cursor();
				change_active_title("MouseMatician Exam");
				set_help_topic(EXAM_TOPIC);
				do_exam(highs, main_handle);
				unset_help_topic();
				change_active_title(PROGRAM_NAME);	/* restore title */
				display_mouse_cursor();
				break;

			#ifdef MMDEMO
			case '6':		/* do self-running demo */
				if (!script_loaded_ok())		/* demo was not loaded at the start of the program */
				{
					myerror(NONFATAL_PAUSE, SCRIPT_LOAD_ERROR);
					break;
				}

				set_self_on();		/* turn demo on */

				break;
			#endif

			default:
				#ifdef MMDEMO
				if (key == TEXT_START_KEY)	/* ignore in demo */
					break;
				#endif
				myerror(NONFATAL_PAUSE, MENU_CHOICE_ERROR);
		}/* switch */

		menu_finish(&refresh, &key, &main_menu, &button_info, *main_handle);
	}/* while */
}/* do_main_menu() */