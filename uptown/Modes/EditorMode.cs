using System;
using System.Collections.Generic;
using System.IO;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using NodeTesting.models;
using uptown.SpecialObjects;
using uptown.Decorations;

namespace uptown.Modes;

// Terrain painting, object placement, and category palettes.
public sealed class EditorMode : GameMode, IDisposable
{
    private AutoTileMap preview;
    private BackgroundMap background;
    private readonly CollisionRect[] backgroundButtons;
    private readonly Texture2D[] backgroundTextures;
    private int selectedBackground;
    private float ForegroundOpacity => palette == Palette.Background ? 0.5f : 1f;
    private readonly Action<string> showStatus;
    private readonly Action<LevelData, Func<bool>> startSaveValidation;
    private Point? spawn;
    private string savePath;
    private readonly TextInput levelNameInput;
    public bool IsEditingText => levelNameInput.IsFocused;
    private LevelData snapshot;
    public LevelData Capture()
    {
        PlatformLayout.Rebuild(objects, PlatformLayout.Cells(objects), preview.Width, preview.Height, preview.Occupied);
        objects.RemoveAll(item => item.Type == LevelObject.Spike
            && !Spike.Supported(new Point(item.X, item.Y), item.Direction, preview.Occupied, objects));
        return LevelData.Capture(preview, spawn, objects, background);
    }
    private enum Tool { None, Terrain, Background, Spawn, Checkpoint, Exit, BounceBall, Spring, Platform, MovingPlatform, Spike, Door, Lightstick, Elevator }
    private enum Palette { Terrain, Special, Sprites, Background, Decoration }
    private Palette palette;
    private readonly Texture2D[] bars;
    private bool draggingFromPalette;
    private readonly CollisionRect[] terrainButtons;
    private int selectedTerrain;
    private readonly CollisionRect SpawnButton;
    private readonly CollisionRect CheckpointButton;
    private readonly CollisionRect ExitButton;
    private readonly CollisionRect BounceBallButton = new(16, 52, 27, 27);
    private readonly CollisionRect SpringButton = new(48, 52, 27, 27);
    private readonly CollisionRect PlatformButton = new(16, 83, 27, 27);
    private readonly CollisionRect MovingPlatformButton = new(48, 83, 27, 27);
    private readonly CollisionRect SpikeButton = new(16, 114, 27, 27);
    private readonly CollisionRect DoorButton = new(48, 114, 27, 27);
    private readonly CollisionRect LightstickButton = new(16, 21, 27, 27);
    private readonly CollisionRect ElevatorButton = new(16, 145, 27, 27);
    private LevelObject pathDraft;
    private LevelObject pathOriginal;
    private LevelObject resizingPlatform;
    private int resizeStartWidth;
    private float resizeStartDistance;
    private Point? pathCursor;
    private bool pathCursorValid;
    private float pathClickAge = float.PositiveInfinity;
    private Vector2 pathClickPosition;
    private BounceDirection bounceDirection;
    private BounceDirection ghostDirection;
    private bool manualSpringDirection;
    private Tool tool;
    private readonly List<LevelObject> objects = new();
    // While dragging the spawn or an object: where it would drop, and whether that spot is allowed.
    private Point? ghost;
    private bool ghostValid;
    // The checkpoint picked up by the current drag, or null when the drag places a new one.
    private LevelObject moving;
    private Point? lastPaintCell;
    private bool lastErase;
    private int viewWidth;
    private int viewHeight;
    private int windowWidth;
    private int windowHeight;
    private float uiScale = 1;
    private static readonly float[] ZoomLevels = { 0.0625f, 0.125f, 0.25f, 0.5f, 1f, 2f, 4f, 8f };
    private int zoomIndex;
    private bool fittedView = true;
    private int wheelRemainder;
    private readonly Action<ModeId> switchMode;
    private readonly Texture2D saveIcon;
    private readonly Texture2D playIcon;
    private readonly Texture2D homeIcon;
    private readonly Texture2D arrowIcon;
    private readonly Texture2D[] tilesets;
    private readonly Texture2D playerSprite;
    private readonly Texture2D checkpointSprite;
    private readonly Texture2D exitSprite;
    private readonly Texture2D bounceBallSprite;
    private readonly Texture2D springSprite;
    private readonly Texture2D platformSprite;
    private readonly Texture2D movingPlatformSprite;
    private readonly Texture2D spikeSprite;
    private readonly Texture2D doorSprite;
    private readonly Texture2D lightstickSprite;
    private readonly Texture2D elevatorSprite;
    private readonly Texture2D pixel;
    private readonly Camera camera;
    private bool isPanning;
    private bool sidebarOpen = true;
    private MouseState previousMouse;
    private Point pointer;
    private const int SidebarWidth = 80;
    private const int ButtonSize = 24;
    private readonly CollisionRect SaveButton;
    private readonly CollisionRect PlayButton;
    private readonly CollisionRect HomeButton;
    private readonly CollisionRect ToggleButton;

    public EditorMode(Game game, Action<ModeId> switchMode, Action<string> showStatus, Action<LevelData, Func<bool>> startSaveValidation)
    {
        viewWidth = 320;
        viewHeight = 180;
        this.switchMode = switchMode;
        this.showStatus = showStatus;
        this.startSaveValidation = startSaveValidation;
        levelNameInput = new TextInput(game, Globals.Content.Load<SpriteFont>("File"),
            new CollisionRect(160, 16, 144, 24), "Level name")
        {
            MaxLength = 48,
            ScreenToLocal = screen => screen / uiScale
        };
        levelNameInput.Submitted += _ => levelNameInput.Blur();
        saveIcon = Globals.Content.Load<Texture2D>("graphics/ui/save");
        playIcon = Globals.Content.Load<Texture2D>("graphics/ui/play");
        homeIcon = Globals.Content.Load<Texture2D>("graphics/ui/home");
        arrowIcon = Globals.Content.Load<Texture2D>("graphics/ui/arrow-left");
        bars = new[]
        {
            Globals.Content.Load<Texture2D>("graphics/ui/tilebar"),
            Globals.Content.Load<Texture2D>("graphics/ui/specialbar"),
            Globals.Content.Load<Texture2D>("graphics/ui/spritebar"),
            Globals.Content.Load<Texture2D>("graphics/ui/backgroundbar"),
            Globals.Content.Load<Texture2D>("graphics/ui/decobar")
        };
        // CollisionRect constructors take center coordinates, not top-left.
        SaveButton = new CollisionRect(viewWidth - 76, 16, ButtonSize, ButtonSize);
        PlayButton = new CollisionRect(viewWidth - 48, 16, ButtonSize, ButtonSize);
        HomeButton = new CollisionRect(viewWidth - 20, 16, ButtonSize, ButtonSize);
        ToggleButton = new CollisionRect(SidebarWidth + 6, viewHeight / 2, 12, 24);
        terrainButtons = new CollisionRect[TerrainCatalog.Names.Length];
        tilesets = new Texture2D[TerrainCatalog.Names.Length];
        for (int i = 0; i < terrainButtons.Length; i++)
        {
            terrainButtons[i] = new CollisionRect(16 + i % 2 * 32, 21 + i / 2 * 31, 27, 27);
            tilesets[i] = Globals.Content.Load<Texture2D>("graphics/tileset/" + TerrainCatalog.Names[i]);
        }
        backgroundButtons = new CollisionRect[BackgroundMap.Names.Length];
        backgroundTextures = new Texture2D[BackgroundMap.Names.Length];
        for (int i = 0; i < backgroundButtons.Length; i++)
        {
            backgroundButtons[i] = new CollisionRect(16 + i % 2 * 32, 21 + i / 2 * 31, 27, 27);
            backgroundTextures[i] = Globals.Content.Load<Texture2D>("graphics/backgroundtiles/" + BackgroundMap.Names[i]);
        }
        SpawnButton = new CollisionRect(16, 21, 27, 27);
        CheckpointButton = new CollisionRect(16, 21, 27, 27);
        ExitButton = new CollisionRect(48, 21, 27, 27);
        playerSprite = Globals.Content.Load<Texture2D>("graphics/player/idle");
        checkpointSprite = Globals.Content.Load<Texture2D>("graphics/special_objects/checkpoint");
        exitSprite = Globals.Content.Load<Texture2D>("graphics/special_objects/exit_flag");
        bounceBallSprite = Globals.Content.Load<Texture2D>("graphics/special_objects/bounceball");
        springSprite = Globals.Content.Load<Texture2D>("graphics/special_objects/spring");
        platformSprite = Globals.Content.Load<Texture2D>("graphics/special_objects/platform");
        movingPlatformSprite = Globals.Content.Load<Texture2D>("graphics/special_objects/moving_platform");
        spikeSprite = Globals.Content.Load<Texture2D>("graphics/special_objects/spike");
        doorSprite = Globals.Content.Load<Texture2D>("graphics/special_objects/door");
        lightstickSprite = Globals.Content.Load<Texture2D>("graphics/decoration/lightstick");
        elevatorSprite = Globals.Content.Load<Texture2D>("graphics/special_objects/elevatoropen");
        pixel = new Texture2D(Globals.graphics.GraphicsDevice, 1, 1);
        pixel.SetData(new[] { Color.White });
        var blank = new int[80, 400];
        for (int y = 0; y < 80; y++)
            for (int x = 0; x < 400; x++) blank[y, x] = -1;
        preview = new AutoTileMap(TerrainCatalog.Paths(), blank, new int[80, 400]);
        background = new BackgroundMap(preview.Width, preview.Height);
        snapshot = Capture();
        camera = new Camera { Origin = Vector2.Zero };
        RefreshLayout();
        FitLevel();
    }

