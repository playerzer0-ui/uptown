using System;
using System.Collections.Generic;
using System.IO;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using NodeTesting.models;

namespace uptown.Modes;

// Layout and navigation first; painting and saving will follow.
public sealed class EditorMode : GameMode
{
    private AutoTileMap preview;
    private readonly Action<string> showStatus;
    private Point? spawn;
    private string savePath;
    private LevelData snapshot;
    public LevelData Capture() => LevelData.Capture(preview, spawn, objects);
    private enum Tool { None, Terrain, Spawn, Checkpoint, Exit }
    private readonly CollisionRect TerrainButton;
    private readonly CollisionRect SpawnButton;
    private readonly CollisionRect CheckpointButton;
    private readonly CollisionRect ExitButton;
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
    private readonly SpriteSheet icons;
    private readonly Texture2D tileset;
    private readonly Texture2D playerSprite;
    private readonly Texture2D checkpointSprite;
    private readonly Texture2D exitSprite;
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

    public EditorMode(Action<ModeId> switchMode, Action<string> showStatus)
    {
        viewWidth = 320;
        viewHeight = 180;
        this.switchMode = switchMode;
        this.showStatus = showStatus;
        icons = new SpriteSheet("graphics/ui/UI_buttons", 5);
        // CollisionRect constructors take center coordinates, not top-left.
        SaveButton = new CollisionRect(viewWidth - 76, 16, ButtonSize, ButtonSize);
        PlayButton = new CollisionRect(viewWidth - 48, 16, ButtonSize, ButtonSize);
        HomeButton = new CollisionRect(viewWidth - 20, 16, ButtonSize, ButtonSize);
        ToggleButton = new CollisionRect(SidebarWidth + 6, viewHeight / 2, 12, 24);
        TerrainButton = new CollisionRect(24, 24, 32, 32);
        SpawnButton = new CollisionRect(56, 24, 32, 32);
        CheckpointButton = new CollisionRect(24, 60, 32, 32);
        ExitButton = new CollisionRect(56, 60, 32, 32);
        tileset = Globals.Content.Load<Texture2D>("graphics/tileset/basic");
        playerSprite = Globals.Content.Load<Texture2D>("graphics/player/idle");
        checkpointSprite = Globals.Content.Load<Texture2D>("graphics/special_objects/checkpoint");
        exitSprite = Globals.Content.Load<Texture2D>("graphics/special_objects/exit_flag");
        pixel = new Texture2D(Globals.graphics.GraphicsDevice, 1, 1);
        pixel.SetData(new[] { Color.White });
        var blank = new int[80, 400];
        for (int y = 0; y < 80; y++)
            for (int x = 0; x < 400; x++) blank[y, x] = -1;
        preview = new AutoTileMap("graphics/tileset/basic", blank);
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
        if (control && Globals.Input.KeyJustDown(Keys.S)) SaveLevel();
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
        if (overLevel && Globals.Input.KeyDown(Keys.P))
        {
            lastPaintCell = null;
            if (clicked) TrySetSpawn(SpawnCellAt(position));
            return;
        }
        // Spawn, checkpoint and exit tools: press on the level, drag the ghost around, release to drop it.
        if (tool is Tool.Spawn or Tool.Checkpoint or Tool.Exit)
        {
            bool held = mouse.LeftButton == ButtonState.Pressed;
            if (held && (ghost.HasValue || (clicked && overLevel)))
            {
                // Pressing on an existing checkpoint picks it up instead of making a new one.
                if (!ghost.HasValue && tool == Tool.Checkpoint)
                    moving = ObjectUnder(position, LevelObject.Checkpoint);
                var cell = SpawnCellAt(position);
                if (cell != ghost)
                {
                    ghost = cell;
                    ghostValid = tool == Tool.Spawn
                        ? LevelData.Capture(preview, cell).ValidSpawn()
                        : ObjectFits(cell, tool == Tool.Checkpoint ? LevelObject.Checkpoint : LevelObject.ExitFlag, moving);
                }
                return;
            }
            if (ghost.HasValue)
            {
                // Dropping outside the level (e.g. on the sidebar) cancels the move.
                if (overLevel) Drop(ghost.Value);
                CancelDrag();
                return;
            }
            // Right-click deletes the checkpoint or exit flag under the cursor.
            if (rightClicked && overLevel && tool != Tool.Spawn)
            {
                var target = ObjectUnder(position, tool == Tool.Checkpoint ? LevelObject.Checkpoint : LevelObject.ExitFlag);
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
        if (sidebarOpen && TerrainButton.Contains(pointer)) tool = Tool.Terrain;
        else if (sidebarOpen && SpawnButton.Contains(pointer)) tool = Tool.Spawn;
        else if (sidebarOpen && CheckpointButton.Contains(pointer)) tool = Tool.Checkpoint;
        else if (sidebarOpen && ExitButton.Contains(pointer)) tool = Tool.Exit;
        else if (ToggleButton.Contains(pointer))
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

        foreach (var item in objects)
        {
            // The picked-up checkpoint, or the exit being moved, fades while its ghost is dragged.
            bool dragged = ghost.HasValue && (item == moving || (tool == Tool.Exit && item.Type == LevelObject.ExitFlag));
            DrawObject(item.Type, new Point(item.X, item.Y), ObjectFits(new Point(item.X, item.Y), item.Type, item),
                dragged ? 0.35f : 1f);
        }
        if (snapshot.HasSpawn)
            DrawPlayer(new Point(snapshot.SpawnX, snapshot.SpawnY), snapshot.ValidSpawn(),
                ghost.HasValue && tool == Tool.Spawn ? 0.35f : 1f);
        if (ghost.HasValue && tool == Tool.Spawn)
            DrawPlayer(ghost.Value, ghostValid, 0.75f);
        else if (ghost.HasValue)
            DrawObject(tool == Tool.Checkpoint ? LevelObject.Checkpoint : LevelObject.ExitFlag, ghost.Value, ghostValid, 0.75f);
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
            Fill(new Rectangle(0, 0, SidebarWidth, viewHeight), new Color(123, 211, 235));
            Fill(new Rectangle(SidebarWidth - 1, 0, 1, viewHeight), new Color(24, 82, 104));
            DrawToolButton(TerrainButton, Tool.Terrain);
            Globals.spriteBatch.Draw(tileset, new Rectangle(12, 12, 24, 24), new Rectangle(8, 0, 8, 8), Color.White);
            DrawToolButton(SpawnButton, Tool.Spawn);
            Globals.spriteBatch.Draw(playerSprite, new Rectangle(44, 12, 24, 24), new Rectangle(0, 0, 16, 16), Color.White);
            DrawToolButton(CheckpointButton, Tool.Checkpoint);
            Globals.spriteBatch.Draw(checkpointSprite, new Rectangle(18, 48, 12, 24), new Rectangle(0, 0, 16, 32), Color.White);
            DrawToolButton(ExitButton, Tool.Exit);
            Globals.spriteBatch.Draw(exitSprite, new Rectangle(50, 48, 12, 24), new Rectangle(0, 0, 16, 32), Color.White);
        }
        DrawButton(SaveButton, 1, new Color(125, 151, 161));
        DrawButton(PlayButton, 0, new Color(225, 69, 59));
        DrawButton(HomeButton, 2, new Color(149, 213, 112));
        DrawButton(ToggleButton, 4, new Color(24, 100, 127),
            effects: sidebarOpen ? SpriteEffects.None : SpriteEffects.FlipHorizontally);
        Globals.spriteBatch.End();
    }

    // Replaces the canvas with a saved level; later saves overwrite that file.
    public void LoadLevel(string path)
    {
        var data = LevelData.Load(path);
        preview = new AutoTileMap("graphics/tileset/basic", data.CreateGrid(false));
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
        try
        {
            if (savePath == null)
                savePath = Path.Combine(LevelData.SaveFolder(), "Level-" + DateTime.Now.ToString("yyyyMMdd-HHmmss-fff") + ".uptown");
            var data = Capture();
            data.Name = Path.GetFileNameWithoutExtension(savePath);
            data.Save(savePath);
            showStatus("Saved: " + savePath);
        }
        catch (Exception error) when (error is IOException || error is UnauthorizedAccessException)
        {
            showStatus("Save failed: " + error.Message);
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
    }

    private void Drop(Point feet)
    {
        if (tool == Tool.Spawn) { TrySetSpawn(feet); return; }
        if (!ghostValid)
        {
            showStatus("Objects need 8x16 pixels of clear space inside the level, and can't share a spot.");
            return;
        }
        if (tool == Tool.Exit)
        {
            // Only one exit: placing it again moves the existing one.
            objects.RemoveAll(item => item.Type == LevelObject.ExitFlag);
            objects.Add(new LevelObject { Type = LevelObject.ExitFlag, X = feet.X, Y = feet.Y });
        }
        else if (moving != null)
        {
            moving.X = feet.X;
            moving.Y = feet.Y;
        }
        else objects.Add(new LevelObject { Type = LevelObject.Checkpoint, X = feet.X, Y = feet.Y });
        snapshot = Capture();
    }

    // Topmost object of the given type whose 16x32 sprite is under the screen position.
    private LevelObject ObjectUnder(Vector2 screen, string type)
    {
        var world = Vector2.Transform(screen, Matrix.Invert(WorldTransform()));
        for (int i = objects.Count - 1; i >= 0; i--)
        {
            var item = objects[i];
            if (item.Type == type && new Rectangle(item.X - 8, item.Y - 32, 16, 32).Contains(world)) return item;
        }
        return null;
    }

    // An object needs its 8x16 hitbox clear of terrain and inside the level, and can't sit on
    // another object's spot. The exit ignores the old exit, since dropping it moves that one.
    private bool ObjectFits(Point feet, string type, LevelObject ignore)
    {
        if (feet.X - 4 < 0 || feet.X + 4 > preview.Width * 8 || feet.Y - 16 < 0 || feet.Y > preview.Height * 8) return false;
        for (int y = (feet.Y - 16) / 8; y <= (feet.Y - 1) / 8; y++)
            for (int x = (feet.X - 4) / 8; x <= (feet.X + 3) / 8; x++)
                if (preview.Occupied(x, y)) return false;
        foreach (var item in objects)
        {
            if (item == ignore || (type == LevelObject.ExitFlag && item.Type == LevelObject.ExitFlag)) continue;
            if (item.X == feet.X && item.Y == feet.Y) return false;
        }
        return true;
    }

    // First frame of the object's sprite at its feet; blocked spots are tinted red.
    private void DrawObject(string type, Point feet, bool valid, float alpha)
    {
        if (!valid) Fill(new Rectangle(feet.X - 4, feet.Y - 16, 8, 16), Color.Red * 0.5f * alpha);
        Texture2D texture = type == LevelObject.ExitFlag ? exitSprite : checkpointSprite;
        Globals.spriteBatch.Draw(texture, new Vector2(feet.X - 8, feet.Y - 32), new Rectangle(0, 0, 16, 32),
            (valid ? Color.White : new Color(255, 120, 120)) * alpha);
    }

    // Idle frame drawn at the feet position; invalid spots are tinted red.
    private void DrawPlayer(Point feet, bool valid, float alpha)
    {
        if (!valid) Fill(new Rectangle(feet.X - 4, feet.Y - 12, 8, 12), Color.Red * 0.5f * alpha);
        Globals.spriteBatch.Draw(playerSprite, new Vector2(feet.X - 8, feet.Y - 16), new Rectangle(0, 0, 16, 16),
            (valid ? Color.White : new Color(255, 120, 120)) * alpha);
    }

    private void DrawToolButton(CollisionRect button, Tool buttonTool)
    {
        button.Draw(tool == buttonTool ? Color.Yellow : button.Contains(pointer) ? Color.White : new Color(24, 82, 104));
        Rectangle bounds = button.Rect;
        Fill(new Rectangle(bounds.X + 1, bounds.Y + 1, bounds.Width - 2, bounds.Height - 2), new Color(24, 100, 127));
    }

    private void Fill(Rectangle rectangle, Color color) => Globals.spriteBatch.Draw(pixel, rectangle, color);

    private Matrix WorldTransform()
    {
        Matrix transform = camera.Transform();
        transform.M41 = MathF.Round(transform.M41);
        transform.M42 = MathF.Round(transform.M42);
        return transform;
    }

    private void RefreshLayout()
    {
        var bounds = Globals.graphics.GraphicsDevice.PresentationParameters.Bounds;
        if (bounds.Width <= 0 || bounds.Height <= 0 || (bounds.Width == windowWidth && bounds.Height == windowHeight)) return;
        Vector2 center = camera.Position + ViewCenter() / camera.Zoom;
        bool hadLayout = windowWidth > 0;
        windowWidth = bounds.Width;
        windowHeight = bounds.Height;
        uiScale = Math.Max(1, Math.Min(windowWidth / 320, windowHeight / 180));
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
            preview.Paint(from.X, from.Y, solid);
            if (from == to) { snapshot = Capture(); break; }
            int twice = 2 * error;
            if (twice >= dy) { error += dy; from.X += sx; }
            if (twice <= dx) { error += dx; from.Y += sy; }
        }
    }

    private void DrawButton(CollisionRect button, int iconIndex, Color background, bool enabled = true,
        SpriteEffects effects = SpriteEffects.None)
    {
        Rectangle bounds = button.Rect;
        bool hovered = enabled && button.Contains(pointer);
        button.Draw(hovered ? Color.White : new Color(24, 48, 63));
        var inset = new Rectangle(bounds.X + 1, bounds.Y + 1, bounds.Width - 2, bounds.Height - 2);
        Fill(inset, hovered ? Color.Lerp(background, Color.White, 0.2f) : background);
        float scale = Math.Min(1f, (float)(bounds.Width - 2) / icons.FrameWidth);
        icons.DrawFrame(iconIndex, button.Center, scale,
            enabled ? Color.White : new Color(150, 150, 150), effects);
    }
}
