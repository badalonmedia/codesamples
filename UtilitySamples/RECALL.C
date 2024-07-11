/*
	RECALL.C	
*/

#include <string.h>
#include <graphics.h>
#include <stdlib.h>
#include <conio.h>
#include <ctype.h>
#include <dos.h>
#include <stdio.h>
#include <time.h>
#include "recall.h"


int num_colors;
int num_fills;
int num_cols;
int num_rows;
int num_cards;		/* number of cards remaining */
int num_ids;
int num_occurs;
int card_width;
int card_height;

struct card_type cards[MAX_ROWS][MAX_COLS];

int fill_styles[] = {
	SOLID_FILL,	// OK
	XHATCH_FILL,	// OK
	LINE_FILL,	// OK
	BKSLASH_FILL,	// OK
	CLOSE_DOT_FILL	// OK
};

int colors[] = {
	EGA_LIGHTGREEN,
	EGA_LIGHTGRAY,
	EGA_LIGHTRED,
	EGA_LIGHTMAGENTA,
	EGA_LIGHTCYAN
};

int midx, midy;
int xmax, ymax;
int ids[MAX_IDS];	/* better way to get this number? */
int seconds_left;
int init_secs;
struct index_type result = {-1, -1};
char misc_buffer[80];
char *string1 = "Result Software & Consulting presents...";
char *string2 = "Ronnie-Recall";
char *string3 = "I love you Ronnie.";
char *string4 = "Copyright 1994. All rights reserved.";
char *string5 = "-Any key to continue-";


/*
char checkerboard[] = {
  0x4A,   /* 1001010  =  Û Û Û Û   */
  0x55,   /* 01010101  =   Û Û Û Û  */
  0x4A,   /* 1001010  =  Û Û Û Û   */
  0x55,   /* 01010101  =   Û Û Û Û  */
  0x4A,   /* 1001010  =  Û Û Û Û   */
  0x55,   /* 01010101  =   Û Û Û Û  */
  0x4A,   /* 1001010  =  Û Û Û Û   */
  0x55    /* 01010101  =   Û Û Û Û  */
};
*/

char *time_string(int value)
{
	sprintf(misc_buffer, "%d:%02d", value / SECS_PER_MINUTE,
		value % SECS_PER_MINUTE);

	return(misc_buffer);
}/* time_string() */


int update_time(void)
{
	static clock_t ticks;

	if (clock() - ticks > TICKS_PER_SEC)
	{
		setcolor(BACK_COLOR);
		//WAIT_VERT_RET;
		outtextxy(midx, TIME_Y, time_string(seconds_left));
		seconds_left--;
		ticks = clock();
		setcolor(TIME_COLOR);
		//WAIT_VERT_RET;
		outtextxy(midx, TIME_Y, time_string(seconds_left));
	}

	return(seconds_left);
}/* update_time() */


void sleep_ticks(clock_t ticks, int status)
{
	clock_t curr;

	curr = clock();

	while (clock() - curr <= ticks)	/* specialized */
		if (status != NO_UPDATE)
		{
			update_time();
			update_pixels();
		}
}/* sleep_ticks() */


void beep(void)
{
	sound(BEEP_FREQ);
	sleep_ticks(BEEP_DELAY, !NO_UPDATE);
	nosound();
}/* beep() */


void hide_mouse_cursor(void)
{
	union REGS regs;

	regs.x.ax = 2;
	int86(MOUSE_INT, &regs, &regs);
}/* hide_mouse_cursor() */


void display_mouse_cursor(void)
{
	union REGS regs;

	regs.x.ax = 1;
	int86(MOUSE_INT, &regs, &regs);
}/* display_mouse_cursor() */


void set_mouse_pos(int xpos, int ypos)
{
	union REGS regs;

	regs.x.ax = 4;
	regs.x.cx = xpos;
	regs.x.dx = ypos;
	int86(MOUSE_INT, &regs, &regs);
}/* set_mouse_pos() */


