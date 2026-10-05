using System;
using System.Collections.Generic;
using System.IO;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using NodeTesting.models;
using uptown.SpecialObjects;

namespace uptown.Modes;

// Terrain painting, object placement, and category palettes.
public sealed class EditorMode : GameMode
{
    private AutoTileMap preview;
    private readonly Action<string> showStatus;
    private readonly Action<LevelData, Func<bool>> startSaveValidation;
    private Point? spawn;
    private string savePath;
    private LevelData snapshot;
    public LevelData Capture() => LevelData.Capture(preview, spawn, objects);
    private enum Tool { None, Terrain, Spawn, Checkpoint, Exit, BounceBall, Spring }
    private enum Palette { Terrain, Special, Sprites, Background }
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

    public EditorMode(Action<ModeId> switchMode, Action<string> showStatus, Action<LevelData, Func<bool>> startSaveValidation)
    {
        viewWidth = 320;
        viewHeight = 180;
        this.switchMode = switchMode;
        this.showStatus = showStatus;
        this.startSaveValidation = startSaveValidation;
        saveIcon = Globals.Content.Load<Texture2D>("graphics/ui/save");
        playIcon = Globals.Content.Load<Texture2D>("graphics/ui/play");
        homeIcon = Globals.Content.Load<Texture2D>("graphics/ui/home");
        arrowIcon = Globals.Content.Load<Texture2D>("graphics/ui/arrow-left");
        bars = new[]
        {
            Globals.Content.Load<Texture2D>("graphics/ui/tilebar"),
            Globals.Content.Load<Texture2D>("graphics/ui/specialbar"),
            Globals.Content.Load<Texture2D>("graphics/ui/spritebar"),
            Globals.Content.Load<Texture2D>("graphics/ui/backgroundbar")
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
        SpawnButton = new CollisionRect(16, 21, 27, 27);
        CheckpointButton = new CollisionRect(16, 21, 27, 27);
        ExitButton = new CollisionRect(48, 21, 27, 27);
        playerSprite = Globals.Content.Load<Texture2D>("graphics/player/idle");
        checkpointSprite = Globals.Content.Load<Texture2D>("graphics/special_objects/checkpoint");
        exitSprite = Globals.Content.Load<Texture2D>("graphics/special_objects/exit_flag");
        bounceBallSprite = Globals.Content.Load<Texture2D>("graphics/special_objects/bounceball");
        springSprite = Globals.Content.Load<Texture2D>("graphics/special_objects/spring");
        pixel = new Texture2D(Globals.graphics.GraphicsDevice, 1, 1);
        pixel.SetData(new[] { Color.White });
        var blank = new int[80, 400];
        for (int y = 0; y < 80; y++)
            for (int x = 0; x < 400; x++) blank[y, x] = -1;
        preview = new AutoTileMap(TerrainCatalog.Paths(), blank, new int[80, 400]);
        snapshot = Capture();
        camera = new Camera { Origin = Vector2.Zero };
        RefreshLayout();
        FitLevel();
    }

    public override void Enter()
    {
        previousMouse = Mouse.GetState();
        isPanning = false;
        lastPaintCell = null;
        wheelRemainder = 0;
        CancelDrag();
    }

    public override void Leave()
    {
        isPanning = false;
        CancelDrag();
    }

    public override void Update(GameTime gameTime)
    {
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
            && !PlayButton.Contains(pointer) && !HomeButton.Contains(pointer);

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
            if (pointer.X >= 64 && pointer.Y >= 0 && pointer.Y < 128)
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
                    draggingFromPalette = tool != Tool.Terrain;
                    if (IsSpecialTool()) showStatus($"{SelectedObjectType()}: {bounceDirection} | R: rotate");
                }
            }
            return;
        }
        if (Globals.Input.KeyJustDown(Keys.R)) RotateObject(position, overLevel);
        if (overLevel && Globals.Input.KeyDown(Keys.P))
        {
            lastPaintCell = null;
            if (clicked) TrySetSpawn(SpawnCellAt(position));
            return;
        }
        // Spawn, checkpoint and exit tools: press on the level, drag the ghost around, release to drop it.
        if (tool is Tool.Spawn or Tool.Checkpoint or Tool.Exit or Tool.BounceBall or Tool.Spring)
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
                if (tool != Tool.Spawn) cell.X += 4;
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
                    objects.Remove(target);
                    snapshot = Capture();
                }
                return;
            }
        }
        if (tool == Tool.Terrain && overLevel && scroll == 0
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
        else if (PlayButton.Contains(pointer)) switchMode(ModeId.Play);
        else if (HomeButton.Contains(pointer)) switchMode(ModeId.Home);
    }

    public override void Draw()
    {
        RefreshLayout();
        Globals.graphics.GraphicsDevice.Clear(new Color(0, 174, 220));
        Globals.spriteBatch.Begin(samplerState: SamplerState.PointClamp, transformMatrix: WorldTransform());
        preview.Draw();
        Globals.spriteBatch.End();

        // Screen-space lines stay one pixel wide regardless of editor zoom.
        Globals.spriteBatch.Begin(samplerState: SamplerState.PointClamp);
        DrawGrid();
        Globals.spriteBatch.End();
        Globals.spriteBatch.Begin(samplerState: SamplerState.PointClamp, transformMatrix: WorldTransform());

        foreach (var item in objects)
        {
            // The picked-up checkpoint, or the exit being moved, fades while its ghost is dragged.
            bool dragged = ghost.HasValue && (item == moving || (tool == Tool.Exit && item.Type == LevelObject.ExitFlag));
            DrawObject(item.Type, new Point(item.X, item.Y), ObjectFits(new Point(item.X, item.Y), item.Type, item, item.Direction),
                dragged ? 0.35f : 1f, item.Direction);
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
        selectedTerrain = 0;
        spawn = data.HasSpawn ? new Point(data.SpawnX, data.SpawnY) : null;
        objects.Clear();
        objects.AddRange(data.Objects);
        CancelDrag();
        savePath = path;
        snapshot = Capture();
        lastPaintCell = null;
        FitLevel();
    }

    public void SaveLevel()
    {
        var data = Capture();
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
                showStatus("Cannot save: an object is blocked or a spring has no supporting terrain.");
                return;
            }
        string path = savePath ?? Path.Combine(LevelData.SaveFolder(),
            "Level-" + DateTime.Now.ToString("yyyyMMdd-HHmmss-fff") + ".uptown");
        data.Name = Path.GetFileNameWithoutExtension(path);
        // Only this exact snapshot is saved after its fresh playtest reaches an exit.
        startSaveValidation(data, () => SaveClearedLevel(data, path));
    }

    private bool SaveClearedLevel(LevelData data, string path)
    {
        try
        {
            data.Save(path);
            savePath = path;
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
        ghost = null;
        moving = null;
        draggingFromPalette = false;
    }

    private void Drop(Point feet)
    {
        if (tool == Tool.Spawn) { TrySetSpawn(feet); return; }
        if (!ghostValid)
        {
            showStatus(tool == Tool.Spring
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
        else if (moving != null)
        {
            moving.X = feet.X;
            moving.Y = feet.Y;
            moving.Direction = ghostDirection;
        }
        else objects.Add(new LevelObject { Type = SelectedObjectType(), X = feet.X, Y = feet.Y, Direction = ghostDirection });
        snapshot = Capture();
    }

    private string SelectedObjectType() => tool switch
    {
        Tool.BounceBall => LevelObject.BounceBall,
        Tool.Spring => LevelObject.Spring,
        Tool.Exit => LevelObject.ExitFlag,
        _ => LevelObject.Checkpoint
    };

    private bool IsSpecialTool() => tool is Tool.Checkpoint or Tool.Exit or Tool.BounceBall or Tool.Spring;

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
                if (!ObjectRotation.Bounds(feet, item.Type, item.Direction).Contains(world)) continue;
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
        if (!IsSpecialTool()) return;
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
            if (item.Type == type && ObjectRotation.Bounds(new Point(item.X, item.Y), type, item.Direction).Contains(world)) return item;
        }
        return null;
    }

    // An object needs its rotated hitbox clear of terrain and inside the level, and can't sit on
    // another object's spot. The exit ignores the old exit, since dropping it moves that one.
    private bool ObjectFits(Point feet, string type, LevelObject ignore, BounceDirection direction = BounceDirection.Up)
    {
        Rectangle bounds = ObjectRotation.Bounds(feet, type, direction, true);
        if (bounds.Left < 0 || bounds.Right > preview.Width * 8 || bounds.Top < 0 || bounds.Bottom > preview.Height * 8) return false;
        for (int y = bounds.Top / 8; y <= (bounds.Bottom - 1) / 8; y++)
            for (int x = bounds.Left / 8; x <= (bounds.Right - 1) / 8; x++)
                if (preview.Occupied(x, y)) return false;
        if (type == LevelObject.Spring && !SpringSupported(feet, direction)) return false;
        foreach (var item in objects)
        {
            if (item == ignore || (type == LevelObject.ExitFlag && item.Type == LevelObject.ExitFlag)) continue;
            if (item.X == feet.X && item.Y == feet.Y) return false;
        }
        return true;
    }

    // First frame of the object's sprite at its feet; blocked spots are tinted red.
    private void DrawObject(string type, Point feet, bool valid, float alpha, BounceDirection direction = BounceDirection.Up)
    {
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

    private Tool PaletteToolAt(Point point)
    {
        if (TerrainAt(point) >= 0) return Tool.Terrain;
        if (palette == Palette.Sprites && SpawnButton.Contains(point)) return Tool.Spawn;
        if (palette == Palette.Special && CheckpointButton.Contains(point)) return Tool.Checkpoint;
        if (palette == Palette.Special && ExitButton.Contains(point)) return Tool.Exit;
        if (palette == Palette.Special && BounceBallButton.Contains(point)) return Tool.BounceBall;
        if (palette == Palette.Special && SpringButton.Contains(point)) return Tool.Spring;
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
        for (int row = 0; row < 3; row++)
            for (int col = 0; col < 2; col++)
            {
                var slot = new Rectangle(3 + col * 32, 8 + row * 31, 27, 27);
                Tool item = PaletteToolAt(slot.Center);
                bool selected = item != Tool.None && tool == item
                    && (item != Tool.Terrain || row * 2 + col == selectedTerrain);
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
        else if (palette == Palette.Sprites)
            DrawPaletteItem(playerSprite, new Rectangle(0, 0, 16, 16), SpawnButton.Rect);
        else if (palette == Palette.Special)
        {
            DrawPaletteItem(checkpointSprite, new Rectangle(0, 0, 16, 32), CheckpointButton.Rect);
            DrawPaletteItem(exitSprite, new Rectangle(0, 0, 16, 32), ExitButton.Rect);
            DrawPaletteItem(bounceBallSprite, new Rectangle(0, 0, 16, 16), BounceBallButton.Rect);
            DrawPaletteItem(springSprite, new Rectangle(0, 0, 16, 16), SpringButton.Rect);
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
        isPanning = false;
        lastPaintCell = null;
    }

    private Vector2 ViewCenter()
    {
        float left = (sidebarOpen ? SidebarWidth + 16 : 16) * uiScale;
        return new Vector2((left + windowWidth - 8 * uiScale) / 2,
            (36 * uiScale + windowHeight - 8 * uiScale) / 2);
    }

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
            preview.Paint(from.X, from.Y, solid, selectedTerrain);
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
