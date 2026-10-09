# Uptown code guide

Uptown is a C#/.NET 8 platformer built with MonoGame DesktopGL. It also has a Mario Maker-style editor: build a level, test it, and beat it to save it.

## How the game runs

`Program.cs` starts `Game1`. `Game1.cs` creates the window, loads shared resources, reads input each frame, and runs the active screen's `Update` (logic) and `Draw` (graphics).

Only one mode runs at a time. `Modes/GameMode.cs` defines the common methods for entering, leaving, updating, and drawing a mode.

The mode IDs are `Home`, `Editor`, `Play` (lobby), `SavedPlay` (saved-level gameplay), and `PlayTest` (editor test). All playable modes use `PlayMode`: its `Action` constructor loads the lobby, while its `LevelData` constructor builds an editor capture or saved level. `Game1` polls `Globals.Input` once per frame and skips simulation on navigation frames. Focused editor text input takes priority over global shortcuts. The resizable window starts at 1280x720 and enforces that minimum size.

| File | Main job |
| --- | --- |
| `Modes/HomeMode.cs` | Main menu and scrollable saved-level list. Loading opens a level in the editor. |
| `Modes/EditorMode.cs` | Paint terrain, place/move/rotate objects, set spawn, edit platform routes, zoom/pan, expand levels, and request saves. |
| `Modes/LobbyLevelPicker.cs` | Read the lobby elevator marker, list saved levels, and run its one-shot closing transition. |
| `Modes/PlayMode.cs` | Open the CSV lobby for normal Play, or build an editor playtest from level data; update objects/player and follow the player with the camera. |
| `Player.cs` | Movement, gravity, collision, jumping/double jumping, wall sliding/climbing, crouching, animation, and respawning. |
| `LevelData.cs` | Store terrain, materials, spawn, objects, and routes; validate, load, and save level files. |

## Folder structure

| Path | Contents and purpose |
| --- | --- |
| Project root | Entry point, game host, player, level persistence, `.csproj`, manifest/icons, this guide, and `AGENTS.md` project context. `text.txt` is historical design discussion. |
| `Modes/` | Menu, editor, lobby/playtest orchestration, and the base mode contract. |
| `models/` | Reusable engine helpers in the existing `NodeTesting.models` namespace. See the helper map below. |
| `SpecialObjects/` | Interactive level objects, placement/support rules, and elevator travel. |
| `Decorations/` | Non-colliding decorations; currently the background-mounted lightstick. |
| `Content/graphics/` | Player sheets, terrain atlases, special-object sheets, background tiles, decoration sprites, and UI graphics. |
| `Content/maps/` | Lobby visual, collision, and special-layer CSV files. The leftmost complete ID-3 block in the special layer places the lobby level-selection elevator. |
| `Content/Content.mgcb` | MonoGame asset pipeline. Register texture/font sources here to produce runtime `.xnb` assets. |
| `Levels/` | JSON levels with the `.uptown` extension, including the debug reference. |
| `.config/`, `.vscode/` | Local MonoGame tools and editor launch configuration. |
| `bin/`, `obj/`, `Content/bin/`, `Content/obj/` | Build output; root `bin/` and `obj/` also contain useful check-program sources, so they are not exclusively disposable caches. |

## Building and saving levels

Home → Play and F1 open the lobby from `Content/maps/lobby.csv` using `TileMap` and `floor.png`, with `lobby_collision.csv` providing solid tiles. The player starts near the bottom-left floor at (12, 304). The lobby's stop button returns home. The leftmost 2x3 ID-3 marker in `lobby_special.csv` places one 16x24 elevator at feet (264, 304). It opens on proximity; K opens a right-side, scrollable saved-level picker and pauses player movement. K, the header, or clicking outside closes the picker. Selecting a valid saved level locks/hides the player, plays the five occupied closing frames once over 0.3 seconds, then opens that level at its saved spawn in `SavedPlay`. Invalid files show an error in the picker. SavedPlay stop returns to the lobby; editor playtests and save validation remain separate. The editor's Play button opens a separate `PlayTest` mode, so editor testing and clear-to-save validation still use the edited level.

The editor starts with an empty **60 × 40 grid of 8-pixel tiles**, matching the debug reference level. Terrain choices are basic, wall, glass, grass, ground, and floor; these change appearance while remaining solid terrain. Placement checks reject blocked or unsupported objects. If no spawn is chosen, the code looks for clear space above terrain.

**Editor → `LevelData.Capture()` → `PlayMode`** creates a separate playtest, so playing does not change the editor's terrain. Saving requires a valid spawn, exit flag, and valid object placements. You must reach the exit during a fresh save playtest; then that captured level is saved as JSON in `Levels/*.uptown`. Returning to the editor cancels the pending save. Loaded levels reuse their original save path.

The editor has Terrain, Special, Sprites, Background, and Decoration palettes. Background painting is implemented, including erase, resize, and persistence; it has no collision. Selecting Background fades foreground visuals for easier editing. Sprites currently provides the player spawn. Decoration currently provides Lightstick.