/*
	to be called at the beginning of a program.

	return 0:	mouse driver not found
	return 1: mouse not found
	return 2: both found and reset
*/
int master_mouse_reset(void)
{
	union REGS iReg, oReg;
	long vector;
	unsigned char first_byte;
	void (interrupt far *int_handler)();


	/* determine mouse driver interrupt address */
	/* get interrupt vector and first instruction of interrupt */
	int_handler = _dos_getvect(MOUSE_INT);
	first_byte = * (unsigned char far *) int_handler;
	vector = (long) int_handler;

	/* vector should not 0 and first instruction should not be iret */
	if (vector == 0 || first_byte == 0xCF)
		return(0);    /* mouse driver not found */

	/* mouse reset and status, call reset_mouse() basically */
	iReg.x.ax = 0;
	int86(MOUSE_INT, &iReg, &oReg);

	if (oReg.x.ax == 0)
		return(1);	/* mouse not found */

	/* both were found and reset */
	return(2);	/* success */
}/* master_mouse_reset() */


void left_button_status(struct button_info_type *b)
{
	union REGS regs;


	regs.x.ax = 5;
	regs.x.bx = 0;
	int86(MOUSE_INT, &regs, &regs);
	b->num_presses = regs.x.bx;
	b->horz = regs.x.cx;
	b->vert = regs.x.dx;
}/* left_button_status() */


void init_ids(void)
{
	int count;

	for (count = 0; count < MAX_IDS; count++)
		ids[count] = 0;
}/* init_ids() */


int check_ids(void)
{
	int count;
	int result = 1;

	for (count = 0; count < num_ids; count++)
		if (ids[count] != num_occurs)
			result = 0;

	return(result);
}/* check_ids() */


void init_cards(void)
{
	int row, col;
	int tent_id;
	int color;
	int fill_style;
	int done;	/* flag */
	struct card_type *c;


	for (row = 0; row < num_rows; row++)
	{
		for (col = 0; col < num_cols; col++)
		{
			c = &cards[row][col];
			c->x1 = (col + 1) * WIDTH_SPACE + col * card_width;
			c->y1 = (row + 1) * HEIGHT_SPACE + row * card_height;
			c->drawn = c->removed = 0;

			done = 0;

			while (!done)
			{
				color = random(num_colors);
				fill_style = random(num_fills);
				tent_id = color * 10 + fill_style;	/* 0 to 44 */

				if (ids[tent_id] < num_occurs)
				{
					c->color = colors[color];
					c->fill_style = fill_styles[fill_style];
					c->id = tent_id;
					ids[tent_id]++;
					done = 1;
				}/* if */
			}/* while */
		}/* for */
	}/* for */
}/* init_cards() */


void init_screen(void)
{
	int row, col;
	struct card_type *c;


	setfillstyle(SOLID_FILL, UNPOINTER_COLOR);
	setcolor(UNPOINTER_COLOR);

	for (row = 0; row < num_rows; row++)
	{
		for (col = 0; col < num_cols; col++)
		{
			c = &cards[row][col];
			bar(c->x1, c->y1, c->x1 + card_width, c->y1 + card_height);
		}
	}/* for */
}/* init_screen() */


void undraw_card(struct card_type *c)
{
	c->drawn = 0;
	hide_mouse_cursor();
	setcolor(UNPOINTER_COLOR);
	setfillstyle(SOLID_FILL, UNPOINTER_COLOR);
	bar(c->x1, c->y1, c->x1 + card_width, c->y1 + card_height);
	display_mouse_cursor();
}/* undraw_card() */


void draw_card(struct card_type *c)
{
	hide_mouse_cursor();
	c->drawn = 1;		/* card is now drawn */
	setcolor(c->color);
	setfillstyle(c->fill_style, c->color);
//	if (c->fill_style == USER_FILL)
//		setfillpattern(checkerboard, c->color);
	bar(c->x1, c->y1, c->x1 + card_width, c->y1 + card_height);
	display_mouse_cursor();
}/* draw_card() */