    public override void Enter()
    {
        levelNameInput.Blur();
        levelNameInput.SyncInput();
        previousMouse = Mouse.GetState();
        isPanning = false;
        lastPaintCell = null;
        wheelRemainder = 0;
        CancelDrag();
    }

    public override void Leave()
    {
        levelNameInput.Blur();
        isPanning = false;
        CancelDrag();
    }

    public override void Update(GameTime gameTime)
    {
        RefreshLayout();
        bool wasEditingText = levelNameInput.IsFocused;
        levelNameInput.Update(gameTime);
        if (wasEditingText && (Globals.Input.KeyJustDown(Keys.Enter) || Globals.Input.KeyJustDown(Keys.Escape)
            || Globals.Input.KeyJustDown(Keys.Tab))) return;
        if (levelNameInput.IsFocused)
        {
            lastPaintCell = null;
            if ((Globals.Input.KeyDown(Keys.LeftControl) || Globals.Input.KeyDown(Keys.RightControl))
                && Globals.Input.KeyJustDown(Keys.S)) SaveLevel();
            return;
        }
        pathClickAge += (float)gameTime.ElapsedGameTime.TotalSeconds;
        preview.Update(gameTime);
        bool control = Globals.Input.KeyDown(Keys.LeftControl) || Globals.Input.KeyDown(Keys.RightControl);
        if (control && Globals.Input.KeyJustDown(Keys.S)) { SaveLevel(); return; }
        if (control && Globals.Input.KeyJustDown(Keys.Right)) ExpandLevel(40, 0);
        if (control && Globals.Input.KeyJustDown(Keys.Down)) ExpandLevel(0, 20);
        if (Globals.Input.KeyJustDown(Keys.F)) FitLevel();
        RefreshLayout();
        MouseState mouse = Mouse.GetState();
        Vector2 position = new Vector2(mouse.X, mouse.Y);
        pointer = new Point((int)MathF.Floor(position.X / uiScale), (int)MathF.Floor(position.Y / uiScale));
        bool clicked = mouse.LeftButton == ButtonState.Pressed && previousMouse.LeftButton == ButtonState.Released;
        bool rightClicked = mouse.RightButton == ButtonState.Pressed && previousMouse.RightButton == ButtonState.Released;
        bool overLevel = new Rectangle(sidebarOpen ? SidebarWidth : 0, 0,
            viewWidth - (sidebarOpen ? SidebarWidth : 0), viewHeight).Contains(pointer)
            && !ToggleButton.Contains(pointer) && !SaveButton.Contains(pointer)
            && !PlayButton.Contains(pointer) && !HomeButton.Contains(pointer)
            && !levelNameInput.Bounds.Contains(pointer);

        if (mouse.MiddleButton == ButtonState.Released) isPanning = false;
        else if (previousMouse.MiddleButton == ButtonState.Released && overLevel) isPanning = true;

        if (isPanning)
        {
            Vector2 previous = new Vector2(previousMouse.X, previousMouse.Y);
            if (position != previous) fittedView = false;
            camera.Position -= (position - previous) / camera.Zoom;
        }
        int scroll = mouse.ScrollWheelValue - previousMouse.ScrollWheelValue;
        if (scroll != 0 && overLevel)
        {
            wheelRemainder += scroll;
            int steps = wheelRemainder / 120;
            wheelRemainder %= 120;
            int nextZoom = Math.Clamp(zoomIndex + steps, 0, ZoomLevels.Length - 1);
            if (nextZoom != zoomIndex)
            {
                Vector2 worldUnderCursor = Vector2.Transform(position, Matrix.Invert(WorldTransform()));
                fittedView = false;
                zoomIndex = nextZoom;
                camera.Zoom = ZoomLevels[zoomIndex];
                camera.Position = worldUnderCursor - position / camera.Zoom;
            }
        }
        previousMouse = mouse;
        if (isPanning) { lastPaintCell = null; CancelDrag(); return; }
        if (clicked && sidebarOpen && pointer.X < SidebarWidth)
        {
            CancelDrag();
            lastPaintCell = null;
            if (pointer.X >= 64 && pointer.Y >= 0 && pointer.Y < bars.Length * 32)
            {
                palette = (Palette)(pointer.Y / 32);
                tool = Tool.None;
            }
            else
            {
                Tool selected = PaletteToolAt(pointer);
                if (selected != Tool.None)
                {
                    if (tool != selected)
                    {
                        bounceDirection = BounceDirection.Up;
                        manualSpringDirection = false;
                    }
                    tool = selected;
                    if (tool == Tool.Terrain)
                    {
                        selectedTerrain = TerrainAt(pointer);
                        showStatus("Terrain: " + TerrainCatalog.Names[selectedTerrain]);
                    }
                    if (tool == Tool.Background)
                    {
                        selectedBackground = BackgroundAt(pointer);
                        showStatus("Background: " + BackgroundMap.Names[selectedBackground] + " | Left-drag: paint | Right-drag: erase");
                    }
                    if (tool == Tool.Lightstick) showStatus("Lightstick | Place on a background tile | Drag to move | Right-click: delete");
                    if (tool == Tool.Elevator) showStatus("Elevator | Place a linked pair | Drag either door | Right-click: delete pair | Play: K at either door");
                    draggingFromPalette = tool != Tool.Background && tool != Tool.Terrain && tool != Tool.Platform && tool != Tool.MovingPlatform;
                    if (IsSpecialTool()) showStatus(tool == Tool.Platform
                        ? "Platform | Drag to paint horizontally from terrain | Right-drag: erase"
                        : tool == Tool.MovingPlatform ? "Moving platform | Click start and waypoints | Double-click: finish | Drag ends: resize | Right-click: undo"
                        : $"{SelectedObjectType()}: {bounceDirection} | R: rotate");
                }
            }
            return;
        }
        if (tool == Tool.MovingPlatform)
        {
            EditMovingPath(position, overLevel, clicked, rightClicked);
            if (overLevel) { lastPaintCell = null; return; }
        }
        if (Globals.Input.KeyJustDown(Keys.R)) RotateObject(position, overLevel);
        if (overLevel && Globals.Input.KeyDown(Keys.P))
        {
            lastPaintCell = null;
            if (clicked) TrySetSpawn(SpawnCellAt(position));
            return;
        }
        // Spawn, checkpoint and exit tools: press on the level, drag the ghost around, release to drop it.
        if (tool is Tool.Spawn or Tool.Checkpoint or Tool.Exit or Tool.BounceBall or Tool.Spring or Tool.Spike or Tool.Door or Tool.Lightstick or Tool.Elevator)
        {
            bool held = mouse.LeftButton == ButtonState.Pressed;
            if (held && (ghost.HasValue || ((clicked || draggingFromPalette) && overLevel)))
            {
                if (!ghost.HasValue && !draggingFromPalette && tool != Tool.Spawn)
                {
                    moving = ObjectUnder(position, SelectedObjectType());
                    if (moving != null)
                    {
                        bounceDirection = moving.Direction;
                        manualSpringDirection = tool == Tool.Spring;
                    }
                }
                var cell = SpawnCellAt(position);
                // Special sprites are 16px wide: center on a tile boundary so their
                // left/right edges align with the 8px grid instead of spanning three columns.
                if (tool != Tool.Spawn && tool != Tool.Spike && tool != Tool.Door && tool != Tool.Lightstick) cell.X += 4;
                ghost = cell;
                ghostDirection = bounceDirection;
                if (tool == Tool.Spring && !manualSpringDirection)
                {
                    TrySpringDirection(cell, out ghostDirection);
                    bounceDirection = ghostDirection;
                }
                ghostValid = tool == Tool.Spawn
                    ? LevelData.Capture(preview, cell).ValidSpawn()
                    : ObjectFits(cell, SelectedObjectType(), moving, ghostDirection);
                return;
            }
            if (ghost.HasValue)
            {
                // Dropping outside the level (e.g. on the sidebar) cancels the move.
                if (overLevel) Drop(ghost.Value);
                CancelDrag();
                return;
            }
            if (!held) draggingFromPalette = false;
            // Right-click deletes the checkpoint or exit flag under the cursor.
            if (rightClicked && overLevel && tool != Tool.Spawn)
            {
                var target = ObjectUnder(position, SelectedObjectType());
                if (target != null)
                {
                    if (target.Type == LevelObject.Elevator) objects.RemoveAll(item => item.Type == LevelObject.Elevator && item.PairId == target.PairId);
                    else objects.Remove(target);
                    snapshot = Capture();
                }
                return;
            }
        }
        if (tool == Tool.Platform && overLevel && scroll == 0
            && (mouse.LeftButton == ButtonState.Pressed || mouse.RightButton == ButtonState.Pressed))
        {
            var world = Vector2.Transform(position, Matrix.Invert(WorldTransform()));
            var cell = new Point((int)MathF.Floor(world.X / 8), (int)MathF.Floor(world.Y / 8));
            bool erase = mouse.RightButton == ButtonState.Pressed;
            var from = lastPaintCell.HasValue && lastErase == erase ? lastPaintCell.Value : cell;
            cell.Y = from.Y;
            PaintPlatformStroke(from, cell, !erase);
            lastPaintCell = cell;
            lastErase = erase;
            return;
        }
        if ((tool == Tool.Terrain || tool == Tool.Background) && overLevel && scroll == 0
            && (mouse.LeftButton == ButtonState.Pressed || mouse.RightButton == ButtonState.Pressed))
        {
            Vector2 world = Vector2.Transform(position, Matrix.Invert(WorldTransform()));
            var cell = new Point((int)MathF.Floor(world.X / 8), (int)MathF.Floor(world.Y / 8));
            bool erase = mouse.RightButton == ButtonState.Pressed;
            if (preview.InBounds(cell.X, cell.Y))
            {
                PaintStroke(lastPaintCell.HasValue && erase == lastErase ? lastPaintCell.Value : cell, cell, !erase);
                lastPaintCell = cell;
                lastErase = erase;
            }
            else lastPaintCell = null;
        }
        else lastPaintCell = null;
        if (!clicked) return;
        if (ToggleButton.Contains(pointer))
        {
            Vector2 center = camera.Position + ViewCenter() / camera.Zoom;
            sidebarOpen = !sidebarOpen;
            if (fittedView) FitLevel();
            else camera.Position = center - ViewCenter() / camera.Zoom;
            ToggleButton.UpdateRect((sidebarOpen ? SidebarWidth : 0) + 6, viewHeight / 2);
        }
        else if (SaveButton.Contains(pointer)) SaveLevel();
        else if (PlayButton.Contains(pointer))
        {
            if (pathDraft != null) showStatus("Double-click the final waypoint to finish the route first.");
            else switchMode(ModeId.Play);
        }
        else if (HomeButton.Contains(pointer)) switchMode(ModeId.Home);
    }

