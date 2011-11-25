#ifdef WIN32
#pragma comment(lib, "../SDL/lib/SDL.lib")
#endif

#include <tchar.h>
#include <cstdlib>
#include <ctime>

#include "../SDL/includes/SDL.h"
#include "math.h"

//Screen size
const int WIDTH = 1024;
const int HEIGHT = 768;

int particleCount = 0;
int penSize = 5;

bool implementParticleSwaps = false;

enum ParticleType
{
	NOTHING = 0,
	WALL = 1,
	SAND = 2,
	MOVEDSAND = 3,
	WATER = 4,
	MOVEDWATER = 5,
	OIL = 6,
	MOVEDOIL = 7
};

//Instead of using a two dimensional array
// we'll use a simple array to improve speed
// vs = virtual screen
ParticleType vs[WIDTH*HEIGHT];

// The current brush type
ParticleType CurrentParticleType = WALL;

//THe screen
SDL_Surface *screen;

static Uint32 WATERCOLOR = 0;
static Uint32 OILCOLOR = 0;
static Uint32 WALLCOLOR = 0;
static Uint32 SANDCOLOR = 0;

// Initializing colors
void initColors()
{
	OILCOLOR = SDL_MapRGB(screen->format, 130, 70, 0);
	WATERCOLOR = SDL_MapRGB(screen->format, 0, 50, 255);
	WALLCOLOR = SDL_MapRGB(screen->format, 100, 100, 100);
	SANDCOLOR = SDL_MapRGB(screen->format, 255, 230, 127);
}

//Setting a pixel to a given color
inline void SetPixel16Bit(Uint16 x, Uint16 y, Uint32 pixel)
{
  *((Uint16 *)screen->pixels + y * screen->pitch/2 + x) = pixel;
}

//Drawing our virtual screen to the real screen
static void DrawScene()
{
	particleCount = 0;

	//Locking the screen
	if ( SDL_MUSTLOCK(screen) )
	{
		if ( SDL_LockSurface(screen) < 0 )
		{
			return;
		}
	}

	//Clearing the screen with black
	SDL_FillRect(screen,NULL,0);

	//Iterating through each pixe height first
	for(int y=0;y<HEIGHT;y++)
	{
		//Width
		for(int x=0;x<WIDTH;x++)
		{
			int same = x+(WIDTH*y);

			//If the type is of MOVEDx then set it to x and draw it
			switch(vs[same])
			{
			case MOVEDSAND:
				vs[same] = SAND;
			case SAND:
				SetPixel16Bit(x,y,SANDCOLOR);
				particleCount++;
				break;
			case MOVEDWATER:
				vs[same] = WATER;
			case WATER:
				SetPixel16Bit(x,y,WATERCOLOR);
				particleCount++;
				break;
			case MOVEDOIL:
				vs[same] = OIL;
			case OIL:
				SetPixel16Bit(x,y,OILCOLOR);
				particleCount++;
				break;
			case WALL:
				SetPixel16Bit(x,y,WALLCOLOR);
				break;
			}
		}
	}
	//Unlocking the screen
	if ( SDL_MUSTLOCK(screen) )
	{
		SDL_UnlockSurface(screen);
	}
}

// Emitting a given particletype at (x,o) width pixels wide and
// with a p density (probability that a given pixel will be drawn 
// at a given position withing the width)
void Emit(int x, int width, ParticleType type, float p)
{
	for (int i = x - width/2; i < x + width/2; i++)
	{
		if ( rand() < (int)(RAND_MAX * p) ) vs[i+WIDTH] = type;
	}
}