Resizing changes the right/bottom edge and preserves the overlapping terrain, material IDs, and background cells. New cells are empty. Shrinking crops removed cells and refuses to cut off an explicit spawn, object, or moving-platform waypoint footprint. Minimum size is 2x2 tiles, with a maximum of 1,048,576 cells. The fixed resize buttons use `arrow-left.png`; `MinResizeButtonWidth`, `MaxResizeButtonWidth`, `MinResizeButtonHeight`, and `MaxResizeButtonHeight` in EditorMode configure their screen-pixel limits. Current limits are 48–384, with preferred size based on `ButtonSize` and UI scale.

Level data stores terrain occupancy and materials separately, background IDs/tileset names, spawn, and `LevelObject` records. Object records contain type, feet, direction, width, route/speed, and elevator pair identity/endpoint role. Captures copy grids, objects, and waypoints. Loading validates dimensions, material/background IDs, moving routes, and complete elevator pairs. Save names are checked for invalid/reserved Windows filenames; writes use a temporary file and move. Successful renames remove the previous file after the new file is written. SaveFolder finds the project `Levels/` folder during development, otherwise a folder beside the application.

## Objects in `SpecialObjects/`

- `Checkpoint` changes where you respawn; `ExitFlag` completes the level.
- `Spring` and `BounceBall` launch you. Jumping near contact gives a stronger bounce; both share `BounceObject` logic.
- `Platform` is a one-way landing surface. `PlatformLayout` joins painted cells into horizontal runs supported by terrain at an end.
- `MovingPlatform` follows a route back and forth, carries/pushes players, and can crush them.
- `Spike` kills on contact and can attach to a moving platform.
- `Door` opens on contact; its top becomes a one-way landing surface.
- `Elevator` is a linked pair that works in both directions. Nearby doors open; K at either open door starts a 0.3-second closing/teleport sequence, followed by the arrival door's reverse animation before movement resumes. Platforms and world animations keep updating during travel; the hidden passenger cannot be pushed or hit by hazards. The editor draws a lavender connector with arrows in both directions between the indicators above the doors; play mode hides it.
- `ElevatorTravel` owns the two-phase travel timer and temporarily locks/hides the player. Elevator sheets each have five 16x24 frames. Placing the editor tool creates two linked doors; dragging preserves the pair, and right-click deletes the pair. The connector anchors to the 2x2 lavender indicator above each door.
- `SpecialObject` provides shared touch/reset methods; `ObjectRotation` handles directions and rotated bounds.

Deaths use a 0.45-second sequence: the player disappears, `DeathEffect` draws an expanding pale-blue bubble, and 18 particles burst outward. Spikes, crushing, falling, and R trigger it. Respawn then resets special objects while retaining the active checkpoint and pending save-validation callback. World simulation pauses during death; elevator travel keeps world simulation running.

## Decorations

`Decorations/Lightstick.cs` derives from Entity and has no collider. It uses an 8x8 sprite, requires background tiles behind its footprint, and draws a procedural warm glow with a 24-pixel radius. Editor and playtest rendering support it; LevelObject records persist it alongside interactive objects. No external light-mask PNG, wall shadows, or dark-scene lighting pass is used. Procedural light/death textures are released by Game1.UnloadContent.

## Shared helpers in `models/`

These helpers remain in `NodeTesting.models`; game-specific code lives under `uptown`, `uptown.Modes`, `uptown.SpecialObjects`, and `uptown.Decorations`.