    public override void Draw()
    {
        RefreshLayout();
        Globals.graphics.GraphicsDevice.Clear(new Color(0, 174, 220));
        Globals.spriteBatch.Begin(samplerState: SamplerState.PointClamp, transformMatrix: WorldTransform());
        background.Draw();
        preview.Draw(ForegroundOpacity);
        Globals.spriteBatch.End();

        // Screen-space lines stay one pixel wide regardless of editor zoom.
        Globals.spriteBatch.Begin(samplerState: SamplerState.PointClamp);
        DrawGrid();
        Globals.spriteBatch.End();
        Globals.spriteBatch.Begin(samplerState: SamplerState.PointClamp, transformMatrix: WorldTransform());

        foreach (var item in objects)
        {
            // The picked-up checkpoint, or the exit being moved, fades while its ghost is dragged.
            bool dragged = item == pathOriginal || ghost.HasValue && (item == moving || (tool == Tool.Exit && item.Type == LevelObject.ExitFlag));
            DrawObject(item.Type, new Point(item.X, item.Y), ObjectFits(new Point(item.X, item.Y), item.Type, item, item.Direction),
                dragged ? 0.35f : 1f, item.Direction, item.WidthTiles);
            if (item.Type == LevelObject.MovingPlatform)
                MovingPlatform.DrawRoute(MovingPlatform.Route(item), Color.White * (dragged ? 0.35f : 1f) * ForegroundOpacity);
        }
        DrawElevatorConnections();
        if (pathDraft != null)
        {
            DrawObject(LevelObject.MovingPlatform, new Point(pathDraft.X, pathDraft.Y), true, 0.8f, widthTiles: pathDraft.WidthTiles);
            var route = MovingPlatform.Route(pathDraft);
            MovingPlatform.DrawRoute(route, Color.White * ForegroundOpacity);
            if (pathCursor.HasValue)
                MovingPlatform.DrawRoute(new List<Vector2> { route[^1], pathCursor.Value.ToVector2() },
                    pathCursorValid ? Color.White * 0.6f : Color.Red);
        }
        if (snapshot.HasSpawn)
            DrawPlayer(new Point(snapshot.SpawnX, snapshot.SpawnY), snapshot.ValidSpawn(),
                ghost.HasValue && tool == Tool.Spawn ? 0.35f : 1f);
        if (ghost.HasValue && tool == Tool.Spawn)
            DrawPlayer(ghost.Value, ghostValid, 0.75f);
        else if (ghost.HasValue)
            DrawObject(SelectedObjectType(), ghost.Value, ghostValid, 0.75f, ghostDirection);
        Globals.spriteBatch.End();

        // Draw the level outline in screen pixels so zoom cannot make it disappear.
        var transform = WorldTransform();
        Vector2 topLeft = Vector2.Transform(Vector2.Zero, transform);
        Vector2 bottomRight = Vector2.Transform(new Vector2(preview.Width * 8, preview.Height * 8), transform);
        int left = (int)MathF.Round(topLeft.X), top = (int)MathF.Round(topLeft.Y);
        int right = (int)MathF.Round(bottomRight.X), bottom = (int)MathF.Round(bottomRight.Y);
        const int thickness = 3;
        Globals.spriteBatch.Begin(samplerState: SamplerState.PointClamp);
        Fill(new Rectangle(left - thickness, top - thickness, right - left + thickness * 2, thickness), Color.White);
        Fill(new Rectangle(left - thickness, bottom, right - left + thickness * 2, thickness), Color.White);
        Fill(new Rectangle(left - thickness, top, thickness, bottom - top), Color.White);
        Fill(new Rectangle(right, top, thickness, bottom - top), Color.White);
        Globals.spriteBatch.End();

        // UI uses its own integer scale; terrain renders directly to the window.
        Globals.spriteBatch.Begin(samplerState: SamplerState.PointClamp, transformMatrix: Matrix.CreateScale(uiScale));
        if (sidebarOpen)
        {
            DrawPalette();
        }
        DrawButton(SaveButton, saveIcon, new Color(125, 151, 161));
        DrawButton(PlayButton, playIcon, new Color(225, 69, 59));
        DrawButton(HomeButton, homeIcon, new Color(149, 213, 112));
        levelNameInput.Draw();
        DrawButton(ToggleButton, arrowIcon, new Color(24, 100, 127),
            effects: sidebarOpen ? SpriteEffects.None : SpriteEffects.FlipHorizontally);
        Globals.spriteBatch.End();
    }