void undraw_pair(struct card_type *c, struct card_type *prev)
{
	undraw_card(c);
	undraw_card(prev);
}/* undraw_pair() */


void remove_pair(struct card_type *c, struct card_type *prev)
{
	c->removed = 1;
	prev->removed = 1;

	hide_mouse_cursor();
	setcolor(BACK_COLOR);
	setfillstyle(SOLID_FILL, BACK_COLOR);
	bar(c->x1, c->y1, c->x1 + card_width, c->y1 + card_height);

	setcolor(BACK_COLOR);
	setfillstyle(SOLID_FILL, BACK_COLOR);
	bar(prev->x1, prev->y1, prev->x1 + card_width, prev->y1 + card_height);
	display_mouse_cursor();
}/* remove_pair() */


/* for debugging */
void draw_all_cards(void)
{
	int row, col;

	for (row = 0; row < num_rows; row++)
		for (col = 0; col < num_cols; col++)
			draw_card(&cards[row][col]);
}/* draw_all_cards() */


int in(int horz, int vert)
{
	register int count;

	result.column = -1;
	result.row = -1;

	for (count = 0; count < num_cols; count++)
		if (horz + 1 > count * card_width + (count + 1) * WIDTH_SPACE
			&& horz - 1 < (count + 1) * card_width + (count + 1) * WIDTH_SPACE)
			result.column = count;

	if (result.column < 0)
		return(0);

	for (count = 0; count < num_rows; count++)
		if (vert + 1 > count * card_height + (count + 1) * HEIGHT_SPACE
			&& vert - 1 < (count + 1) * card_height + (count + 1) * HEIGHT_SPACE)
			result.row = count;

	if (result.row < 0)
		return(0);

	return(1);
}/* in() */


/* only visual */
void explode(void)
{
	int count;

	for (count = 0; count < EXPLODE_REPS; count++)
	{
		setbkcolor(EXPLODE_COLOR);
		sleep_ticks(EXPLODE_DELAY, !NO_UPDATE);
		setbkcolor(BACK_COLOR);
	}
}/* explode() */


void shuffle_cards(int status)
{
	int count;
	int row, col;
	int color;
	struct card_type *c;


	for (count = 0; count < SHUFFLE_REPS; count++)
	{
		color = colors[random(num_colors)];
		if (color == UNPOINTER_COLOR)
			color = EGA_LIGHTRED;
		setcolor(color);
		setfillstyle(SOLID_FILL, color);
		row = random(num_rows);
		col = random(num_cols);
		c = &cards[row][col];

		if (status != NO_SOUND)
			sound(MIN_FREQ + random(MAX_FREQ_INC) + 1);

		bar(c->x1, c->y1, c->x1 + card_width, c->y1 + card_height);

		delay(SHUFFLE_DELAY);
	}/* for */

	nosound();
}/* shuffle_cards() */


void init_time(void)
{
	setcolor(TIME_COLOR);
	outtextxy(midx, TIME_Y, time_string(init_secs));
	seconds_left = init_secs;
}/* init_time() */


void do_help(void)
{
	cprintf("\n\rRun the program as follows:");
	cprintf("\n\r\n\r   RECALL <name> <minutes> <level>");
	cprintf("\n\r\n\rwhere <name> is your name (one word).");
	cprintf("\n\r<minutes> is between 1 and 10 and is");
	cprintf("\n\rthe number of minutes in the game.");
	cprintf("\n\r<level> is between 1 and 4 and is the game level.");

	cprintf("\n\r\n\rAny <minutes> outside this range is converted to 5.");
	cprintf("\n\rAny <level> outside this range is converted to 3.");
}/* do_help() */


