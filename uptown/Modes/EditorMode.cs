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
    private int viewWidth;
    private int viewHeight;
    private int windowWidth;
    private int windowHeight;
    private float uiScale = 1;
    private static readonly float[] ZoomLevels = { 0.25f, 0.5f, 1f, 2f, 4f, 8f };
    private int zoomIndex;
    private int wheelRemainder;
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

    public EditorMode(Action<ModeId> switchMode)
    {
        viewWidth = 320;
        viewHeight = 180;
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
        camera = new Camera { Origin = Vector2.Zero };
        RefreshLayout();
        float fit = Math.Min((windowWidth - (SidebarWidth + 24) * uiScale) / (preview.Width * 8),
            (windowHeight - 44 * uiScale) / (preview.Height * 8));
        while (zoomIndex < ZoomLevels.Length - 1 && ZoomLevels[zoomIndex + 1] <= fit) zoomIndex++;
        camera.Zoom = ZoomLevels[zoomIndex];
        camera.Position = -new Vector2((SidebarWidth + 16) * uiScale, 36 * uiScale) / camera.Zoom;
    }

    public override void Enter()
    {
        previousMouse = Mouse.GetState();
        isPanning = false;
        lastPaintCell = null;
        wheelRemainder = 0;
    }

    public override void Leave() => isPanning = false;

    public override void Update(GameTime gameTime)
    {
        preview.Update(gameTime);
        RefreshLayout();
        MouseState mouse = Mouse.GetState();
        Vector2 position = new Vector2(mouse.X, mouse.Y);
        pointer = new Point((int)MathF.Floor(position.X / uiScale), (int)MathF.Floor(position.Y / uiScale));
        bool clicked = mouse.LeftButton == ButtonState.Pressed && previousMouse.LeftButton == ButtonState.Released;
        bool overLevel = new Rectangle(sidebarOpen ? SidebarWidth : 0, 0,
            viewWidth - (sidebarOpen ? SidebarWidth : 0), viewHeight).Contains(pointer)
            && !ToggleButton.Contains(pointer) && !SaveButton.Contains(pointer)
            && !PlayButton.Contains(pointer) && !HomeButton.Contains(pointer);

        if (mouse.MiddleButton == ButtonState.Released) isPanning = false;
        else if (previousMouse.MiddleButton == ButtonState.Released && overLevel) isPanning = true;

        if (isPanning)
        {
            Vector2 previous = new Vector2(previousMouse.X, previousMouse.Y);
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
                zoomIndex = nextZoom;
                camera.Zoom = ZoomLevels[zoomIndex];
                camera.Position = worldUnderCursor - position / camera.Zoom;
            }
        }
        previousMouse = mouse;
        if (isPanning) { lastPaintCell = null; return; }
        if (terrainSelected && overLevel && scroll == 0
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
        RefreshLayout();
        Globals.graphics.GraphicsDevice.Clear(new Color(0, 174, 220));
        Globals.spriteBatch.Begin(samplerState: SamplerState.PointClamp, transformMatrix: WorldTransform());
        preview.Draw();
        Globals.spriteBatch.End();

        // UI uses its own integer scale; terrain renders directly to the window.
        Globals.spriteBatch.Begin(samplerState: SamplerState.PointClamp, transformMatrix: Matrix.CreateScale(uiScale));
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
        windowWidth = bounds.Width;
        windowHeight = bounds.Height;
        uiScale = Math.Max(1, Math.Min(windowWidth / 320, windowHeight / 180));
        viewWidth = (int)MathF.Ceiling(windowWidth / uiScale);
        viewHeight = (int)MathF.Ceiling(windowHeight / uiScale);
        SaveButton.UpdateRect(viewWidth - 76, 16);
        PlayButton.UpdateRect(viewWidth - 48, 16);
        HomeButton.UpdateRect(viewWidth - 20, 16);
        ToggleButton.UpdateRect((sidebarOpen ? SidebarWidth : 0) + 6, viewHeight / 2);
        isPanning = false;
        lastPaintCell = null;
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
