using System;
using System.IO;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using NodeTesting.models;

namespace uptown.Modes;

// Layout and navigation first; painting and saving will follow.
public sealed class EditorMode : GameMode
{
    private readonly AutoTileMap preview;
    private readonly CollisionRect TerrainButton;
    private bool terrainSelected;
    private Point? lastPaintCell;
    private bool lastErase;
    private readonly int viewWidth;
    private readonly int viewHeight;
    private readonly Canvas canvas;
    private readonly Action<ModeId> switchMode;
    private readonly SpriteSheet icons;
    private readonly Texture2D tileset;
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

    public EditorMode(int viewWidth, int viewHeight, Canvas canvas, Action<ModeId> switchMode)
    {
        this.viewWidth = viewWidth;
        this.viewHeight = viewHeight;
        this.canvas = canvas;
        this.switchMode = switchMode;
        icons = new SpriteSheet("graphics/ui/UI_buttons", 5);
        // CollisionRect constructors take center coordinates, not top-left.
        SaveButton = new CollisionRect(viewWidth - 76, 16, ButtonSize, ButtonSize);
        PlayButton = new CollisionRect(viewWidth - 48, 16, ButtonSize, ButtonSize);
        HomeButton = new CollisionRect(viewWidth - 20, 16, ButtonSize, ButtonSize);
        ToggleButton = new CollisionRect(SidebarWidth + 6, viewHeight / 2, 12, 24);
        TerrainButton = new CollisionRect(24, 24, 32, 32);
        tileset = Globals.Content.Load<Texture2D>("graphics/tileset/basic");
        pixel = new Texture2D(Globals.graphics.GraphicsDevice, 1, 1);
        pixel.SetData(new[] { Color.White });
        preview = new AutoTileMap("graphics/tileset/basic",
            Path.Combine(AppContext.BaseDirectory, "Content", "maps", "test-map2_platforms.csv"));
        var area = new Rectangle(SidebarWidth + 16, 36, viewWidth - SidebarWidth - 24, viewHeight - 44);
        float scale = Math.Min((float)area.Width / (preview.Width * preview.TileSizeX),
            (float)area.Height / (preview.Height * preview.TileSizeY));
        camera = new Camera
        {
            Origin = Vector2.Zero,
            Zoom = scale,
            Position = -new Vector2(area.X, area.Y) / scale
        };
    }

    public override void Enter()
    {
        previousMouse = Mouse.GetState();
        isPanning = false;
        lastPaintCell = null;
    }

    public override void Leave() => isPanning = false;

