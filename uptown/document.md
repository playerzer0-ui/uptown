# Uptown code guide

Uptown is a C#/.NET 8 platformer built with MonoGame DesktopGL. It also has a Mario Maker-style editor: build a level, test it, and beat it to save it.

## How the game runs

`Program.cs` starts `Game1`. `Game1.cs` creates the window, loads shared resources, reads input each frame, and runs the active screen's `Update` (logic) and `Draw` (graphics).

Only one mode runs at a time. `Modes/GameMode.cs` defines the common methods for entering, leaving, updating, and drawing a mode.

| File | Main job |
| --- | --- |
| `Modes/HomeMode.cs` | Main menu and scrollable saved-level list. Loading opens a level in the editor. |
| `Modes/EditorMode.cs` | Paint terrain, place/move/rotate objects, set spawn, edit platform routes, zoom/pan, expand levels, and request saves. |
| `Modes/PlayMode.cs` | Turn editor data into a playable world, update objects/player, detect touches and completion, and follow the player with the camera. |
| `Player.cs` | Movement, gravity, collision, jumping/double jumping, wall sliding/climbing, crouching, animation, and respawning. |
| `LevelData.cs` | Store terrain, materials, spawn, objects, and routes; validate, load, and save level files. |

## Building and saving levels

The editor starts with an empty **400 × 80 grid of 8-pixel tiles**. Terrain choices are basic, wall, glass, grass, and ground; these change appearance while remaining solid terrain. Placement checks reject blocked or unsupported objects. If no spawn is chosen, the code looks for clear space above terrain.

**Editor → `LevelData.Capture()` → `PlayMode`** creates a separate playtest, so playing does not change the editor's terrain. Saving requires a valid spawn, exit flag, and valid object placements. You must reach the exit during a fresh save playtest; then that captured level is saved as JSON in `Levels/*.uptown`. Returning to the editor cancels the pending save. Loaded levels reuse their original save path.

## Objects in `SpecialObjects/`

- `Checkpoint` changes where you respawn; `ExitFlag` completes the level.
- `Spring` and `BounceBall` launch you. Jumping near contact gives a stronger bounce; both share `BounceObject` logic.
- `Platform` is a one-way landing surface. `PlatformLayout` joins painted cells into horizontal runs supported by terrain at an end.
- `MovingPlatform` follows a route back and forth, carries/pushes players, and can crush them.
- `Spike` kills on contact and can attach to a moving platform.
- `Door` opens on contact; its top becomes a one-way landing surface.
- `SpecialObject` provides shared touch/reset methods; `ObjectRotation` handles directions and rotated bounds.

Deaths reset objects while keeping the active checkpoint.

## Shared helpers in `models/`

- **Maps:** `Map` holds tile data; `AutoTileMap` automatically chooses terrain edges/corners; `TerrainCatalog` lists materials. `CollisionMap` checks solid tiles, while `ICollider`, `CollisionRect`, and `CollisionCircle` describe hitboxes. Player movement checks one pixel at a time to avoid skipping through walls.
- **Objects/input:** `Entity` and `EntityList` manage world objects; `InputManager` tracks keyboard/controller input; `Path2D` moves through waypoints.
- **Drawing:** `Camera` controls the view, `WindowRendering` keeps pixels aligned, `SpriteAnimation` plays frames, `Globals` shares resources, and `PicoPallete` supplies colors.
- **Additional helpers:** `Canvas`, `CameraFocus`, `TileMap`, `Sprite`, `SpriteSheet`, and `TextBox` provide reusable drawing/map features; the current modes mainly use direct window rendering and `AutoTileMap`.

`Content/` contains textures, fonts, and sample CSV maps. `Content.mgcb` builds assets for MonoGame. `bin/` and `obj/` mainly contain build output, plus movement/platform check programs.

## Useful controls

- **Anywhere:** F1 play, F2 editor, F3 home, Esc exit.
- **Play:** WASD/arrows move; Space jumps; hold J to grab/climb walls; S/down crouches; R respawns. Controller input is also supported.
- **Editor:** choose a palette tool; left-drag paints/places, right-click/drag deletes or erases. P + click sets spawn; R rotates supported objects. Wheel zooms, middle-drag pans, F fits the level. Ctrl+Right/Down expands it; Ctrl+S starts save validation.
- **Moving routes:** click start and waypoints, double-click or Enter to finish, right-click/Backspace to undo draft points, drag platform ends to resize.

The background palette is currently a placeholder. Start reading `Game1.cs`, then the three mode files, `Player.cs`, and `LevelData.cs`.