// Performing the movement logic of a given particle. The argument 'type'
// is passed so that we don't need a table lookup when determining the
// type to set the given particle to - i.e. if the particle is SAND then the
// passed type will be MOVEDSAND
inline void MoveParticle(int x, int y, ParticleType type)
{
	//Creating a random int
	int r = rand();

	if ( r < RAND_MAX / 13 ) return; // This makes it fall unevenly

	// We'll only calculate these indicies once for optimization purpose
	int below = x+((y+1)*WIDTH);
	int same = x+(WIDTH*y);

	//If nothing below then just fall
	if ( vs[below] == NOTHING && r % 8) //rand() % 8 makes it spread
	{
		vs[below] = type;
		vs[same] = NOTHING;
		return;
	}

	// Peform 'realism' logic?
	if(implementParticleSwaps)
	{
		//Making water lighter than sand
		if(type == MOVEDSAND && (vs[below] == WATER))
		{
			vs[below] = MOVEDSAND;
			vs[same] = WATER;
			return;
		}

		//Making sand lighter than oil
		if(type == MOVEDSAND && (vs[below] == OIL) && (rand() % 5 == 0)) //Making oil dense so that sand falls slower
		{
			vs[below] = MOVEDSAND;
			vs[same] = OIL;
			return;
		}

		//Making oil lighter than water
		if(type == MOVEDWATER && (vs[below] == OIL))
		{
			vs[below] = MOVEDWATER;
			vs[same] = OIL;
			return;
		}
	}

	//Randomly select right or left first
	int sign = rand() % 2 == 0 ? -1 : 1;

	//Add to sideways flow
	if(((vs[(x+1)+((y-1)*WIDTH)] !=  NOTHING && vs[(x+1)+(WIDTH*y)] != NOTHING) || (vs[(x-1)+((y-1)*WIDTH)] != NOTHING && vs[(x-1)+(WIDTH*y)] != NOTHING)) && (x-5)>0 && (x+5) < WIDTH)
		sign *= rand()%5;

	// We'll only calculate these indicies once for optimization purpose
	int firstdown = (x+sign)+((y+1)*WIDTH);
	int seconddown = (x-sign)+((y+1)*WIDTH);
	int first = (x+sign)+(WIDTH*y);
	int second = (x-sign)+(WIDTH*y);

	// The place below (x,y+1) is filled with something, then check (x+sign,y+1) and (x-sign,y+1) 
	// We chose sign randomly to randomly check eigther left or right
	if ( vs[firstdown] == NOTHING )
	{
		vs[firstdown] = type;
		vs[same] = NOTHING;
	}
	else if ( vs[seconddown] == NOTHING )
	{
		vs[seconddown] = type;
		vs[same] = NOTHING;
	}
	//If (x+sign,y+1) is filled then try (x+sign,y) and (x-sign,y)
	else if (vs[first] == NOTHING )
	{
		vs[first] = type;
		vs[same] = NOTHING;
	}
	else if (vs[second] == NOTHING )
	{
		vs[second] = type;
		vs[same] = NOTHING;
	}
}


//Drawing a filled circle at a given position with a given radius and a given partice type
void DrawParticles(int xpos, int ypos, int radius, ParticleType type)
{
	for (int x = ((xpos - radius - 1) < 0) ? 0 : (xpos - radius - 1); x <= xpos + radius && x < WIDTH; x++)
		for (int y = ((ypos - radius - 1) < 0) ? 0 : (ypos - radius - 1); y <= ypos + radius && y < HEIGHT; y++)
		{
			if ((x-xpos)*(x-xpos) + (y-ypos)*(y-ypos) <= radius*radius) vs[x+(WIDTH*y)] = type;
		};
}

// Drawing some random wall lines
void DoRandomLines(ParticleType type)
{
	for(int i = 0; i < 20; i++)
	{
		int x1 = rand() % WIDTH;
		int x2 = rand() % WIDTH;

		float step = 1.0f / HEIGHT;
		for (float a = 0; a < 1; a+=step)
			DrawParticles((int)(a*x1+(1-a)*x2),(int)(a*0+(1-a)*HEIGHT),penSize,type); 
	}
	for(int i = 0; i < 20; i++)
	{
		int y1 = rand() % HEIGHT;
		int y2 = rand() % HEIGHT;

		float step = 1.0f / WIDTH;
		for (float a = 0; a < 1; a+=step)
			DrawParticles((int)(a*0+(1-a)*WIDTH),(int)(a*y1+(1-a)*y2),penSize,type); 
	}
}

// Drawing a line
void DrawLine(int newx, int newy, int oldx, int oldy)
{
	if(newx == oldx && newy == oldy)
	{
		DrawParticles(newx,newy,penSize,CurrentParticleType);
	}
	else
	{
		float step = 1.0f / ((abs(newx-oldx)>abs(newy-oldy)) ? abs(newx-oldx) : abs(newy-oldy));
		for (float a = 0; a < 1; a+=step)
			DrawParticles((int)(a*newx+(1-a)*oldx),(int)(a*newy+(1-a)*oldy),penSize,CurrentParticleType); 
	}
}