    public override void Update(GameTime gameTime)
    {
        preview.Update(gameTime);
        MouseState mouse = Mouse.GetState();
        Vector2 position = canvas.ScreenToCanvas(new Vector2(mouse.X, mouse.Y));
        pointer = new Point((int)MathF.Floor(position.X), (int)MathF.Floor(position.Y));
        bool clicked = mouse.LeftButton == ButtonState.Pressed && previousMouse.LeftButton == ButtonState.Released;
        bool overLevel = new Rectangle(sidebarOpen ? SidebarWidth : 0, 0,
            viewWidth - (sidebarOpen ? SidebarWidth : 0), viewHeight).Contains(pointer)
            && !ToggleButton.Contains(pointer) && !SaveButton.Contains(pointer)
            && !PlayButton.Contains(pointer) && !HomeButton.Contains(pointer);

        if (mouse.MiddleButton == ButtonState.Released) isPanning = false;
        else if (previousMouse.MiddleButton == ButtonState.Released && overLevel) isPanning = true;

        if (isPanning)
        {
            // Convert both positions with the current canvas scale so resizing
            // and letterboxing do not change the drag speed.
            Vector2 previous = canvas.ScreenToCanvas(new Vector2(previousMouse.X, previousMouse.Y));
            camera.Position -= (position - previous) / camera.Zoom;
        }
        int scroll = mouse.ScrollWheelValue - previousMouse.ScrollWheelValue;
        if (scroll != 0 && overLevel)
        {
            Vector2 worldUnderCursor = Vector2.Transform(position, Matrix.Invert(camera.Transform()));
            camera.Zoom = MathHelper.Clamp(camera.Zoom * MathF.Pow(1.2f, scroll / 120f), 0.125f, 8f);
            camera.Position = worldUnderCursor - position / camera.Zoom;
        }
        previousMouse = mouse;
        if (isPanning) { lastPaintCell = null; return; }
        if (terrainSelected && overLevel && scroll == 0
            && (mouse.LeftButton == ButtonState.Pressed || mouse.RightButton == ButtonState.Pressed))
        {
            Vector2 world = Vector2.Transform(position, Matrix.Invert(camera.Transform()));
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
        if (sidebarOpen && TerrainButton.Contains(pointer)) terrainSelected = true;
        else if (ToggleButton.Contains(pointer))
        {
            sidebarOpen = !sidebarOpen;
            ToggleButton.UpdateRect((sidebarOpen ? SidebarWidth : 0) + 6, viewHeight / 2);
        }
        else if (PlayButton.Contains(pointer)) switchMode(ModeId.Play);
        else if (HomeButton.Contains(pointer)) switchMode(ModeId.Home);
    }

    public override void Draw()
    {
        Globals.graphics.GraphicsDevice.Clear(new Color(0, 174, 220));
        Globals.spriteBatch.Begin(samplerState: SamplerState.PointClamp, transformMatrix: camera.Transform());
        preview.Draw();
        Globals.spriteBatch.End();

        // UI is drawn in canvas space, independently of the level view.
        Globals.spriteBatch.Begin(samplerState: SamplerState.PointClamp);
        if (sidebarOpen)
        {
            Fill(new Rectangle(0, 0, SidebarWidth, viewHeight), new Color(123, 211, 235));
            Fill(new Rectangle(SidebarWidth - 1, 0, 1, viewHeight), new Color(24, 82, 104));
            TerrainButton.Draw(terrainSelected ? Color.Yellow : TerrainButton.Contains(pointer) ? Color.White : new Color(24, 82, 104));
            Fill(new Rectangle(9, 9, 30, 30), new Color(24, 100, 127));
            Globals.spriteBatch.Draw(tileset, new Rectangle(12, 12, 24, 24), new Rectangle(8, 0, 8, 8), Color.White);
        }
        DrawButton(SaveButton, 1, new Color(125, 151, 161), false);
        DrawButton(PlayButton, 0, new Color(225, 69, 59));
        DrawButton(HomeButton, 2, new Color(149, 213, 112));
        DrawButton(ToggleButton, sidebarOpen ? 3 : 4, new Color(24, 100, 127));
        Globals.spriteBatch.End();
    }

    private void Fill(Rectangle rectangle, Color color) => Globals.spriteBatch.Draw(pixel, rectangle, color);

    private void PaintStroke(Point from, Point to, bool solid)
    {
        // Fill gaps when the pointer crosses multiple cells between updates.
        int dx = Math.Abs(to.X - from.X), dy = -Math.Abs(to.Y - from.Y);
        int sx = from.X < to.X ? 1 : -1, sy = from.Y < to.Y ? 1 : -1;
        int error = dx + dy;
        while (true)
        {
            preview.Paint(from.X, from.Y, solid);
            if (from == to) break;
            int twice = 2 * error;
            if (twice >= dy) { error += dy; from.X += sx; }
            if (twice <= dx) { error += dx; from.Y += sy; }
        }
    }

    private void DrawButton(CollisionRect button, int iconIndex, Color background, bool enabled = true)
    {
        Rectangle bounds = button.Rect;
        bool hovered = enabled && button.Contains(pointer);
        button.Draw(hovered ? Color.White : new Color(24, 48, 63));
        var inset = new Rectangle(bounds.X + 1, bounds.Y + 1, bounds.Width - 2, bounds.Height - 2);
        Fill(inset, hovered ? Color.Lerp(background, Color.White, 0.2f) : background);
        float scale = Math.Min(1f, (float)(bounds.Width - 2) / icons.FrameWidth);
        icons.DrawFrame(iconIndex, button.Center, scale,
            enabled ? Color.White : new Color(150, 150, 150));
    }
}
