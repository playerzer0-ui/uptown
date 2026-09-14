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
    private readonly TileMap preview;
    private readonly int viewWidth;
    private readonly int viewHeight;
    private readonly Canvas canvas;
    private readonly Action<ModeId> switchMode;
    private readonly Texture2D icons;
    private readonly Texture2D tileset;
    private readonly Texture2D pixel;
    private bool sidebarOpen = true;
    private MouseState previousMouse;
    private Point pointer;
    private const int SidebarWidth = 80;
    private const int ButtonSize = 24;
    private Rectangle SaveButton => new(viewWidth - 88, 4, ButtonSize, ButtonSize);
    private Rectangle PlayButton => new(viewWidth - 60, 4, ButtonSize, ButtonSize);
    private Rectangle HomeButton => new(viewWidth - 32, 4, ButtonSize, ButtonSize);
    private Rectangle ToggleButton => new(sidebarOpen ? SidebarWidth : 0, viewHeight / 2 - 12, 12, 24);

    public EditorMode(int viewWidth, int viewHeight, Canvas canvas, Action<ModeId> switchMode)
    {
        this.viewWidth = viewWidth;
        this.viewHeight = viewHeight;
        this.canvas = canvas;
        this.switchMode = switchMode;
        icons = Globals.Content.Load<Texture2D>("graphics/ui/UI_buttons");
        tileset = Globals.Content.Load<Texture2D>("graphics/tileset/basic");
        pixel = new Texture2D(Globals.graphics.GraphicsDevice, 1, 1);
        pixel.SetData(new[] { Color.White });
        preview = new TileMap("graphics/tileset/basic", 8, 8,
            Path.Combine(AppContext.BaseDirectory, "Content", "maps", "test-map2_platforms.csv"));
    }

    public override void Enter() => previousMouse = Mouse.GetState();

    public override void Update(GameTime gameTime)
    {
        preview.Update(gameTime);
        MouseState mouse = Mouse.GetState();
        Vector2 position = canvas.ScreenToCanvas(new Vector2(mouse.X, mouse.Y));
        pointer = new Point((int)MathF.Floor(position.X), (int)MathF.Floor(position.Y));
        bool clicked = mouse.LeftButton == ButtonState.Pressed && previousMouse.LeftButton == ButtonState.Released;
        previousMouse = mouse;
        if (!clicked) return;
        if (ToggleButton.Contains(pointer)) sidebarOpen = !sidebarOpen;
        else if (PlayButton.Contains(pointer)) switchMode(ModeId.Play);
        else if (HomeButton.Contains(pointer)) switchMode(ModeId.Home);
    }

    public override void Draw()
    {
        Globals.graphics.GraphicsDevice.Clear(new Color(0, 174, 220));
        int left = sidebarOpen ? SidebarWidth + 16 : 16;
        var area = new Rectangle(left, 36, viewWidth - left - 8, viewHeight - 44);
        float scale = Math.Min((float)area.Width / (preview.Width * preview.TileSizeX),
            (float)area.Height / (preview.Height * preview.TileSizeY));
        var transform = Matrix.CreateScale(scale) * Matrix.CreateTranslation(area.X, area.Y, 0);
        Globals.spriteBatch.Begin(samplerState: SamplerState.PointClamp, transformMatrix: transform);
        preview.Draw();
        Globals.spriteBatch.End();

        // UI is drawn in canvas space, independently of the level view.
        Globals.spriteBatch.Begin(samplerState: SamplerState.PointClamp);
        if (sidebarOpen)
        {
            Fill(new Rectangle(0, 0, SidebarWidth, viewHeight), new Color(123, 211, 235));
            Fill(new Rectangle(SidebarWidth - 1, 0, 1, viewHeight), new Color(24, 82, 104));
            // Preserve the atlas arrangement for the upcoming tile-selection tool.
            Globals.spriteBatch.Draw(tileset, new Rectangle(0, 0, tileset.Width * 2, tileset.Height * 2), Color.White);
        }
        DrawButton(SaveButton, 1, new Color(125, 151, 161), false);
        DrawButton(PlayButton, 0, new Color(225, 69, 59));
        DrawButton(HomeButton, 2, new Color(149, 213, 112));
        DrawButton(ToggleButton, sidebarOpen ? 3 : 4, new Color(24, 100, 127));
        Globals.spriteBatch.End();
    }

    private void Fill(Rectangle rectangle, Color color) => Globals.spriteBatch.Draw(pixel, rectangle, color);

    private void DrawButton(Rectangle bounds, int iconIndex, Color background, bool enabled = true)
    {
        bool hovered = enabled && bounds.Contains(pointer);
        Fill(bounds, hovered ? Color.White : new Color(24, 48, 63));
        var inset = new Rectangle(bounds.X + 1, bounds.Y + 1, bounds.Width - 2, bounds.Height - 2);
        Fill(inset, hovered ? Color.Lerp(background, Color.White, 0.2f) : background);
        int size = Math.Min(16, bounds.Width - 2);
        var destination = new Rectangle(bounds.Center.X - size / 2, bounds.Center.Y - size / 2, size, size);
        Globals.spriteBatch.Draw(icons, destination, new Rectangle(iconIndex * 16, 0, 16, 16),
            enabled ? Color.White : new Color(150, 150, 150));
    }
}
