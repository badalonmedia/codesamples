/*
	MMKHAND.C

*/

#include <stddef.h>
#include <bios.h>
#include <stdio.h>
#include <time.h>
#include "mmpack.h"
#include "keys.h"
#ifdef MMDEMO
	#include "mmdemo.h"
#endif


extern struct setup_type setup;


int wait_inert(struct button_info_type *but)
{
	int result;

	while ((result = KEY_HANDLER(but)) == INERT_KEY)
		;

	return(result);
}/* wait_inert() */


#ifdef MMDEMO
/*
	demo_key_handler()

	This function is a custom key handler which can replace the getch()
	function.  It allows other processing to go on while the program waits
	for a keypress.  Now responds to left and right mouse button pressses.

	for use in DEMO
*/
int demo_key_handler(struct button_info_type *but)
{
	static int key;
	int lbp = 0, rbp = 0;		/* presses */
	static clock_t num_ticks;
	static int time_out;
	int curr_count;
	int result;

	if (self_status() == ON)		/* guided tour is on */
	{
		curr_count = get_self_count();

		/* only wait if not the first element and last move was not text */
		if (curr_count > 0 && key != TEXT_START_KEY)
		{
			result = demo_wait((clock_t) num_ticks);

			if (result == 1)
			{
				show_last_screen();
				set_self_off();
				return(INERT_KEY);
			}
		}/* if */

		key = next_demo_key(curr_count);
		num_ticks = next_demo_ticks(curr_count);

		if (key == TEXT_START_KEY)	/* text window */
		{
			hide_text_cursor();
			if (display_text(next_text_elt(get_text_count()),
				next_demo_elt(curr_count)) == 1)
			{
				show_last_screen();
				set_self_off();
				return(INERT_KEY);
			}/* if */

			set_text_count(get_text_count() + 1);
		}
		else
			display_text_cursor();

		set_self_count(curr_count + 1);

		if (last_script_elt(get_self_count()))
			set_self_off();	/* done */

		return(key == TEXT_START_KEY ? INERT_KEY : key);
	}/* if */

	/* do any maintenance stuff here! */

	if (get_mouse() == ON)
	{
		while (!bioskey(1) &&
			!(lbp = left_button_presses(&setup)) &&
			!(rbp = right_button_presses(&setup)))
		{
			/* check if time is exceeded but not during guided tour */
			if (!time_out && self_status() != ON && get_program_ticks() > DEMO_TIME)
			{
				time_out = 1;
				myerror(FATAL, DEMO_TIME_ERROR);
			}
		}
	}
	else
		while (!bioskey(1))
		{
			/* check if time is exceeded but not during guided tour */
			if (!time_out && self_status() != ON && get_program_ticks() > DEMO_TIME)
			{
				time_out = 1;
				myerror(FATAL, DEMO_TIME_ERROR);
			}
		}/* while */

	if (but != NULL)
	{
		but->vert = -1;
		but->horz = -1;	/* assume to start that position does not matter */
	}

	if (get_mouse() == ON && rbp)		/* right button was pressed */
		key = ESC;
	else if (get_mouse() == ON && lbp)	/* left button was pressed */
	{
		key = ENTER;
		if (but != NULL)
			button_status(&setup, but, LEFT);
	}
	else			/* key was pressed */
	{
		key = bioskey(0);

		if (key & 0xFF)	/* if key was a character then kill upper byte */
			key &= 0xFF;

		check_help(&key);		/* check if F1 was pressed */
	}

	return(key);
}/* demo_key_handler() */
#endif


#ifdef MOUSEMAT
/*
	prog_key_handler()

	This function is a custom key handler which can replace the getch()
	function.  It allows other processing to go on while the program waits
	for a keypress.  Now responds to left and right mouse button pressses.

	used in programs other than DEMO
*/
int prog_key_handler(struct button_info_type *but)
{
	static int key;
	int lbp = 0, rbp = 0;		/* presses */


	/* do any maintenance stuff here! */

	if (get_mouse() == ON)
	{
		while (!bioskey(1) &&
			!(lbp = left_button_presses(&setup)) &&
			!(rbp = right_button_presses(&setup)))
			;
	}
	else
		while (!bioskey(1))
			;

	if (but != NULL)
	{
		but->vert = -1;
		but->horz = -1;	/* assume to start that position does not matter */
	}

	if (get_mouse() == ON && rbp)		/* right button was pressed */
		key = ESC;
	else if (get_mouse() == ON && lbp)	/* left button was pressed */
	{
		key = ENTER;
		if (but != NULL)
			button_status(&setup, but, LEFT);
	}
	else			/* key was pressed */
	{
		key = bioskey(0);

		if (key & 0xFF)	/* if key was a character then kill upper byte */
			key &= 0xFF;

		check_help(&key);		/* check if F1 was pressed */
	}

	return(key);
}/* prog_key_handler() */
#endif


#ifdef INSTALL
/*
	prog_key_handler()

	This function is a custom key handler which can replace the getch()
	function.  It allows other processing to go on while the program waits
	for a keypress.  Now responds to left and right mouse button pressses.

	used in programs other than DEMO
*/
int prog_key_handler(struct button_info_type *but)
{
	static int key;
	int lbp = 0, rbp = 0;		/* presses */


	/* do any maintenance stuff here! */

	if (get_mouse() == ON)
	{
		while (!bioskey(1) &&
			!(lbp = left_button_presses(&setup)) &&
			!(rbp = right_button_presses(&setup)))
			;
	}
	else
		while (!bioskey(1))
			;

	if (but != NULL)
	{
		but->vert = -1;
		but->horz = -1;	/* assume to start that position does not matter */
	}

	if (get_mouse() == ON && rbp)		/* right button was pressed */
		key = ESC;
	else if (get_mouse() == ON && lbp)	/* left button was pressed */
	{
		key = ENTER;
		if (but != NULL)
			button_status(&setup, but, LEFT);
	}
	else			/* key was pressed */
	{
		key = bioskey(0);

		if (key & 0xFF)	/* if key was a character then kill upper byte */
			key &= 0xFF;

		check_help(&key);		/* check if F1 was pressed */
	}

	return(key);
}/* prog_key_handler() */
#endif
