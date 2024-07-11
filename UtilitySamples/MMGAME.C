/*
	mmgame.c

	routines for mousematician game itself
*/

#include <string.h>
#include <bios.h>
#include <stdlib.h>
#include <graphics.h>
#include <dos.h>
#include <time.h>
#include <stdio.h>
#include "mmpack.h"
#include "mmstring.h"
#include "mmsleep.h"
#include "directs.h"
#include "keys.h"
#ifdef MMDEMO
	#include "mmdemo.h"
#endif


extern struct setup_type setup;
extern int game_count;
extern char misc_buffer[];


long screen_diag;
int explosion_reps;
int mode;			/* mode */
int driver;		/* driver */
int sound_flag = SOUND_OFF;	/* to start with */
int sound_ticks;
struct score_type *level_info;
clock_t curr_ticks;
int beat_freq;		/* frequency of heart beat */
int text_v_pos;
int hor_max;		/* max x coord */
int vert_max;		/* max y coord */
int seconds_left;	/* time left in game */
int shoot_time;		/* time for a move */
int target_height;		/* height of a target */
int target_width;		/* width of a target */
int stat_height;
int time_start;		/* x starting positions of time */
int score_start;		/* x starting positions of score */
int dbx_ofs;		/* amount inward for double targets */
int dby_ofs;
int target2_height;	/* height and width of drag box */
int target2_width;
int drag_ytoler;
int drag_xtoler;
void (*XORdirectangle)(int, int, int, int);
void (*ORdirectangle)(int, int, int, int);

struct score_type target_scores[LEVEL_MAX] = {
	{200, 300, 400, 100, 7, 10},	/* level 1 */
	{250, 350, 450, 150, 6, 9},
	{300, 400, 500, 200, 6, 9},
	{350, 450, 550, 250, 5, 8},
	{400, 500, 600, 300, 5, 8},
	{450, 550, 650, 350, 4, 7},
	{500, 600, 700, 400, 4, 7},
	{550, 650, 750, 450, 3, 6},
	{600, 700, 800, 500, 3, 6},
	{650, 750, 850, 550, 2, 5}  	/* level 10 */
};

int action_freqs[] = {
	DRAG_TARG_IDX, SPOIL_TARG_IDX, DOUBLE_TARG_IDX,
	DRAG_TARG_IDX, DOUBLE_TARG_IDX, SINGLE_TARG_IDX, DRAG_TARG_IDX,
	SINGLE_TARG_IDX, DRAG_TARG_IDX, DOUBLE_TARG_IDX
};

int times_list[LEVEL_MAX] = {
	ONE_TIME, TWO_TIME, THREE_TIME, FOUR_TIME, FIVE_TIME, SIX_TIME,
	SEVEN_TIME, EIGHT_TIME, NINE_TIME, TEN_TIME
};


void set_shoot_time(int value)
{
	shoot_time = value;
}/* set_shoot_time() */


void set_explode_reps(int value)
{
	explosion_reps = value;
}/* set_explode_reps() */


void set_drag_tol(int x, int y)
{
	drag_xtoler = x;
	drag_ytoler = y;
}/* set_drag_tol() */


void set_inner_offset(int x, int y)
{
	dbx_ofs = x;
	dby_ofs = y;
}/* set_inner_offset() */


void set_mode(int value)
{
	mode = value;
}/* set_mode() */


void set_driver(int value)
{
	driver = value;
}/* set_driver() */


int get_driver(void)
{
	return(driver);
}/* get_driver() */


void set_rect_funcs(int driver)
{
	switch(driver)
	{
		case HERCMONO:
			XORdirectangle = HERCXORdirectangle;
			ORdirectangle = HERCORdirectangle;
			break;

		case CGA:
			XORdirectangle = CGAXORdirectangle;
			ORdirectangle = CGAORdirectangle;
			break;

		case VGA:
		case EGA:
		case EGA64:
			XORdirectangle = VGAXORdirectangle;
			ORdirectangle = VGAORdirectangle;
			break;

		case EGAMONO:
		case MCGA:
			XORdirectangle = MCGAXORdirectangle;
			ORdirectangle = MCGAORdirectangle;
			break;

	}/* switch */
}/* set_rect_funcs() */