    // Replaces the canvas with a saved level; later saves overwrite that file.
    public void LoadLevel(string path)
    {
        var data = LevelData.Load(path);
        preview = new AutoTileMap(TerrainCatalog.Paths(), data.CreateGrid(false),
            data.CreateMaterials(TerrainCatalog.Names));
        background = new BackgroundMap(data.Width, data.Height, data.CreateBackgroundGrid());
        selectedTerrain = 0;
        selectedBackground = 0;
        spawn = data.HasSpawn ? new Point(data.SpawnX, data.SpawnY) : null;
        objects.Clear();
        objects.AddRange(data.Objects);
        CancelDrag();
        savePath = path;
        levelNameInput.Text = Path.GetFileNameWithoutExtension(path);
        snapshot = Capture();
        lastPaintCell = null;
        FitLevel();
    }

    public void SaveLevel()
    {
        if (pathDraft != null) { showStatus("Double-click the final waypoint to finish the route first."); return; }
        var data = Capture();
        if (!Elevator.ValidPairs(data.Objects)) { showStatus("Cannot save: each elevator needs a linked source and destination."); return; }
        if (!data.Objects.Exists(item => item.Type == LevelObject.ExitFlag))
        {
            showStatus("Cannot save: place an exit flag first.");
            return;
        }
        if (!data.ValidSpawn())
        {
            showStatus("Cannot save: set a clear player spawn or paint a platform first.");
            return;
        }
        foreach (var item in objects)
            if (!ObjectFits(new Point(item.X, item.Y), item.Type, item, item.Direction))
            {
                showStatus("Cannot save: an object is blocked or missing its required terrain/background support.");
                return;
            }
        string name = levelNameInput.Text.Trim();
        if (name.Length == 0) name = savePath == null ? "Level-" + DateTime.Now.ToString("yyyyMMdd-HHmmss-fff")
            : Path.GetFileNameWithoutExtension(savePath);
        string path;
        try { path = LevelData.NamedSavePath(name); }
        catch (ArgumentException error) { showStatus(error.Message); return; }
        if (File.Exists(path) && !string.Equals(path, savePath, StringComparison.OrdinalIgnoreCase))
        { showStatus("That level name already exists. Choose another name."); return; }
        data.Name = name;
        string previousPath = savePath;
        // Only this exact snapshot is saved after its fresh playtest reaches an exit.
        startSaveValidation(data, () => SaveClearedLevel(data, path, previousPath));
    }

    private bool SaveClearedLevel(LevelData data, string path, string previousPath)
    {
        try
        {
            data.SaveAs(path, previousPath);
            savePath = path;
            levelNameInput.Text = data.Name;
            showStatus("Congratulations! Level cleared and saved: " + path);
            return true;
        }
        catch (Exception error) when (error is IOException || error is UnauthorizedAccessException)
        {
            showStatus("Save failed: " + error.Message);
            return false;
        }
    }

    // Spawn points are the player's feet: centered on a tile, on that tile's bottom edge.
    private Point SpawnCellAt(Vector2 screen)
    {
        var world = Vector2.Transform(screen, Matrix.Invert(WorldTransform()));
        return new Point((int)MathF.Floor(world.X / 8) * 8 + 4, ((int)MathF.Floor(world.Y / 8) + 1) * 8);
    }

    private void TrySetSpawn(Point proposed)
    {
        if (LevelData.Capture(preview, proposed).ValidSpawn())
        {
            spawn = proposed;
            snapshot = Capture();
        }
        else showStatus("Spawn needs 8x12 pixels of clear space inside the level.");
    }

    private void CancelDrag()
    {
        pathClickAge = float.PositiveInfinity;
        pathDraft = pathOriginal = resizingPlatform = null;
        pathCursor = null;
        ghost = null;
        moving = null;
        draggingFromPalette = false;
    }

    private void Drop(Point feet)
    {
        if (tool == Tool.Spawn) { TrySetSpawn(feet); return; }
        if (!ghostValid)
        {
            showStatus(tool == Tool.Lightstick ? "Lightstick needs a background tile and clear space inside the level."
                : tool == Tool.Door ? "Door needs terrain directly above and below its doorway."
                : tool == Tool.Spike ? "Spike needs terrain, the top of a platform, or a solid moving-platform side behind its base."
                : tool == Tool.Spring
                ? "Spring needs clear space and solid terrain across its mounting base."
                : "Object needs clear space inside the level and can't share a spot.");
            return;
        }
        if (tool == Tool.Exit)
        {
            // Only one exit: placing it again moves the existing one.
            objects.RemoveAll(item => item.Type == LevelObject.ExitFlag);
            objects.Add(new LevelObject { Type = LevelObject.ExitFlag, X = feet.X, Y = feet.Y, Direction = ghostDirection });
        }
        else if (tool == Tool.Elevator && moving == null)
        {
            Point? destination = FindElevatorDestination(feet);
            if (!destination.HasValue) { showStatus("Elevator pair needs room for a second door nearby. Clear space first."); return; }
            string pairId = Guid.NewGuid().ToString("N");
            objects.Add(new LevelObject { Type = LevelObject.Elevator, X = feet.X, Y = feet.Y, PairId = pairId });
            objects.Add(new LevelObject { Type = LevelObject.Elevator, X = destination.Value.X, Y = destination.Value.Y,
                PairId = pairId, IsDestination = true });
            showStatus("Elevator pair created | Purple arrows connect both doors | Play: K at either door | Drag either door to move it");
        }
        else if (moving != null)
        {
            moving.X = feet.X;
            moving.Y = feet.Y;
            moving.Direction = ghostDirection;
        }
        else objects.Add(new LevelObject { Type = SelectedObjectType(), X = feet.X, Y = feet.Y, Direction = ghostDirection });
        snapshot = Capture();
    }

    private Point? FindElevatorDestination(Point source)
    {
        Rectangle sourceBounds = Elevator.Bounds(source);
        bool Fits(Point candidate) => !sourceBounds.Intersects(Elevator.Bounds(candidate))
            && ObjectFits(candidate, LevelObject.Elevator, null);
        foreach (int offset in new[] { 32, -32 })
        {
            Point candidate = new(source.X + offset, source.Y);
            if (Fits(candidate)) return candidate;
        }
        for (int radius = 2; radius <= 16; radius++)
            for (int dy = -radius; dy <= radius; dy++)
                for (int dx = -radius; dx <= radius; dx++)
                {
                    if (Math.Abs(dx) != radius && Math.Abs(dy) != radius) continue;
                    Point candidate = new(source.X + dx * 8, source.Y + dy * 8);
                    if (Fits(candidate)) return candidate;
                }
        return null;
    }

    private void DrawElevatorConnections()
    {
        foreach (var source in objects)
        {
            if (source.Type != LevelObject.Elevator || source.IsDestination) continue;
            var destination = objects.Find(item => item.Type == LevelObject.Elevator && item.PairId == source.PairId && item.IsDestination);
            if (destination == null) continue;
            Point SourceFeet(LevelObject item) => item == moving && ghost.HasValue ? ghost.Value : new Point(item.X, item.Y);
            Vector2 from = Elevator.Connector(SourceFeet(source)), to = Elevator.Connector(SourceFeet(destination));
            Vector2 delta = to - from;
            if (delta.LengthSquared() == 0) continue;
            void Line(Vector2 a, Vector2 b)
            {
                Vector2 difference = b - a;
                Globals.spriteBatch.Draw(pixel, a, null, PicoPallete.lavender,
                    MathF.Atan2(difference.Y, difference.X), Vector2.Zero,
                    new Vector2(difference.Length(), Math.Max(1f, 1f / camera.Zoom)), SpriteEffects.None, 0);
            }
            Line(from, to);
            Vector2 direction = Vector2.Normalize(delta), normal = new(-direction.Y, direction.X);
            Vector2 arrow = Vector2.Lerp(from, to, 0.65f);
            Line(arrow - direction * 5 + normal * 3, arrow);
            Line(arrow - direction * 5 - normal * 3, arrow);
            Vector2 reverseArrow = Vector2.Lerp(from, to, 0.35f);
            Line(reverseArrow + direction * 5 + normal * 3, reverseArrow);
            Line(reverseArrow + direction * 5 - normal * 3, reverseArrow);
        }
    }