// Updating a virtual pixel
inline void UpdateVirtualPixel(int x, int y)
{
	switch (vs[x+(WIDTH*y)])
	{
	case SAND:
		MoveParticle(x,y,MOVEDSAND);
		break;
	case WATER:
		MoveParticle(x,y,MOVEDWATER);
		break;
	case OIL:
		MoveParticle(x,y,MOVEDOIL);
		break;				
	}
}

// Updating the particle system (virtual screen) pixel by pixel
inline void UpdateVirtualScreen()
{
	for(int y = HEIGHT-2; y > 0; y--)
	{
		// Due to biasing when iterating through the scanline from left to right,
		// we now chose our direction randomly per scanline.
		if (rand() & 2)
			for(int x = WIDTH-2; x > 0 ; x--) UpdateVirtualPixel(x,y);
		else
			for(int x = 1; x < WIDTH - 1; x++) UpdateVirtualPixel(x,y);
	}
}

//Cearing the partice system
void Clear()
{
	for(int w = 0; w < WIDTH ; w++)
	{
		for(int h = 0; h < HEIGHT; h++)
		{
			vs[w+(WIDTH*h)] = NOTHING;
		}
	}
}

// Initializing the screen a
void init()
{
	// Initializing SDL
	if ( SDL_Init( SDL_INIT_VIDEO ) < 0 )
	{
		fprintf( stderr, "Video initialization failed: %s\n",
			SDL_GetError( ) );
		SDL_Quit( );
	}

	//Creating the screen using 16 bit colors
	screen = SDL_SetVideoMode(WIDTH, HEIGHT, 16, SDL_HWSURFACE|SDL_DOUBLEBUF|SDL_ASYNCBLIT);
	if ( screen == NULL ) {
		fprintf(stderr, "Unable to set video mode: %s\n", SDL_GetError());

	}
	//Setting caption
	SDL_WM_SetCaption("SDL Sand",NULL);
	//Enabeling key repeats
	SDL_EnableKeyRepeat(SDL_DEFAULT_REPEAT_DELAY, SDL_DEFAULT_REPEAT_INTERVAL);

	initColors();
}