void get_input(int *key, struct button_info_type *b)
{
	if (bioskey(1))
		*key = KEY_HANDLER(b);
	else
		*key = ESC + 1;		/* so that loop won't quit */
}/* get_input() */


long newton_root(long ofwhat, int reps)
{
	static long unsigned seed, prev_seed, seed_sq;
	static int count;

	if (ofwhat == 0)
		return(0);

	if (ofwhat == 1)
		return(1);

	seed = ofwhat + 1;		/* play with this */
	count = 0;
	seed_sq = seed * seed;


	while (count < reps && seed_sq > ofwhat)
	{
		prev_seed = seed;

		seed = (seed >> 1) + ofwhat / (seed << 1);

		count++;

		seed_sq = seed * seed;
	}

	return(seed_sq > ofwhat ? seed : prev_seed);
}/* newton_root() */


void init_heart_beat(clock_t *beat_ticks)
{
	beat_freq = BEAT_FREQ;
	*beat_ticks = 0;
}/* init_heart_beat() */


void update_heart_beat(clock_t *beat_ticks)
{
	clock_t curr_ticks;


	curr_ticks = clock();

	if (setup.sound_val != SOUND_OFF && curr_ticks > *beat_ticks)
	{
		sound_ticks += (int)(curr_ticks - *beat_ticks);
		*beat_ticks = curr_ticks;

		if (sound_flag != SOUND_OFF)		/* sound is currently on */
		{
			if (sound_ticks > level_info->beat_duration)
			{
				sound_ticks = 0;
				sound_flag = SOUND_OFF;
				nosound();
			}/* if */
		}/* if */
		else
		{
			if (sound_ticks > level_info->beat_rest)
			{
				sound_ticks = 0;
				sound_flag = SOUND_ON;
				sound(beat_freq);
			}/* if */
		}/* else */
	}/* if */
}/* update_heart_beat() */


void explosion(int targ_type, int x, int y, int x2, int y2, clock_t *beat_ticks,
	int action)
{
	int count;
	int rcolor;


	hide_mouse_cursor();

	/* flash */
	for (count = 0; count < explosion_reps; count++)
	{
		update_heart_beat(beat_ticks);

		setbkcolor(setup.colors[EXPLOSION_ITEM_IDX]);
		sleep_ticks(1);
		setbkcolor(BLACK);		/* standard background color */
	}/* for */

	update_heart_beat(beat_ticks);

	/* first take care of erasing inner rectangle of DOUBLE, etc. */
	switch(action)
	{
		case SINGLE_TARG_IDX:
			rcolor = setup.colors[SINGLE_TARG_IDX];
			setcolor(rcolor);
			break;

		case DOUBLE_TARG_IDX:
			(*XORdirectangle)(x + dbx_ofs, y + dby_ofs,
				x + target_width - dbx_ofs, y + target_height - dby_ofs);
			rcolor = setup.colors[DOUBLE_TARG_IDX];
			setcolor(rcolor);
			break;

		case DRAG_TARG_IDX:
			(*XORdirectangle)(x2, y2, x2 + target2_width, y2 + target2_height);
			rcolor = setup.colors[DRAG_TARG_IDX];
			setcolor(rcolor);
			break;
	}/* switch */

	if (targ_type == SPOIL_TARG_IDX)	/* done if SPOILER */
	{
		display_mouse_cursor();
		return;
	}

	for (count = y; count < y + target_height; count += 4)
	{
		line(x, count, x + target_width, count);
		delay(5);
		setcolor(BLACK);	/* to erase */
		line(x, count, x + target_width, count);

		update_heart_beat(beat_ticks);

		/* get the sides */
		putpixel(x, count + 1, BLACK);
		putpixel(x + target_width, count + 1, BLACK);
		putpixel(x, count + 2, BLACK);
		putpixel(x + target_width, count + 2, BLACK);
		putpixel(x, count + 3, BLACK);
		putpixel(x + target_width, count + 3, BLACK);
	}/* for */

	setcolor(rcolor);
	line(x, y + target_height, x + target_width, y + target_height);

	/* erase the final line */
	for (count = x + target_width; count >= x; count--)
	{
		update_heart_beat(beat_ticks);
		putpixel(count, y + target_height, BLACK);
		delay(2);
	}

	display_mouse_cursor();
}/* explosion() */


void bump_score(struct game_type *g, long increment)
{
	g->score += increment;
}/* bump_score() */