    private static Rectangle ObjectBounds(Point feet, string type, BounceDirection direction, int widthTiles, bool collision = false) =>
        type == LevelObject.Platform ? Platform.Bounds(feet, widthTiles)
            : type == LevelObject.MovingPlatform ? MovingPlatform.Bounds(feet, widthTiles)
            : type == LevelObject.Door ? Door.Bounds(feet)
            : ObjectRotation.Bounds(feet, type, direction, collision);

    private bool MovingPointFits(Point feet, LevelObject ignore, int widthTiles = 3)
    {
        var bounds = MovingPlatform.Bounds(feet, widthTiles);
        if (bounds.Left < 0 || bounds.Top < 0 || bounds.Right > preview.Width * 8 || bounds.Bottom > preview.Height * 8) return false;
        for (int y = bounds.Top / 8; y <= (bounds.Bottom - 1) / 8; y++)
            for (int x = bounds.Left / 8; x <= (bounds.Right - 1) / 8; x++)
                if (preview.Occupied(x, y)) return false;
        foreach (var item in objects)
            if (item != ignore && item.Type == LevelObject.Platform
                && Platform.Bounds(new Point(item.X, item.Y), item.WidthTiles).Intersects(bounds)) return false;
        return true;
    }

    private bool MovingSegmentFits(Point from, Point to, LevelObject ignore, int widthTiles = 3)
    {
        int dx = to.X - from.X, dy = to.Y - from.Y;
        if (dx != 0 && dy != 0 && Math.Abs(dx) != Math.Abs(dy)) return false;
        int steps = Math.Max(Math.Abs(dx), Math.Abs(dy));
        for (int i = 0; i <= steps; i++)
        {
            var point = new Point(from.X + Math.Sign(dx) * i, from.Y + Math.Sign(dy) * i);
            if (!MovingPointFits(point, ignore, widthTiles)) return false;
            if (i > 0 && dx != 0 && dy != 0
                && !MovingPointFits(new Point(point.X, point.Y - Math.Sign(dy)), ignore, widthTiles)) return false;
        }
        return true;
    }

    private bool MovingRouteFits(LevelObject item)
    {
        var route = MovingPlatform.Route(item);
        if (route.Count < 2 || !float.IsFinite(item.MoveSpeed) || item.MoveSpeed <= 0 || item.MoveSpeed > 240) return false;
        for (int i = 1; i < route.Count; i++)
            if (!MovingSegmentFits(route[i - 1].ToPoint(), route[i].ToPoint(), item, item.WidthTiles)) return false;
        return true;
    }

    private bool ResizeMovingPlatform(Vector2 screen, bool overLevel, bool clicked)
    {
        Vector2 world = Vector2.Transform(screen, Matrix.Invert(WorldTransform()));
        if (resizingPlatform == null && clicked && overLevel)
        {
            var candidates = pathDraft != null ? new List<LevelObject> { pathDraft } : objects;
            foreach (var item in candidates)
            {
                if (item.Type != LevelObject.MovingPlatform) continue;
                var bounds = MovingPlatform.Bounds(new Point(item.X, item.Y), item.WidthTiles);
                if (world.Y < bounds.Top - 2 || world.Y > bounds.Bottom + 2) continue;
                if (Math.Abs(world.X - bounds.Left) <= 3 || Math.Abs(world.X - bounds.Right) <= 3)
                {
                    resizingPlatform = item;
                    resizeStartWidth = item.WidthTiles;
                    resizeStartDistance = Math.Abs(world.X - item.X);
                    pathClickAge = float.PositiveInfinity;
                    break;
                }
            }
        }
        if (resizingPlatform == null) return false;
        if (Mouse.GetState().LeftButton == ButtonState.Released)
        {
            if (resizingPlatform != pathDraft) snapshot = Capture();
            resizingPlatform = null;
            return true;
        }
        if (!overLevel) return true;
        int previousWidth = resizingPlatform.WidthTiles;
        int width = Math.Clamp(resizeStartWidth + (int)MathF.Round(
            (Math.Abs(world.X - resizingPlatform.X) - resizeStartDistance) / 4), 3, Math.Max(3, preview.Width));
        resizingPlatform.WidthTiles = width;
        bool fits = resizingPlatform.Waypoints.Count == 0
            ? MovingPointFits(new Point(resizingPlatform.X, resizingPlatform.Y), resizingPlatform, width)
            : MovingRouteFits(resizingPlatform);
        if (!fits)
        {
            resizingPlatform.WidthTiles = previousWidth;
            showStatus("Platform length blocked along its route.");
        }
        else showStatus($"Moving platform: {width} tiles | Drag either end to resize | Double-click final waypoint to finish");
        return true;
    }

    private void EditMovingPath(Vector2 screen, bool overLevel, bool clicked, bool rightClicked)
    {
        pathCursor = null;
        if (ResizeMovingPlatform(screen, overLevel, clicked)) return;
        bool doubleClicked = clicked && overLevel && pathClickAge <= 0.35f
            && Vector2.DistanceSquared(screen, pathClickPosition) <= 36f;
        if (pathDraft != null && (doubleClicked || Globals.Input.KeyJustDown(Keys.Enter)))
        {
            if (!MovingRouteFits(pathDraft)) { showStatus("Route needs at least two points and clear space along every segment."); return; }
            if (pathOriginal != null) objects.Remove(pathOriginal);
            objects.Add(pathDraft);
            pathDraft = pathOriginal = null;
            pathClickAge = float.PositiveInfinity;
            snapshot = Capture();
            showStatus("Moving platform route finished | Click a platform to edit its route.");
            return;
        }
        if (pathDraft != null && (rightClicked || Globals.Input.KeyJustDown(Keys.Back)))
        {
            pathClickAge = float.PositiveInfinity;
            if (pathDraft.Waypoints.Count > 0) pathDraft.Waypoints.RemoveAt(pathDraft.Waypoints.Count - 1);
            else { pathDraft = pathOriginal = null; }
            return;
        }
        if (!overLevel) return;
        Point feet = SpawnCellAt(screen);
        if (pathDraft == null)
        {
            if (rightClicked)
            {
                var target = ObjectUnder(screen, LevelObject.MovingPlatform);
                if (target != null) { objects.Remove(target); snapshot = Capture(); }
                return;
            }
            if (!clicked) return;
            var existing = ObjectUnder(screen, LevelObject.MovingPlatform);
            if (existing != null)
            {
                pathOriginal = existing;
                pathDraft = new LevelObject { Type = existing.Type, X = existing.X, Y = existing.Y,
                    WidthTiles = existing.WidthTiles, MoveSpeed = existing.MoveSpeed,
                    Waypoints = existing.Waypoints?.ConvertAll(p => new LevelWaypoint { X = p.X, Y = p.Y }) ?? new() };
            }
            else if (MovingPointFits(feet, null))
                pathDraft = new LevelObject { Type = LevelObject.MovingPlatform, X = feet.X, Y = feet.Y, WidthTiles = 3 };
            else showStatus("Moving platform needs clear space inside the level.");
            return;
        }
        var points = MovingPlatform.Route(pathDraft);
        Point last = points[^1].ToPoint();
        feet = MovingPlatform.SnapWaypoint(last, feet);
        pathCursor = feet;
        pathCursorValid = feet != last && MovingSegmentFits(last, feet, pathOriginal, pathDraft.WidthTiles);
        if (clicked && pathCursorValid)
        {
            pathDraft.Waypoints.Add(new LevelWaypoint { X = feet.X, Y = feet.Y });
            pathClickAge = 0;
            pathClickPosition = screen;
        }
        else if (clicked) showStatus("Path segment blocked: use a clear horizontal, vertical or diagonal route.");
    }