int _tmain(int argc, _TCHAR* argv[])
{
	init();
	Clear();

	int tick = 0;
	int done=0;

	//To emit or not to emit
	bool emitSand = true;
	bool emitWater = true;
	bool emitOil = true;

	//Initial density of emitters
	float oilDens = 0.3f;
	float sandDens = 0.3f;
	float waterDens = 0.3f;

	// Set initial seed
	srand( (unsigned)time( NULL ) );

	int oldx = 0, oldy = 0;

	//Mouse button pressed down?
	bool down = false;

	//Used for calculating the average FPS from NumFrames
	const int NumFrames = 20;
	int AvgFrameTimes[NumFrames];
	for( int i = 0; i < NumFrames; i++)
		AvgFrameTimes[i] = 0;
	int FrameTime = 0;
	int PrevFrameTime = 0;
	int Index = 0;

	//The game loop
	while(done == 0)
	{
		tick++;

		SDL_Event event;
		//Polling events
		while ( SDL_PollEvent(&event) )
		{
			if ( event.type == SDL_QUIT )  {  done = 1;  }

			//Key strokes
			if ( event.type == SDL_KEYDOWN )
			{
				switch (event.key.keysym.sym)
				{
				case SDLK_ESCAPE: //Exit
					done = 1;
					break;
				case SDLK_0: // Eraser
					CurrentParticleType = NOTHING;
					break;
				case SDLK_1: // Draw walls
					CurrentParticleType = WALL;
					break;
				case SDLK_2: // Draw sand		
					CurrentParticleType = SAND;
					break;
				case SDLK_3: // Draw water		
					CurrentParticleType = WATER;
					break;
				case SDLK_4: // Draw oil		
					CurrentParticleType = OIL;
					break;
				case SDLK_UP: // Increase pen size
					penSize *= 2;
					break;
				case SDLK_DOWN: // Decrease pen size
					penSize /= 2;
					if(penSize < 1)
						penSize = 1;
					break;
				case SDLK_DELETE: // Clear screen
					Clear();
					break;
				case SDLK_z: //Enable or disable sand emitter
					emitSand ^= true;
					break;
				case SDLK_x: //Enable or disable water emitter
					emitWater ^= true;
					break;
				case SDLK_q: // Increase sand emitter density
					sandDens += 0.05f;
					if(sandDens > 1.0f)
						sandDens = 1.00f;
					break;
				case SDLK_a: // Decrease sand emitter density
					sandDens -= 0.05f;
					if(sandDens < 0.05f)
						sandDens = 0.05f;
					break;
				case SDLK_w: // Increase water emitter density
					waterDens += 0.05f;
					if(waterDens > 1.0f)
						waterDens = 1.0f;
					break;
				case SDLK_s: // Decrease water emitter density
					waterDens -= 0.05f;
					if(waterDens < 0.05f)
						waterDens = 0.05f;
					break;
				case SDLK_e: // Increase oil emitter density
					oilDens += 0.05f;
					if(oilDens > 1.0f)
						oilDens = 1.0f;
					break;
				case SDLK_d: // Decrease oil emitter density
					oilDens -= 0.05f;
					if(oilDens < 0.05f)
						oilDens = 0.05f;
					break;
				case SDLK_c: //Enable or disable oil emitter
					emitOil ^= true;
					break;
				case SDLK_r: // Draw a bunch of random lines
					DoRandomLines(WALL);
					break;
				case SDLK_t: // Erase a bunch of random lines
					DoRandomLines(NOTHING);
					break;
				case SDLK_o: // Enable or disable particle swaps
					implementParticleSwaps ^= true;
					break;
				}
			}
			// If mouse button pressed then save position of cursor
			if( event.type == SDL_MOUSEBUTTONDOWN)
			{
				SDL_MouseButtonEvent mbe = (SDL_MouseButtonEvent) event.button;
				oldx = mbe.x; oldy=mbe.y;
				DrawLine(mbe.x,mbe.y,oldx,oldy);
				down = true;
			}
			// Button released
			if(event.type == SDL_MOUSEBUTTONUP)
			{
				SDL_MouseButtonEvent mbe = (SDL_MouseButtonEvent) event.button;
				DrawLine(mbe.x,mbe.y,oldx,oldy);
				down = false;
			}
			// Mouse has moved
			if(event.type == SDL_MOUSEMOTION)
			{
				SDL_MouseMotionEvent mme = (SDL_MouseMotionEvent) event.motion;
				if(mme.state & SDL_BUTTON(1))
					DrawLine(mme.x,mme.y,oldx,oldy);
				oldx = mme.x; oldy=mme.y;
			}
		}

		//To emit or not to emit
		if(emitSand)
			Emit(WIDTH/4, 20, SAND, sandDens);
		if(emitWater)
			Emit(WIDTH/4*2, 20, WATER, waterDens);
		if(emitOil)
			Emit(WIDTH/4*3, 20, OIL, oilDens);

		//If the button is pressed (and no event has occured since last frame due
		// to the polling procedure, then draw at the position (enabeling 'dynamic emitters')
		if(down)
			DrawLine(oldx,oldy,oldx,oldy);

		//Clear bottom line
		for (int i=0; i< WIDTH; i++) vs[i+((HEIGHT-1)*WIDTH)] = NOTHING;

		// Update the virtual screen (performing particle logic)
		UpdateVirtualScreen();
		// Map the virtual screen to the real screen
		DrawScene();
		//Fip the vs
		SDL_Flip(screen);

		//Printing out the framerate and particlecount
		FrameTime = SDL_GetTicks();
		AvgFrameTimes[Index] = FrameTime - PrevFrameTime;
		Index = (Index + 1) % NumFrames;
		PrevFrameTime = FrameTime;
		//We'll print for each 50 frames
		if(tick % 50 == 0)
		{
			int avg = 0;
			//Calculating the average over NumFrames frames
			for( int i = 0; i < NumFrames; i++)
				avg += AvgFrameTimes[i];

			avg = 1000/((int)avg/NumFrames);

			printf("FPS: %i\n",avg);
			printf("Particle count: %i\n",particleCount);
		}
	}

	//Loop ended - quit SDL
	SDL_Quit( );
	return 0;
}