void bump_time(signed int increment)
{
	seconds_left += increment;
}/* bump_time() */


int get_game_time(void)
{
	return(seconds_left);
}/* get_game_time() */


long get_game_score(struct game_type *g)
{
	return(g->score);
}/* get_game_score() */


void update_time(signed int increment)
{
	if (seconds_left > 0)
	{
		setcolor(BACK_COLOR);
		itoa(seconds_left, misc_buffer, 10);
		outtextxy(time_start, vert_max - stat_height, misc_buffer);
		outtextxy(time_start, vert_max - stat_height, misc_buffer);
		itoa(seconds_left += increment, misc_buffer, 10);
		setcolor(setup.colors[FONT_ITEM_IDX]);
		outtextxy(time_start, vert_max - stat_height, misc_buffer);
	}
}/* update_time() */


void update_score(struct game_type *g, long increment)
{
	char *str;

	hide_mouse_cursor();
	setcolor(BACK_COLOR);
	str = score_string(g->score);
	/* do I need to do this twice? */
	outtextxy(score_start, vert_max - stat_height, str);
	outtextxy(score_start, vert_max - stat_height, str);
	g->score += increment;
	setcolor(setup.colors[FONT_ITEM_IDX]);
	outtextxy(score_start, vert_max - stat_height, score_string(g->score));
	display_mouse_cursor();
}/* update_score() */



int new_move(int first_move, clock_t move_ticks, clock_t *time_ticks, clock_t *beat_ticks)
{
	curr_ticks = clock();

	update_heart_beat(beat_ticks);

	/* check seconds */
	if (curr_ticks - *time_ticks > TICKS_PER_SEC)
	{
		update_time(-1);
		*time_ticks = curr_ticks;	/* reset counter */
	}

	if (first_move)	/* the move is the first one of the game */
		return(1);
	else
		return(curr_ticks - move_ticks > shoot_time);
}/* new_move() */


void erase_move(int action, int xpos, int ypos, int x2pos, int y2pos)
{
	hide_mouse_cursor();

	switch(action)
	{
		case SINGLE_TARG_IDX:
			(*XORdirectangle)(xpos, ypos, xpos + target_width,
				ypos + target_height);
			break;

		case DOUBLE_TARG_IDX:
			(*XORdirectangle)(xpos, ypos, xpos + target_width, ypos + target_height);
			(*XORdirectangle)(xpos + dbx_ofs, ypos + dby_ofs, xpos + target_width -
				dbx_ofs, ypos + target_height - dby_ofs);
			break;

		case DRAG_TARG_IDX:
			(*XORdirectangle)(x2pos, y2pos, x2pos + target2_width,
				y2pos + target2_height);
			(*ORdirectangle)(xpos, ypos, xpos + target_width,
				ypos + target_height);
			(*XORdirectangle)(xpos, ypos, xpos + target_width,
				ypos + target_height);
			break;

		case SPOIL_TARG_IDX:
			setcolor(BLACK);
			rectangle(xpos, ypos, xpos + target_width, ypos + target_height);
			setlinestyle(SOLID_LINE, 0, NORM_WIDTH);
			break;

	}/* switch */

	display_mouse_cursor();
}/* erase_move() */


void draw_move(struct game_type *g, int action, int xpos, int ypos, int x2pos,
	int y2pos)
{
	hide_mouse_cursor();

	switch(action)
	{
		case SINGLE_TARG_IDX:
			set_rect_color(setup.colors[action]);
			(*XORdirectangle)(xpos, ypos, xpos + target_width, ypos + target_height);
			g->single_targs++;
			break;

		case DOUBLE_TARG_IDX:
			g->double_targs++;
			set_rect_color(setup.colors[action]);
			(*XORdirectangle)(xpos, ypos, xpos + target_width, ypos + target_height);
			(*XORdirectangle)(xpos + dbx_ofs, ypos + dby_ofs, xpos + target_width -
				dbx_ofs, ypos + target_height - dby_ofs);
			break;

		case DRAG_TARG_IDX:
			g->drag_targs++;
			set_rect_color(setup.colors[action]);
			(*XORdirectangle)(xpos, ypos, xpos + target_width, ypos + target_height);
			(*XORdirectangle)(x2pos, y2pos, x2pos + target2_width, y2pos + target2_height);
			break;

		case SPOIL_TARG_IDX:
			g->spoil_targs++;
			setlinestyle(DASHED_LINE, 0, THICK_WIDTH);
			setcolor(setup.colors[SPOIL_TARG_IDX]);
			rectangle(xpos, ypos, xpos + target_width, ypos + target_height);
			rectangle(xpos, ypos, xpos + target_width, ypos + target_height);
			break;
	}/* switch */

	display_mouse_cursor();
}/* draw_move() */