void do_title(void)
{
	settextjustify(CENTER_TEXT, CENTER_TEXT);

	settextstyle(TRIPLEX_FONT, HORIZ_DIR, 2);
	setcolor(EGA_MAGENTA);
	outtextxy(midx, midy / 4, string1);

	settextstyle(TRIPLEX_FONT, HORIZ_DIR, 9);
	setcolor(EGA_BLUE);
	outtextxy(midx, midy / 2, string2);

	settextstyle(TRIPLEX_FONT, HORIZ_DIR, 5);
	setcolor(EGA_BLUE);
	outtextxy(midx, midy, string3);

	settextstyle(TRIPLEX_FONT, HORIZ_DIR, 3);
	setcolor(EGA_MAGENTA);
	outtextxy(midx, 3 * midy / 2, string4);

	settextstyle(TRIPLEX_FONT, HORIZ_DIR, 4);
	setcolor(EGA_GREEN);
	outtextxy(midx, ymax - textheight(string5), string5);

	if (getch() == ESC)
	{
		closegraph();
		exit(EXIT_FAILURE);
	}
}/* do_title() */


void update_log(char *name, int guesses, int min_left, int sec_left,
	int level)
{
	static char time_buffer[9];
	static char date_buffer[9];

	cprintf("\n\rUpdating RECALL.LOG...");

	/* name, guesses, time taken, time done, date done */
	printf("\n%s   %d   %d   %d:%02d   %s   %s",
		name,
		level,
		guesses,
		min_left,
		sec_left,
		_strtime(time_buffer),
		_strdate(date_buffer));
}/* update_log() */


void game_setup(int level)
{
	switch(level)
	{
		case 1:
			num_cols = 6;
			num_rows = 3;
			num_colors = 3;
			num_fills = 3;
			num_occurs = 2;
			num_ids = 22;
			card_width = 95;
			card_height = 130;
			break;

		case 2:
			num_cols = 8;
			num_rows = 4;
			num_colors = 4;
			num_fills = 4;
			num_occurs = 2;
			num_ids = 33;
			card_width = 69;
			card_height = 92;
			break;

		case 3:
			num_cols = 10;
			num_rows = 5;
			num_colors = 5;
			num_fills = 5;
			num_occurs = 2;
			num_ids = 44;
			card_width = 52;
			card_height = 72;
			break;

		case 4:
			num_cols = 10;
			num_rows = 10;
			num_colors = 5;
			num_fills = 5;
			num_occurs = 4;
			num_ids = 44;
			card_width = 52;
			card_height = 31;
			break;
	}/* switch */

	num_cards = num_cols * num_rows;
}/* game_setup() */


