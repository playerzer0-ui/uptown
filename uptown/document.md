# Uptown code guide

Uptown is a C#/.NET 8 platformer built with MonoGame DesktopGL. It also has a Mario Maker-style editor: build a level, test it, and beat it to save it.

## How the game runs

`Program.cs` starts `Game1`. `Game1.cs` creates the window, loads shared resources, reads input each frame, and runs the active screen's `Update` (logic) and `Draw` (graphics).

Only one mode runs at a time. `Modes/GameMode.cs` defines the common methods for entering, leaving, updating, and drawing a mode.

| File | Main job |
| --- | --- |
| `Modes/HomeMode.cs` | Main menu and scrollable saved-level list. Loading opens a level in the editor. |
| `Modes/EditorMode.cs` | Paint terrain, place/move/rotate objects, set spawn, edit platform routes, zoom/pan, expand levels, and request saves. |
| `Modes/PlayMode.cs` | Open the CSV lobby for normal Play, or build an editor playtest from level data; update objects/player and follow the player with the camera. |
| `Player.cs` | Movement, gravity, collision, jumping/double jumping, wall sliding/climbing, crouching, animation, and respawning. |
| `LevelData.cs` | Store terrain, materials, spawn, objects, and routes; validate, load, and save level files. |

## Building and saving levels

Home → Play and F1 open the lobby from `Content/maps/lobby.csv` using `TileMap` and `floor.png`, with `lobby_collision.csv` providing solid tiles. The player starts near the bottom-left floor at (12, 304). The lobby's stop button returns home. The editor's Play button opens a separate `PlayTest` mode, so editor testing and clear-to-save validation still use the edited level.

The editor starts with an empty **60 × 40 grid of 8-pixel tiles**, matching the debug reference level. Terrain choices are basic, wall, glass, grass, ground, and floor; these change appearance while remaining solid terrain. Placement checks reject blocked or unsupported objects. If no spawn is chosen, the code looks for clear space above terrain.

**Editor → `LevelData.Capture()` → `PlayMode`** creates a separate playtest, so playing does not change the editor's terrain. Saving requires a valid spawn, exit flag, and valid object placements. You must reach the exit during a fresh save playtest; then that captured level is saved as JSON in `Levels/*.uptown`. Returning to the editor cancels the pending save. Loaded levels reuse their original save path.

## Objects in `SpecialObjects/`

- `Checkpoint` changes where you respawn; `ExitFlag` completes the level.
- `Spring` and `BounceBall` launch you. Jumping near contact gives a stronger bounce; both share `BounceObject` logic.
- `Platform` is a one-way landing surface. `PlatformLayout` joins painted cells into horizontal runs supported by terrain at an end.
- `MovingPlatform` follows a route back and forth, carries/pushes players, and can crush them.
- `Spike` kills on contact and can attach to a moving platform.
- `Door` opens on contact; its top becomes a one-way landing surface.
- `Elevator` is a linked pair that works in both directions. Nearby doors open; K at either open door starts a 0.3-second closing/teleport sequence, followed by the arrival door's reverse animation before movement resumes. Platforms and world animations keep updating during travel; the hidden passenger cannot be pushed or hit by hazards. The editor draws a lavender connector with arrows in both directions between the indicators above the doors; play mode hides it.
- `SpecialObject` provides shared touch/reset methods; `ObjectRotation` handles directions and rotated bounds.

Deaths reset objects while keeping the active checkpoint.

## Shared helpers in `models/`

- **Maps:** `Map` holds tile data; `AutoTileMap` automatically chooses terrain edges/corners; `TerrainCatalog` lists materials. `CollisionMap` checks solid tiles, while `ICollider`, `CollisionRect`, and `CollisionCircle` describe hitboxes. Player movement checks one pixel at a time to avoid skipping through walls.
- **Objects/input:** `Entity` and `EntityList` manage world objects; `InputManager` tracks keyboard/controller input; `Path2D` moves through waypoints.
- **Drawing:** `Camera` controls the view, `WindowRendering` keeps pixels aligned, `SpriteAnimation` plays frames, `Globals` shares resources, and `PicoPallete` supplies colors.
- **Additional helpers:** `Canvas`, `CameraFocus`, `TileMap`, `Sprite`, `SpriteSheet`, and `TextBox` provide reusable drawing/map features; the current modes mainly use direct window rendering and `AutoTileMap`.

`Content/` contains textures, fonts, and sample CSV maps. `Content.mgcb` builds assets for MonoGame. `bin/` and `obj/` mainly contain build output, plus movement/platform check programs.

## Useful controls

- **Anywhere:** F1 lobby, F2 editor, F3 home, Esc exit.
- **Play:** WASD/arrows move; Space jumps; hold J to grab/climb walls; S/down crouches; R respawns; K enters either open elevator in a linked pair. Controller input is also supported for movement.
- **Editor:** choose a palette tool; left-drag paints/places, right-click/drag deletes or erases. P + click sets spawn; R rotates supported objects. Wheel zooms, middle-drag pans, F fits the level. Fixed arrows on the right of the viewport add/remove one column; fixed arrows at the bottom add/remove one row. Their width and height each scale between 64 and 96 screen pixels, independent of camera zoom and pan. Ctrl+Left/Right/Up/Down does the same. Shrinking crops the right/bottom edge and is blocked if it would cut off a spawn, object, or moving-platform route. Ctrl+S starts save validation.
- **Moving routes:** click start and waypoints, double-click or Enter to finish, right-click/Backspace to undo draft points, drag platform ends to resize.

The background palette is currently a placeholder. Start reading `Game1.cs`, then the three mode files, `Player.cs`, and `LevelData.cs`.