    private void PaintPlatformStroke(Point from, Point to, bool solid)
    {
        var cells = PlatformLayout.Cells(objects);
        int left = Math.Min(from.X, to.X), right = Math.Max(from.X, to.X);
        for (int x = left; x <= right; x++)
        {
            var cell = new Point(x, from.Y);
            if (!preview.InBounds(x, from.Y)) continue;
            if (!solid) cells.Remove(cell);
            else if (!preview.Occupied(x, from.Y))
            {
                var bounds = new Rectangle(x * 8, from.Y * 8, 8, 8);
                bool blocked = false;
                foreach (var item in objects)
                    if (item.Type != LevelObject.Platform && ObjectBounds(new Point(item.X, item.Y), item.Type,
                        item.Direction, item.WidthTiles, true).Intersects(bounds)) { blocked = true; break; }
                if (!blocked) cells.Add(cell);
            }
        }
        PlatformLayout.Rebuild(objects, cells, preview.Width, preview.Height, preview.Occupied);
        snapshot = Capture();
    }

    private string SelectedObjectType() => tool switch
    {
        Tool.BounceBall => LevelObject.BounceBall,
        Tool.Spring => LevelObject.Spring,
        Tool.Platform => LevelObject.Platform,
        Tool.MovingPlatform => LevelObject.MovingPlatform,
        Tool.Spike => LevelObject.Spike,
        Tool.Door => LevelObject.Door,
        Tool.Lightstick => LevelObject.Lightstick,
        Tool.Elevator => LevelObject.Elevator,
        Tool.Exit => LevelObject.ExitFlag,
        _ => LevelObject.Checkpoint
    };

    private bool IsSpecialTool() => tool is Tool.Checkpoint or Tool.Exit or Tool.BounceBall or Tool.Spring or Tool.Platform or Tool.MovingPlatform or Tool.Spike or Tool.Door;

    private void RotateObject(Vector2 screen, bool overLevel)
    {
        // A dragged preview has priority; otherwise rotate the topmost object under the cursor.
        if (!ghost.HasValue && !draggingFromPalette && overLevel)
        {
            Vector2 world = Vector2.Transform(screen, Matrix.Invert(WorldTransform()));
            for (int i = objects.Count - 1; i >= 0; i--)
            {
                var item = objects[i];
                var feet = new Point(item.X, item.Y);
                if (!ObjectBounds(feet, item.Type, item.Direction, item.WidthTiles).Contains(world)) continue;
                if (item.Type is LevelObject.Platform or LevelObject.MovingPlatform or LevelObject.Door or LevelObject.Lightstick or LevelObject.Elevator) return;
                var next = ObjectRotation.Next(item.Type, item.Direction);
                if (!ObjectFits(feet, item.Type, item, next))
                {
                    showStatus("Rotation blocked: clear space and a supported spring base are required.");
                    return;
                }
                item.Direction = next;
                snapshot = Capture();
                showStatus($"{item.Type}: {next} | R: rotate");
                return;
            }
        }
        if (!IsSpecialTool() || tool is Tool.Platform or Tool.MovingPlatform or Tool.Door) return;
        bounceDirection = ObjectRotation.Next(SelectedObjectType(), bounceDirection);
        ghostDirection = bounceDirection;
        if (tool == Tool.Spring) manualSpringDirection = true;
        if (ghost.HasValue)
            ghostValid = ObjectFits(ghost.Value, SelectedObjectType(), moving, ghostDirection);
        showStatus($"{SelectedObjectType()}: {bounceDirection} | R: rotate");
    }

    private static bool IsLauncher(string type) =>
        type == LevelObject.BounceBall || type == LevelObject.Spring;

    private Texture2D ObjectTexture(string type) => type switch
    {
        LevelObject.BounceBall => bounceBallSprite,
        LevelObject.Spring => springSprite,
        LevelObject.Platform => platformSprite,
        LevelObject.MovingPlatform => movingPlatformSprite,
        LevelObject.Spike => spikeSprite,
        LevelObject.ExitFlag => exitSprite,
        _ => checkpointSprite
    };

    private bool SpringSupported(Point feet, BounceDirection direction)
    {
        int left = (feet.X - 8) / 8, right = (feet.X + 7) / 8;
        int top = (feet.Y - 16) / 8, bottom = (feet.Y - 1) / 8;
        if (direction == BounceDirection.Up)
        {
            for (int x = left; x <= right; x++)
                if (!preview.Occupied(x, feet.Y / 8)) return false;
            return true;
        }
        if (direction == BounceDirection.Down)
        {
            for (int x = left; x <= right; x++)
                if (!preview.Occupied(x, top - 1)) return false;
            return true;
        }
        if (direction is BounceDirection.Right or BounceDirection.Left)
        {
            int wall = direction == BounceDirection.Right ? left - 1 : right + 1;
            for (int y = top; y <= bottom; y++)
                if (!preview.Occupied(wall, y)) return false;
            return true;
        }
        return false;
    }

    private bool TrySpringDirection(Point feet, out BounceDirection direction)
    {
        // Prefer a floor mount at a corner, then walls or a ceiling.
        foreach (var candidate in new[] { BounceDirection.Up, BounceDirection.Right, BounceDirection.Left, BounceDirection.Down })
            if (SpringSupported(feet, candidate)) { direction = candidate; return true; }
        direction = BounceDirection.Up;
        return false;
    }

    private void DrawDirection(Point feet, BounceDirection direction, float alpha)
    {
        Vector2 vector = ObjectRotation.Vector(direction);
        Point axis = new(Math.Sign(vector.X), Math.Sign(vector.Y));
        var center = new Point(feet.X, feet.Y - 8);
        Color color = PicoPallete.dark_blue * alpha;
        for (int i = -2; i <= 2; i++)
            Fill(new Rectangle(center.X + axis.X * i, center.Y + axis.Y * i, 1, 1), color);
        for (int side = -1; side <= 1; side += 2)
            Fill(new Rectangle(center.X + axis.X - axis.Y * side,
                center.Y + axis.Y + axis.X * side, 1, 1), color);
    }

    // Hit testing uses the actual sprite size for each object.
    private LevelObject ObjectUnder(Vector2 screen, string type)
    {
        var world = Vector2.Transform(screen, Matrix.Invert(WorldTransform()));
        for (int i = objects.Count - 1; i >= 0; i--)
        {
            var item = objects[i];
            if (item.Type == type && ObjectBounds(new Point(item.X, item.Y), type, item.Direction, item.WidthTiles).Contains(world)) return item;
        }
        return null;
    }

    // An object needs its rotated hitbox clear of terrain and inside the level, and can't sit on
    // another object's spot. The exit ignores the old exit, since dropping it moves that one.
    private bool ObjectFits(Point feet, string type, LevelObject ignore, BounceDirection direction = BounceDirection.Up, int? widthTiles = null)
    {
        if (type == LevelObject.MovingPlatform && ignore != null && !MovingRouteFits(ignore)) return false;
        Rectangle bounds = ObjectBounds(feet, type, direction, widthTiles ?? ignore?.WidthTiles ?? 1, true);
        if (bounds.Left < 0 || bounds.Right > preview.Width * 8 || bounds.Top < 0 || bounds.Bottom > preview.Height * 8) return false;
        for (int y = bounds.Top / 8; y <= (bounds.Bottom - 1) / 8; y++)
            for (int x = bounds.Left / 8; x <= (bounds.Right - 1) / 8; x++)
                if (preview.Occupied(x, y)) return false;
        if (type == LevelObject.Door && !Door.Supported(feet, preview.Occupied)) return false;
        if (type == LevelObject.Spring && !SpringSupported(feet, direction)) return false;
        if (type == LevelObject.Spike && !Spike.Supported(feet, direction, preview.Occupied, objects)) return false;
        if (type == LevelObject.Lightstick && !Lightstick.Supported(feet, background.Occupied)) return false;
        foreach (var item in objects)
        {
            if (item == ignore || (type == LevelObject.ExitFlag && item.Type == LevelObject.ExitFlag)) continue;
            if ((type == LevelObject.Elevator || item.Type == LevelObject.Elevator)
                && ObjectBounds(new Point(item.X, item.Y), item.Type, item.Direction, item.WidthTiles, true).Intersects(bounds)) return false;
            if (item.X == feet.X && item.Y == feet.Y) return false;
        }
        return true;
    }