int in(int event, int x, int y, int m_y, int m_x)
{
	int x_offs, y_offs, height, width;

	switch(event)
	{
		case SPOIL_TARG_IDX:
		case SINGLE_TARG_IDX:
			height = target_height;
			width = target_width;
			x_offs = 0;
			y_offs = 0;
			break;

		case DOUBLE_TARG_IDX:
			height = target_height;
			width = target_width;
			x_offs = dbx_ofs;
			y_offs = dby_ofs;
			break;

		case DRAG_TARG_IDX:
			height = target2_height;
			width = target2_width;
			x_offs = 0;
			y_offs = 0;
			break;
	}/* switch */

	/* return 1 for in, 0 else */
	return(m_y > y + y_offs && m_y < y + height - y_offs &&
		m_x > x +x_offs && m_x < x + width - x_offs);
} /* in() */


void init_stats(struct high_type list[], int level, int read_result)
{
	static char level_string[LEVEL_STRING_LEN];
	int str1_start;
	int str2_start;
	int str3_start;
	int l1_start;
	int l2_start;


	text_v_pos = vert_max - stat_height;

	if (read_result == SUCCESS)		/* high scores were read */
		sprintf(level_string, "LEVEL %d HIGH SCORE: %s", level,
			score_string(list[level - 1].score));
	else
		sprintf(level_string, "LEVEL %d HIGH SCORE:   ", level);

	str1_start = INIT_SEP;	/* keep constant */
	str2_start = textwidth("   ") + textwidth(level_string) + SEC_SEP / 2 + INIT_SEP;
	str3_start = hor_max - textwidth("TIME:    ") - INIT_SEP;

	score_start = str2_start + textwidth("SCORE: ");
	time_start = str3_start + textwidth("TIME: ");

	setcolor(setup.colors[FONT_ITEM_IDX]);

	outtextxy(str1_start, text_v_pos, level_string);
	outtextxy(str2_start, text_v_pos, "SCORE: ");
	outtextxy(str3_start, text_v_pos, "TIME: ");

	l1_start = textwidth("   ") + textwidth(level_string) + INIT_SEP + SEC_SEP / 3;
	l2_start = str3_start - SEC_SEP / 5;

	setcolor(setup.colors[LINE_ITEM_IDX]);

	rectangle(0, text_v_pos, hor_max, vert_max);

	line(l1_start, text_v_pos, l1_start, vert_max);
	line(l2_start, text_v_pos, l2_start, vert_max);
}/* init_stats() */


/*
	returns:
		0 if drag was bad in both directions
		1 if vertical part is good
		2 if horizontal part is good
		3 if both are good
*/
int good_drag(int x2pos, int y2pos, int mxpos, int mypos, int mxprev, int myprev)
{
	int mdiff;
	int vresult = 0, hresult = 0;		/* both directions were bad */


	mdiff = mypos - myprev;

	/* check for down */
	if (mdiff > drag_ytoler && y2pos + mdiff + target2_height <
		text_v_pos - 2)
		vresult = 1;		/* down is good */
	else if (mdiff < -drag_ytoler && y2pos + mdiff >= 0)
		vresult = 1;		/* up is good */

	mdiff = mxpos - mxprev;

	if (mdiff > drag_xtoler && x2pos + mdiff + target2_width < hor_max)
		hresult = 2;		/* right is good */
	else if (mdiff < -drag_xtoler && x2pos + mdiff >= 0)
		hresult = 2;		/* left is good */

	return(hresult + vresult);
}/* good_drag() */