int main(int argc, char *argv[])
{
	int driver, mode;
	int chosen = 0;		/* cards chosen so far during one move */
	int key;
	struct card_type *c, *prev;
	struct button_info_type binfo;
	int time_level;
	int skill_level;
	int guesses = 0;
	int min_left, sec_left;	/* for display at end */


	clrscr();

	if (argc == 4)		/* name first, time second, level third */
	{
  //		if (strlen(argv[1]) > MAX_NAME_LENGTH)
    //			*(argv[1] + MAX_NAME_LENGTH) = '\0';

		strupr(argv[1]);

		time_level = atoi(argv[2]);
		skill_level = atoi(argv[3]);

		if (time_level >= MIN_MINUTES && time_level <= MAX_MINUTES)
			init_secs = time_level * 60 + 1;
		else
			init_secs = DEFAULT_SECS;

		if (skill_level < MIN_SKILL || skill_level > MAX_SKILL)
			skill_level = DEFAULT_SKILL;	/* default */

		game_setup(skill_level);
	}
	else
	{
		do_help();
		exit(EXIT_FAILURE);
	}

	registerbgidriver(EGAVGA_driver);
	registerbgifont(triplex_font);

	randomize();

	cprintf("\n\rResetting mouse...");
	if (master_mouse_reset() != 2)
	{
		cprintf("\n\rCannot find mouse driver or mouse itself.");
		exit(EXIT_FAILURE);
	}

	cprintf("\n\rInitializing game board...");
	delay(100);	/* just to see the message */
	init_ids();
	init_cards();

	detectgraph(&driver, &mode);

	if (driver != VGA || mode != VGAHI)
	{
		cprintf("\n\rRonnie-Recall requires VGA.");
		exit(EXIT_FAILURE);
	}

	initgraph(&driver, &mode, "");
	midx = (xmax = getmaxx()) / 2;
	midy =(ymax = getmaxy()) / 2;

	delay(MODE_DELAY);	/* because screen flickers */
	do_title();
	cleardevice();
	settextstyle(TRIPLEX_FONT, HORIZ_DIR, 6);
	settextjustify(CENTER_TEXT, CENTER_TEXT);
	setbkcolor(BACK_COLOR);
	init_screen();		/* draw solid rectangles */
	shuffle_cards(!NO_SOUND);	/* visual effect */
	sleep_ticks(9, NO_UPDATE);   /* wait half of a second */
	cleardevice();
	init_screen();		/* draw solid rectangles */
	launch_pixels();
	init_time();
	set_mouse_pos(midx, midy);
	display_mouse_cursor();
	left_button_status(&binfo);

	do
	{
		if (kbhit() && (key = getch()) == ESC)
			continue;	/* leave loop */

		if (!kbhit())
		{
			update_pixels();

			if (update_time() == 0)
			{
				key = ESC;
				continue;
			}/* if */
		}/* if */

		left_button_status(&binfo);

		if (binfo.num_presses)	/* left button was pressed */
		{
			if (!in(binfo.horz, binfo.vert))
				continue;

			c = &cards[result.row][result.column];

			if (c->drawn || c->removed)
				continue;

			if (chosen == 0)	/* none yet */
			{
				prev = c;
				draw_card(c);
				chosen++;
			}
			else if (chosen == 1)	/* one chosen already */
			{
				guesses++;

				draw_card(c);
				sleep_ticks(OTHER_DELAY, !NO_UPDATE);

				if (c->id == prev->id)	/* match */
				{
						num_cards -= 2;
						explode();
						beep();
						remove_pair(c, prev);
				}
				else					/* no match */
					undraw_pair(c, prev);

				chosen = 0;
			}
		}/* if */
	}while (key != ESC && num_cards > 0);

	sleep_ticks(TICKS_PER_SEC, NO_UPDATE);

	hide_mouse_cursor();

	if (num_cards == 0)
	{
		init_secs--;	/* bump down by one */
		cleardevice();
		setcolor(EGA_MAGENTA);
		settextstyle(TRIPLEX_FONT, HORIZ_DIR, 3);
		min_left = (init_secs - seconds_left) / SECS_PER_MINUTE;
		sec_left = (init_secs - seconds_left) % SECS_PER_MINUTE;
		sprintf(misc_buffer, "You finished the game in %d minutes %d seconds.",
			min_left, sec_left);
		outtextxy(midx, midy / 2, misc_buffer);
		sprintf(misc_buffer, "It took you %d guesses.", guesses);
		outtextxy(midx, midy, misc_buffer);
		settextstyle(TRIPLEX_FONT, HORIZ_DIR, 4);
		setcolor(EGA_GREEN);
		outtextxy(midx, ymax - textheight(string5), string5);
		getch();
	}

	cleardevice();
	draw_all_cards();	// show solution
	settextstyle(TRIPLEX_FONT, HORIZ_DIR, 4);
	setcolor(EGA_GREEN);
	outtextxy(midx, ymax - textheight(string5), string5);
	getch();

	closegraph();

	if (num_cards == 0)
	{
		delay(MODE_DELAY);
		update_log(argv[1], guesses, min_left, sec_left, skill_level);
	}

	exit(EXIT_SUCCESS);
	return;
}/* main() */