    // First frame of the object's sprite at its feet; blocked spots are tinted red.
    private void DrawObject(string type, Point feet, bool valid, float alpha, BounceDirection direction = BounceDirection.Up, int widthTiles = 6)
    {
        alpha *= ForegroundOpacity;
        if (type == LevelObject.Elevator)
        {
            Globals.spriteBatch.Draw(elevatorSprite, Elevator.Bounds(feet), new Rectangle(0, 0, 16, 24),
                (valid ? Color.White : new Color(255, 120, 120)) * alpha);
            return;
        }
        if (type == LevelObject.Lightstick)
        {
            if (valid) Lightstick.DrawGlow(feet.ToVector2(), alpha);
            Globals.spriteBatch.Draw(lightstickSprite, Lightstick.Bounds(feet),
                (valid ? Color.White : new Color(255, 120, 120)) * alpha);
            return;
        }
        if (type == LevelObject.Door)
        {
            Globals.spriteBatch.Draw(doorSprite, feet.ToVector2(), new Rectangle(0, 0, 16, 16),
                (valid ? Color.White : new Color(255, 120, 120)) * alpha, 0, new Vector2(4, 16), 1f, SpriteEffects.None, 0);
            return;
        }
        if (type == LevelObject.Spike)
        {
            var bounds = ObjectRotation.Bounds(feet, type, direction);
            Globals.spriteBatch.Draw(spikeSprite, bounds.Center.ToVector2(), null,
                (valid ? Color.White : new Color(255, 120, 120)) * alpha,
                ObjectRotation.Angle(direction), new Vector2(4, 4), 1f, SpriteEffects.None, 0);
            return;
        }
        if (type == LevelObject.MovingPlatform)
        {
            var bounds = MovingPlatform.Bounds(feet, widthTiles);
            MovingPlatform.DrawPieces(movingPlatformSprite, bounds,
                (valid ? Color.White : new Color(255, 120, 120)) * alpha);
            if (tool == Tool.MovingPlatform)
            {
                Fill(new Rectangle(bounds.Left - 1, bounds.Top + 2, 3, 4), Color.White * alpha);
                Fill(new Rectangle(bounds.Right - 2, bounds.Top + 2, 3, 4), Color.White * alpha);
            }
            return;
        }
        if (type == LevelObject.Platform)
        {
            var bounds = Platform.Bounds(feet, widthTiles);
            Platform.DrawPieces(platformSprite, bounds,
                (valid ? Color.White : new Color(255, 120, 120)) * alpha,
                preview.Occupied(bounds.Left / 8 - 1, bounds.Top / 8),
                preview.Occupied(bounds.Right / 8, bounds.Top / 8));
            return;
        }
        if (!valid) Fill(ObjectRotation.Bounds(feet, type, direction, true), Color.Red * 0.5f * alpha);
        Texture2D texture = ObjectTexture(type);
        int height = IsLauncher(type) ? 16 : 32;
        Rectangle spriteBounds = ObjectRotation.Bounds(feet, type, direction);
        Globals.spriteBatch.Draw(texture, spriteBounds.Center.ToVector2(), new Rectangle(0, 0, 16, height),
            (valid ? Color.White : new Color(255, 120, 120)) * alpha,
            ObjectRotation.Angle(direction), new Vector2(8, height / 2), 1f, SpriteEffects.None, 0);
        if (type == LevelObject.BounceBall) DrawDirection(feet, direction, alpha);
    }

    // Idle frame drawn at the feet position; invalid spots are tinted red.
    private void DrawPlayer(Point feet, bool valid, float alpha)
    {
        alpha *= ForegroundOpacity;
        if (!valid) Fill(new Rectangle(feet.X - 4, feet.Y - 12, 8, 12), Color.Red * 0.5f * alpha);
        Globals.spriteBatch.Draw(playerSprite, new Vector2(feet.X - 8, feet.Y - 16), new Rectangle(0, 0, 16, 16),
            (valid ? Color.White : new Color(255, 120, 120)) * alpha);
    }

    private int TerrainAt(Point point)
    {
        if (palette != Palette.Terrain) return -1;
        for (int i = 0; i < terrainButtons.Length; i++)
            if (terrainButtons[i].Contains(point)) return i;
        return -1;
    }

    private int BackgroundAt(Point point)
    {
        if (palette != Palette.Background) return -1;
        for (int i = 0; i < backgroundButtons.Length; i++)
            if (backgroundButtons[i].Contains(point)) return i;
        return -1;
    }

    private Tool PaletteToolAt(Point point)
    {
        if (BackgroundAt(point) >= 0) return Tool.Background;
        if (TerrainAt(point) >= 0) return Tool.Terrain;
        if (palette == Palette.Decoration && LightstickButton.Contains(point)) return Tool.Lightstick;
        if (palette == Palette.Sprites && SpawnButton.Contains(point)) return Tool.Spawn;
        if (palette == Palette.Special && CheckpointButton.Contains(point)) return Tool.Checkpoint;
        if (palette == Palette.Special && ExitButton.Contains(point)) return Tool.Exit;
        if (palette == Palette.Special && BounceBallButton.Contains(point)) return Tool.BounceBall;
        if (palette == Palette.Special && SpringButton.Contains(point)) return Tool.Spring;
        if (palette == Palette.Special && PlatformButton.Contains(point)) return Tool.Platform;
        if (palette == Palette.Special && MovingPlatformButton.Contains(point)) return Tool.MovingPlatform;
        if (palette == Palette.Special && SpikeButton.Contains(point)) return Tool.Spike;
        if (palette == Palette.Special && DoorButton.Contains(point)) return Tool.Door;
        if (palette == Palette.Special && ElevatorButton.Contains(point)) return Tool.Elevator;
        return Tool.None;
    }

    private void DrawPalette()
    {
        // Extend only the panel body using the artwork's bottom row; tabs keep their native size.
        if (viewHeight > 180)
            Globals.spriteBatch.Draw(bars[(int)palette], new Rectangle(0, 180, 64, viewHeight - 180),
                new Rectangle(0, 179, 64, 1), Color.White);
        Globals.spriteBatch.Draw(bars[(int)palette], new Rectangle(0, 0, 64, 180),
            new Rectangle(0, 0, 64, 180), Color.White);
        for (int i = 0; i < bars.Length; i++)
        {
            var tab = new Rectangle(64, i * 32, 16, 32);
            Globals.spriteBatch.Draw(bars[i], tab, tab, Color.White);
            if (tab.Contains(pointer)) Fill(tab, Color.White * 0.12f);
        }
        for (int row = 0; row < (palette == Palette.Special ? 5 : 3); row++)
            for (int col = 0; col < 2; col++)
            {
                var slot = new Rectangle(3 + col * 32, 8 + row * 31, 27, 27);
                Tool item = PaletteToolAt(slot.Center);
                bool selected = item != Tool.None && tool == item
                    && (item != Tool.Terrain || row * 2 + col == selectedTerrain)
                    && (item != Tool.Background || row * 2 + col == selectedBackground);
                Color border = selected ? Color.Yellow
                    : item != Tool.None && slot.Contains(pointer) ? Color.White : new Color(194, 195, 199);
                Fill(slot, border);
                Fill(new Rectangle(slot.X + 1, slot.Y + 1, slot.Width - 2, slot.Height - 2),
                    new Color(95, 87, 79));
            }
        if (palette == Palette.Terrain)
        {
            for (int i = 0; i < tilesets.Length; i++)
                DrawPaletteItem(tilesets[i], new Rectangle(0, 0, 8, 8), terrainButtons[i].Rect);
        }
        else if (palette == Palette.Background)
        {
            for (int i = 0; i < backgroundTextures.Length; i++)
                DrawPaletteItem(backgroundTextures[i], new Rectangle(0, 0, 8, 8), backgroundButtons[i].Rect);
        }
        else if (palette == Palette.Decoration)
            DrawPaletteItem(lightstickSprite, new Rectangle(0, 0, 8, 8), LightstickButton.Rect);
        else if (palette == Palette.Sprites)
            DrawPaletteItem(playerSprite, new Rectangle(0, 0, 16, 16), SpawnButton.Rect);
        else if (palette == Palette.Special)
        {
            DrawPaletteItem(checkpointSprite, new Rectangle(0, 0, 16, 32), CheckpointButton.Rect);
            DrawPaletteItem(exitSprite, new Rectangle(0, 0, 16, 32), ExitButton.Rect);
            DrawPaletteItem(bounceBallSprite, new Rectangle(0, 0, 16, 16), BounceBallButton.Rect);
            DrawPaletteItem(springSprite, new Rectangle(0, 0, 16, 16), SpringButton.Rect);
            DrawPaletteItem(platformSprite, new Rectangle(24, 0, 8, 8), PlatformButton.Rect);
            DrawPaletteItem(movingPlatformSprite, new Rectangle(0, 0, 24, 8), MovingPlatformButton.Rect);
            DrawPaletteItem(spikeSprite, new Rectangle(0, 0, 8, 8), SpikeButton.Rect);
            DrawPaletteItem(doorSprite, new Rectangle(0, 0, 16, 16), DoorButton.Rect);
            DrawPaletteItem(elevatorSprite, new Rectangle(0, 0, 16, 24), ElevatorButton.Rect);
        }
    }