int do_move(struct game_type *g, int *first_move, clock_t *beat_ticks)
{
	static int score_flag;		/* flag for updating score */
	static clock_t time_ticks;
	static clock_t move_ticks;
	static int score_updated;
	static int move_done;	/* flag to say whether a move is done or not */
	static int double_click;	/* check for double click */
	static int xpos, ypos, x2pos, y2pos;	/* target positions */
	static int action;
	static int dx, dy;
	static int first_pressed;	/* used with DOUBLE targets */
	static int mxpos, mypos, mxprev, myprev;	/* for dragging */
	static long curr_score;
	static clock_t hit_ticks;
	int key;
	struct button_info_type button_info;
	long move_diag, tent_score;
	int drag_result;		/* for good_drag() */
	static int new_result;
	static int mouse_cursor_hidden;	/* for drag targets */


	get_input(&key, &button_info);

	button_status(&setup, &button_info, LEFT);

	if (key == ESC	|| right_button_presses(&setup))
		return(0);

	if (new_result || new_move(*first_move, move_ticks, &time_ticks, beat_ticks))
	{
		new_result = 0;

		mouse_cursor_hidden = 0;

		move_done = 0;
		first_pressed = 0;		/* no button presses */

		/* clear previous move from the screen if there is one */
		if (!(*first_move) && !score_flag && !double_click)
			erase_move(action, xpos, ypos, x2pos, y2pos);

		/* reset move variables */
		*first_move = 0;
		double_click = 0;
		score_flag = 0;	/* no score to start */
		score_updated = 0;
		move_ticks = curr_ticks;
		action = action_freqs[random(DISTRIBUTION)];	/* choose a target */
		xpos = random(hor_max - target_width);
		ypos = random(vert_max - target_height - stat_height - 1);

		if (action == DRAG_TARG_IDX)	/* drag target */
		{
			x2pos = random(hor_max - target_width);
			y2pos = random(vert_max - target_height - stat_height - 1);

			/* don't want boxes to intersect */
			if (!((y2pos + target2_height < ypos || y2pos > ypos + target_height)
			&& (x2pos + target2_width < xpos || x2pos > xpos + target_width)))
			{
				if (xpos > (hor_max >> 1))
					x2pos = 1;
				else
					x2pos = hor_max - target_width - 1;
			}/* if */

			dx = abs(xpos - x2pos);
			dy = abs(ypos - y2pos);
		}/* if */
		else
			x2pos = y2pos = 0;	/* don't need them, but initialize them */

		/* draw the new move */
		draw_move(g, action, xpos, ypos, x2pos, y2pos);
	}/* if */

	if (!move_done)
	{
	switch(action)
	{
		case SINGLE_TARG_IDX:
			if (button_info.num_presses && in(SINGLE_TARG_IDX, xpos,
				ypos, button_info.vert, button_info.horz))
			{
				if (!score_flag)		/* no score yet */
				{
					curr_score = (long)level_info->single_max -
						(long)(curr_ticks - move_ticks);
					explosion(SINGLE_TARG_IDX, xpos, ypos, x2pos, y2pos,
						beat_ticks, action);
					update_score(g, curr_score);
					hit_ticks = clock();
					g->single_hits++;
					beat_freq += BEAT_INC;
					score_flag = 1;
				}
				else if (clock() - hit_ticks < DBCLICK_TIME - 2) /* */
				{  	/* user screwed up and double-clicked */
					g->single_hits--;
					beat_freq -= BEAT_INC;
					double_click = 1;	/* woops */
					update_score(g, -1 * curr_score);
					move_done = 1;
				}
				else
					move_done = 1;
			}/* if */

			break;

		case DOUBLE_TARG_IDX:
			if (button_info.num_presses && in(DOUBLE_TARG_IDX, xpos,
				ypos, button_info.vert, button_info.horz))
			{
				if (!score_flag)	/* no score yet */
				{
					if (first_pressed &&	/* one left press already */
						clock() - hit_ticks < DBCLICK_TIME)
					{
						update_heart_beat(beat_ticks);	/* try it */
						explosion(DOUBLE_TARG_IDX, xpos, ypos, x2pos,
							y2pos, beat_ticks, action);
						curr_score = (long)level_info->double_max -
							(long)(curr_ticks - move_ticks);
						update_score(g, curr_score);
						g->double_hits++;
						beat_freq += BEAT_INC;
						score_flag = 1;
						move_done = 1;
					}
					else
					{
						first_pressed = 1;
						hit_ticks = clock();
					}/* else */
				}
			}/* if */

			break;

		case DRAG_TARG_IDX:
			/* not really the way I wanted to do it */
			while (!move_done && seconds_left > 0 &&
				!(new_result = new_move(*first_move, move_ticks,
					&time_ticks, beat_ticks)))
			{
				mypos = get_vert_pos();	/* get current mouse position */
				mxpos = get_horz_pos();

				if (bioskey(1) && KEY_HANDLER(NULL) == ESC)
						return(0);

				if (right_button_presses(&setup))
					return(0);

			while(!move_done && left_button_down(&setup) && in(DRAG_TARG_IDX, x2pos, y2pos, mypos, mxpos) &&
				seconds_left > 0 && !(new_result = new_move(*first_move, move_ticks, &time_ticks, beat_ticks)))
			{

				if (!mouse_cursor_hidden)
				{
					hide_mouse_cursor();
					mouse_cursor_hidden = 1;
				}

				myprev = mypos;
				mxprev = mxpos;
				mypos = get_vert_pos();
				mxpos = get_horz_pos();

				/* redraw the rectangles */
				if ((drag_result = good_drag(x2pos, y2pos, mxpos, mypos, mxprev, myprev)) != 0)
				{
					WAIT_VERT_RET;

					/* erase small rectangle */
					(*XORdirectangle)(x2pos, y2pos, x2pos + target2_width,
						y2pos + target2_height);
					/* redraw big one */
					(*ORdirectangle)(xpos, ypos, xpos + target_width,
						ypos + target_height);

					switch(drag_result)
					{
						case 1:	/* vertical drag */
							y2pos += (mypos - myprev);
							break;
						case 2:	/* horizontal drag */
							x2pos += (mxpos - mxprev);
							break;
						default:	/* 0 is excluded by the if */
							x2pos += (mxpos - mxprev);
							y2pos += (mypos - myprev);
					}/* switch */

					/* draw new second rectangle */
					(*ORdirectangle)(x2pos, y2pos, x2pos + target2_width,
						y2pos + target2_height);

				}/* if */
			}/* while */

			if (!left_button_down(&setup) && x2pos > xpos && y2pos > ypos &&
				x2pos + target2_width <
				xpos + target_width &&
				y2pos + target2_height <
				ypos + target_height)
			{
				/* the other box is in */
				g->drag_hits++;
				move_diag = newton_root((long)dx * dx + (long)dy * dy,
					ROOT_ITS);
				tent_score = (long)level_info->drag_min +
					(long)level_info->drag_bonus * move_diag / screen_diag -
					(long)(curr_ticks - move_ticks);
				explosion(DRAG_TARG_IDX, xpos, ypos, x2pos, y2pos, beat_ticks,
					action);
				update_score(g, tent_score);
				beat_freq += BEAT_INC;
				score_flag = 1;
				move_done = 1;		/* done with move */
			}/* if */

			if (mouse_cursor_hidden)
			{
				display_mouse_cursor();
				mouse_cursor_hidden = 0;
			}

			}/* while */

			break;

		case SPOIL_TARG_IDX:
			if (button_info.num_presses && in(SPOIL_TARG_IDX, xpos,
				ypos, button_info.vert, button_info.horz))
			{
				g->spoil_hits++;
				g->missed_score += (g->score >> 1);
				explosion(SPOIL_TARG_IDX, xpos, ypos, x2pos, y2pos,
					beat_ticks, action);
				update_score(g, -1 * (g->score >> 1));
				score_flag = 0;
				if (g->score)	/* only change freq if there is a score */
					beat_freq = (beat_freq < BEAT_FREQ ? BEAT_FREQ >> 1 :
					beat_freq >> 1);
				move_done = 1;
			}/* if */

			break;

	}/* switch */
	}/* if */

	/* if right button or escape then quit */
	return(key == ESC ? 0 : 1);
}/* do_move() */