| Helper | Responsibility and current usage |
| --- | --- |
| `Map.cs` | Base tile grid, atlas splitting, CSV loading, and optional tile animations. Grid arrays index `[y,x]`; negative tile IDs are empty. |
| `TileMap.cs` | Direct rendering of stored atlas IDs. Used by the lobby with `floor.png`. |
| `AutoTileMap.cs` | Material-aware 8x8 terrain assembled from 4x4 quarters, with full-tile matching for atlas details. Paint refreshes neighboring cells; Resize preserves overlap and rebuilds edges. Used by Editor and LevelData playtests. |
| `TerrainCatalog.cs` | Names/asset paths for basic, wall, glass, grass, ground, and floor. Materials affect appearance, not solidity. |
| `BackgroundMap.cs` | Decorative background IDs, occupancy queries for mounts, painting, capture, resize, and drawing. No collision or autotiling. |
| `CollisionMap.cs` | Separate solid grid: tile ID 0 is solid, out-of-bounds is empty. Only tiles overlapping the queried bounds are tested. Also offers reusable-probe collider resolution/debug drawing. |
| `ICollider.cs` | Common shape interface for static-state flags, intersection, containment, debug drawing, and overlap resolution. |
| `CollisionRect.cs`, `CollisionCircle.cs` | Rectangular/circular hitboxes and collision helpers. CollisionRect constructor coordinates are its center, not its top-left corner. |
| `Entity.cs` | Base world object with position, optional collider, active/visible flags, scene membership, and lifecycle hooks. |
| `EntityList.cs` | Ordered update/draw list with deferred additions/removals during update; typed lookup and collision queries. |
| `InputManager.cs` | Named keyboard/controller bindings, current/previous state, edge presses/releases, and stick input. Poll once per frame in Game1. |
| `Path2D.cs` | Waypoint motion, speed, waits, looping, reverse, and ping-pong. Preserves leftover frame time through corners; MovingPlatform adds pixel-step collision/carry behavior. |
| `Camera.cs`, `CameraFocus.cs` | View transforms and optional instant/smooth focus helpers. Current modes directly manage camera position/zoom. |
| `WindowRendering.cs` | Integer UI/render scale based on a 320x180 reference, plus rounded transform translation for pixel alignment. Current modes draw directly to the window with PointClamp. |
| `Canvas.cs` | Optional render-target/letterbox helper and coordinate conversion. Current modes do not use its off-screen presentation path. |
| `SpriteAnimation.cs` | AnimationState, multi-row animation, frame control, looping/one-shot/reverse support, and the single-row SpriteAnimation wrapper. Player uses separate sheets per action. Elevators choose timed frames directly. |
| `Sprite.cs`, `SpriteSheet.cs` | Additional static-sprite and frame-sheet drawing helpers. |
| `TextInput.cs` | Editable level-name input using MonoGame text events, caret/editing/repeat behavior, focus handling, and disposal. |
| `TextBox.cs` | Simple centered text drawing helper. |
| `ParticleSystem.cs` | Visual bursts, velocities/gravity, particle lifetime/fade, drawing, and cleanup. No particle collision. |
| `DeathEffect.cs` | Reusable procedural expanding bubble and particle burst driven by Player.Died. |
| `Globals.cs` | Shared content manager, sprite batch, graphics manager, input manager, and lazily created white pixel texture. Requires Game1 to initialize graphics/content first. |
| `PicoPallete.cs` | Existing shared pixel-art colors; lavender is used for elevator connectors. Keep the established spelling when referencing it. |

## Coordinates and update order

World tiles are 8x8 pixels. Player and LevelObject positions use bottom-center feet; sprites/hitboxes convert from there. The player's standing collider is 8x12, crouching collider 8x6. Player movement accumulates fractional remainders, then moves in one-pixel steps to prevent tunneling through thin tiles. Moving-platform transport also steps by pixel, while ordinary one-way platforms only block downward crossings.

PlayMode updates elevator proximity/travel state, terrain animations, player movement when permitted, moving platforms, and other entities, then handles touches and completion. Elevator frames continue world updates but skip player/hazard touch handling until arrival animation completes. Spikes take priority over other touch hooks, so contact with a hazard and exit cannot clear a level simultaneously. Respawn/reset checks occur between stages. The camera clamps to level bounds and uses integer rendering scale; editor zoom/pan and fixed UI coordinates are separate.

## Useful controls

- **Anywhere:** F1 lobby, F2 editor, F3 home, Esc exit.
- **Play:** WASD/arrows move; Space jumps; hold J to grab/climb walls; S/down crouches; R respawns; K enters either open elevator in a linked pair. Controller input is also supported for movement.
- **Editor:** choose a palette tool; left-drag paints/places, right-click/drag deletes or erases. P + click sets spawn; R rotates supported objects. Wheel zooms, middle-drag pans, F fits the level. Fixed arrows on the right of the viewport add/remove one column; fixed arrows at the bottom add/remove one row. Their width/height limits are configured in EditorMode and are independent of camera zoom and pan. Ctrl+Left/Right/Up/Down does the same. Shrinking crops the right/bottom edge and is blocked if it would cut off a spawn, object, or moving-platform route. Ctrl+S starts save validation.
- **Moving routes:** click start and waypoints, double-click or Enter to finish, right-click/Backspace to undo draft points, drag platform ends to resize.

## Build and checks

`uptown.csproj` targets net8.0 and references MonoGame Framework DesktopGL and Content Builder Task 3.8.*. `.config/dotnet-tools.json` declares MGCB/editor tools; the project runs `dotnet tool restore` during builds. Source CSV maps copy to both build and publish output.

```powershell
dotnet build uptown.csproj --no-restore
dotnet run --project bin/MovingPlatformChecks/Checks.csproj --no-restore
dotnet run --project obj/elevator-checks/Checks.csproj --no-restore
dotnet run --project obj/lobby-checks/Checks.csproj --no-restore
dotnet run --project obj/resize-checks/Checks.csproj --no-restore
dotnet run --project obj/death-checks/Checks.csproj --no-restore
dotnet run --project obj/lightstick-checks/Checks.csproj --no-restore
```

These console checks reference the built game DLL, so build first. On a fresh checkout, restore each check project before using `--no-restore`. Choose checks relevant to the change. The older `obj/movement-checks/` harness includes stale assumptions such as captured level version 1; inspect it before interpreting failures. Builds and headless checks verify code behavior, not final visual appearance.

Start reading Game1, the mode files, Player, and LevelData, then follow their dependencies into models and object classes. `AGENTS.md` stores the concise project context for future work. Keep new/edited C# files in CRLF, preserve user maps/saves and size-setting adjustments, and update this guide when established behavior changes.