    private void DrawPaletteItem(Texture2D texture, Rectangle source, Rectangle slot)
    {
        // Integer scaling keeps small pixel-art thumbnails sharp and inside their slots.
        float scale = Math.Max(0.5f, MathF.Floor(Math.Min(
            (slot.Width - 4f) / source.Width, (slot.Height - 4f) / source.Height)));
        int width = (int)(source.Width * scale), height = (int)(source.Height * scale);
        var destination = new Rectangle(slot.Center.X - width / 2, slot.Center.Y - height / 2, width, height);
        Globals.spriteBatch.Draw(texture, destination, source, Color.White);
    }

    private void DrawGrid()
    {
        float spacing = 8 * camera.Zoom;
        // Subpixel cells cannot be distinguished; avoid covering the level in white at overview zoom.
        if (spacing < 4) return;
        Matrix transform = WorldTransform();
        float originX = transform.M41, originY = transform.M42;
        int left = Math.Max(sidebarOpen ? (int)(SidebarWidth * uiScale) : 0,
            (int)MathF.Ceiling(originX));
        int top = Math.Max(0, (int)MathF.Ceiling(originY));
        int right = Math.Min(windowWidth, (int)MathF.Ceiling(originX + preview.Width * spacing));
        int bottom = Math.Min(windowHeight, (int)MathF.Ceiling(originY + preview.Height * spacing));
        if (right <= left || bottom <= top) return;
        Color color = Color.White * 0.3f;
        int firstColumn = Math.Max(0, (int)MathF.Ceiling((left - originX) / spacing));
        int lastColumn = Math.Min(preview.Width, (int)MathF.Floor((right - originX) / spacing));
        for (int column = firstColumn; column <= lastColumn; column++)
        {
            int x = (int)MathF.Round(originX + column * spacing);
            if (x < right) Fill(new Rectangle(x, top, 1, bottom - top), color);
        }
        int firstRow = Math.Max(0, (int)MathF.Ceiling((top - originY) / spacing));
        int lastRow = Math.Min(preview.Height, (int)MathF.Floor((bottom - originY) / spacing));
        for (int row = firstRow; row <= lastRow; row++)
        {
            int y = (int)MathF.Round(originY + row * spacing);
            if (y < bottom) Fill(new Rectangle(left, y, right - left, 1), color);
        }
    }

    private void Fill(Rectangle rectangle, Color color) => Globals.spriteBatch.Draw(pixel, rectangle, color);

    private Matrix WorldTransform()
    {
        return WindowRendering.PixelAligned(camera.Transform());
    }

    private void RefreshLayout()
    {
        var bounds = Globals.graphics.GraphicsDevice.PresentationParameters.Bounds;
        if (bounds.Width <= 0 || bounds.Height <= 0 || (bounds.Width == windowWidth && bounds.Height == windowHeight)) return;
        Vector2 center = camera.Position + ViewCenter() / camera.Zoom;
        bool hadLayout = windowWidth > 0;
        windowWidth = bounds.Width;
        windowHeight = bounds.Height;
        uiScale = WindowRendering.ScaleFor(windowWidth, windowHeight);
        viewWidth = (int)MathF.Ceiling(windowWidth / uiScale);
        viewHeight = (int)MathF.Ceiling(windowHeight / uiScale);
        if (hadLayout)
        {
            if (fittedView) FitLevel();
            else camera.Position = center - ViewCenter() / camera.Zoom;
        }
        SaveButton.UpdateRect(viewWidth - 76, 16);
        PlayButton.UpdateRect(viewWidth - 48, 16);
        HomeButton.UpdateRect(viewWidth - 20, 16);
        ToggleButton.UpdateRect((sidebarOpen ? SidebarWidth : 0) + 6, viewHeight / 2);
        int nameLeft = (sidebarOpen ? SidebarWidth : 0) + 8;
        int nameWidth = Math.Max(36, Math.Min(144, viewWidth - nameLeft - 92));
        levelNameInput.Collider.Resize(nameWidth, 24);
        levelNameInput.Collider.UpdateRect(nameLeft + nameWidth / 2, 16);
        isPanning = false;
        lastPaintCell = null;
    }

    private Vector2 ViewCenter()
    {
        float left = (sidebarOpen ? SidebarWidth + 16 : 16) * uiScale;
        return new Vector2((left + windowWidth - 8 * uiScale) / 2,
            (36 * uiScale + windowHeight - 8 * uiScale) / 2);
    }

    public void Dispose() => levelNameInput.Dispose();

    private void FitLevel()
    {
        float left = (sidebarOpen ? SidebarWidth + 16 : 16) * uiScale;
        float fit = Math.Min(Math.Max(1, windowWidth - left - 8 * uiScale) / (preview.Width * 8),
            Math.Max(1, windowHeight - 44 * uiScale) / (preview.Height * 8));
        zoomIndex = 0;
        while (zoomIndex < ZoomLevels.Length - 1 && ZoomLevels[zoomIndex + 1] <= fit) zoomIndex++;
        camera.Zoom = ZoomLevels[zoomIndex];
        camera.Position = new Vector2(preview.Width * 4, preview.Height * 4) - ViewCenter() / camera.Zoom;
        fittedView = true;
        lastPaintCell = null;
    }

    private void ExpandLevel(int columns, int rows)
    {
        try
        {
            preview.Expand(preview.Width + columns, preview.Height + rows);
            background.Expand(preview.Width, preview.Height);
            snapshot = Capture();
            lastPaintCell = null;
            FitLevel();
            showStatus($"Level: {preview.Width} x {preview.Height} tiles | Ctrl+Right: wider | Ctrl+Down: taller | F: fit");
        }
        catch (ArgumentOutOfRangeException error) { showStatus(error.Message); }
    }

    private void PaintStroke(Point from, Point to, bool solid)
    {
        // Fill gaps when the pointer crosses multiple cells between updates.
        int dx = Math.Abs(to.X - from.X), dy = -Math.Abs(to.Y - from.Y);
        int sx = from.X < to.X ? 1 : -1, sy = from.Y < to.Y ? 1 : -1;
        int error = dx + dy;
        while (true)
        {
            if (tool == Tool.Background) background.Paint(from.X, from.Y, solid ? selectedBackground : -1);
            else preview.Paint(from.X, from.Y, solid, selectedTerrain);
            if (from == to) { snapshot = Capture(); break; }
            int twice = 2 * error;
            if (twice >= dy) { error += dy; from.X += sx; }
            if (twice <= dx) { error += dx; from.Y += sy; }
        }
    }

    private void DrawButton(CollisionRect button, Texture2D icon, Color background, bool enabled = true,
        SpriteEffects effects = SpriteEffects.None)
    {
        Rectangle bounds = button.Rect;
        bool hovered = enabled && button.Contains(pointer);
        button.Draw(hovered ? Color.White : new Color(24, 48, 63));
        var inset = new Rectangle(bounds.X + 1, bounds.Y + 1, bounds.Width - 2, bounds.Height - 2);
        Fill(inset, hovered ? Color.Lerp(background, Color.White, 0.2f) : background);
        float scale = Math.Min(1f, Math.Min((float)(bounds.Width - 2) / icon.Width,
            (float)(bounds.Height - 2) / icon.Height));
        Globals.spriteBatch.Draw(icon, button.Center, null,
            enabled ? Color.White : new Color(150, 150, 150), 0,
            new Vector2(icon.Width / 2f, icon.Height / 2f), scale, effects, 0);
    }
}