void init_game_variables(struct game_type *g)
{
	g->spoil_targs = g->spoil_hits = g->single_targs =
	g->single_hits = g->double_targs = g->double_hits =
	g->drag_targs = g->drag_hits = g->total_targs =
	g->total_hits = 0;
	g->score = g->missed_score = 0;	/* initialize scores */
	seconds_left = GAME_TIME + 1;		/* initialize game clock */
}/* init_game_variables() */


void init_game(struct game_type *g, struct high_type highs[], int read_result,
	int *first_move, clock_t *beat_ticks)
{
	level_info = &target_scores[g->skill_level - 1];

	init_heart_beat(beat_ticks);

	initgraph(&driver, &mode, "");
	setbkcolor(BLACK);
	cleardevice();		/* clear screen */
	settextstyle(SANS_SERIF_FONT, HORIZ_DIR, 1);
	setlinestyle(SOLID_LINE, 0, NORM_WIDTH);

	if (driver == HERCMONO)		/* Hercules preliminaries */
	{
		if (peekb(0x40, 0x62) == 0)	/* first page */
			pokeb(0x40, 0x49, 6);
		else
			pokeb(0x40, 0x49, 5);

		reset_mouse();
	}

	/* get information about current mode */
	hor_max = getmaxx();
	vert_max = getmaxy();
		/* get game drawing info, experimental */
	target_height = vert_max / VERT_SHRINK;
	target_width = hor_max / HOR_SHRINK;
	target2_height = vert_max / (VERT_SHRINK + 3);
	target2_width = hor_max / (HOR_SHRINK + 4);
	stat_height = 25;

	setlinestyle(SOLID_LINE, 0, NORM_WIDTH);
	init_stats(highs, g->skill_level, read_result);

	set_y_max_min(vert_max - stat_height - 5, 0);
	set_x_max_min(hor_max - 6, 0);

	/* calculation for screen diagonal */
	screen_diag = newton_root((long)(hor_max - target_width) * (hor_max - target_width) +
		(long)(vert_max - target_height - stat_height) * (vert_max - target_height - stat_height), ROOT_ITS);

	display_mouse_cursor();

	init_game_variables(g);

	update_time(0);				/* write initial time on screen */
	update_score(g, 0);				/* write initial score on screen */

	*first_move = 1;
}/* init_game() */


void play_game(struct game_type *g, struct high_type highs[])
{
	int read_result;
	int first_move;		/* flag for first move */
	clock_t beat_ticks;


	randomize();

	set_help_status(OFF);	/* no help for a while */

	read_result = read_one_high(highs, g->skill_level);

	#ifdef MMDEMO
	if (self_status() == ON)		/* guided tour is running */
	{
		text_play_game(g, highs);
		get_date_time(g->today, g->now);
		set_help_status(ON);
		return;
	}
	#endif

	init_game(g, highs, read_result, &first_move, &beat_ticks);

	/* REMOVE : for testing on PS/2 model 30 */
/*	while (getch() != ESC)
	{
		explosion(SINGLE_TARG_IDX, 1, 1, 1, 1, &beat_ticks, SINGLE_TARG_IDX);
		sleep(2);
		explosion(DOUBLE_TARG_IDX, 1, 1, 1, 1, &beat_ticks, DOUBLE_TARG_IDX);
		sleep(2);
		explosion(DRAG_TARG_IDX, 1, 1, 1, 1, &beat_ticks, DRAG_TARG_IDX);
		sleep(2);
		explosion(SPOIL_TARG_IDX, 1, 1, 1, 1, &beat_ticks, SPOIL_TARG_IDX);
	}
	closegraph();
	return;*/
	/* REMOVE : for testing on PS/2 model 30 */

	/* main loop */
	while (seconds_left > 0 && do_move(g, &first_move, &beat_ticks) != 0)
		;

	if (!game_count)
		game_count = 1;	/* at least one game was played */

	if (setup.sound_val == SOUND_ON)
		nosound();	/* shut off speaker */

	delay(END_DELAY);			/* wait for last minute score updates */

	closegraph();

	get_date_time(g->today, g->now);

	/* update the high score file if it needs to be */
	#ifdef MOUSEMAT
	update_highs(g->score, g->skill_level, &g->player_name[2], g->today,
		g->now);
	#endif
	#ifdef MMDEMO	/* update highs in RAM */
	if (g->score > highs[g->skill_level - 1].score)
	{
		highs[g->skill_level - 1].score = g->score;
		strcpy(highs[g->skill_level - 1].score_date, g->today);
		strcpy(highs[g->skill_level - 1].score_time, g->now);
		strcpy(highs[g->skill_level - 1].name, &g->player_name[2]);
	}
	#endif

	//fcloseall();

	reset_mouse();		/* restore default mouse settings */

	set_help_status(ON);	/* restore help system */
}/* play_game() */
