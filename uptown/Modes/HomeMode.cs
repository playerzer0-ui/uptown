using System;
using System.IO;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using NodeTesting.models;

namespace uptown.Modes;

// Title screen and level picker use the same direct window rendering as the other modes.
public sealed class HomeMode : GameMode
{
    private const int ButtonWidth = 96;
    private const int ButtonHeight = 22;
    private const int RowHeight = 18;
    private static readonly Color Ink = new(24, 48, 63);
    private static readonly Rectangle ListBox = new(56, 40, 208, 128);
    private static readonly RasterizerState Clip = new() { ScissorTestEnable = true };
    private int uiScale = 1;
    private Vector2 uiOffset;
    private int windowWidth;
    private int windowHeight;
    private readonly Func<string[]> listLevels;
    private readonly Action<string> load;
    private readonly Texture2D backIcon;
    private readonly SpriteFont font;
    private readonly MenuButton[] buttons;
    private readonly CollisionRect backButton = new(24, 20, 24, 24);
    private MouseState previousMouse;
    private Point pointer;
    private bool choosingLevel;
    private string[] levels = Array.Empty<string>();
    private float scroll;

    private sealed record MenuButton(string Label, Texture2D Icon, Color Background, CollisionRect Bounds, Action Click);

    public HomeMode(Action play, Action create,
        Func<string[]> listLevels, Action<string> load, Action exit)
    {
        this.listLevels = listLevels;
        this.load = load;
        backIcon = Globals.Content.Load<Texture2D>("graphics/ui/arrow-left");
        font = Globals.Content.Load<SpriteFont>("File");
        // CollisionRect constructors take center coordinates, not top-left.
        MenuButton Button(string label, string icon, Color background, int y, Action click) =>
            new(label, Globals.Content.Load<Texture2D>($"graphics/ui/{icon}"), background,
                new CollisionRect(160, y, ButtonWidth, ButtonHeight), click);
        buttons = new[]
        {
            Button("Play", "play", new Color(225, 69, 59), 70, play),
            Button("Create", "create", new Color(24, 100, 127), 96, create),
            Button("Load", "save", new Color(125, 151, 161), 122, OpenLevelList),
            Button("Exit", "stop", new Color(149, 213, 112), 148, exit),
        };
    }

    public override void Enter()
    {
        previousMouse = Mouse.GetState();
        choosingLevel = false;
    }

    public override void Update(GameTime gameTime)
    {
        RefreshLayout();
        MouseState mouse = Mouse.GetState();
        Vector2 menu = ScreenToMenu(new Vector2(mouse.X, mouse.Y));
        pointer = new Point((int)MathF.Floor(menu.X), (int)MathF.Floor(menu.Y));
        bool clicked = mouse.LeftButton == ButtonState.Pressed && previousMouse.LeftButton == ButtonState.Released;
        int wheel = mouse.ScrollWheelValue - previousMouse.ScrollWheelValue;
        previousMouse = mouse;

        if (choosingLevel)
        {
            // One wheel notch scrolls one row.
            scroll = Math.Clamp(scroll - wheel / 120f * RowHeight, 0, MaxScroll());
            if (!clicked) return;
            if (backButton.Contains(pointer)) choosingLevel = false;
            else if (HoveredRow() is int row) load(levels[row]);
            return;
        }

        if (!clicked) return;
        foreach (var button in buttons)
            if (button.Bounds.Contains(pointer)) { button.Click(); return; }
    }

    public override void Draw()
    {
        RefreshLayout();
        Globals.graphics.GraphicsDevice.Clear(new Color(0, 174, 220));
        Globals.spriteBatch.Begin(samplerState: SamplerState.PointClamp, transformMatrix: UiTransform());
        if (choosingLevel) DrawLevelList();
        else DrawMenu();
        Globals.spriteBatch.End();
    }

    private void OpenLevelList()
    {
        try { levels = listLevels(); }
        catch (Exception error) when (error is IOException || error is UnauthorizedAccessException)
        {
            levels = Array.Empty<string>();
        }
        scroll = 0;
        choosingLevel = true;
    }

    private float MaxScroll() => Math.Max(0, levels.Length * RowHeight - ListBox.Height);

    private int? HoveredRow()
    {
        if (!ListBox.Contains(pointer)) return null;
        int row = (int)((pointer.Y - ListBox.Y + scroll) / RowHeight);
        return row >= 0 && row < levels.Length ? row : null;
    }

    private void DrawMenu()
    {
        DrawText("UPTOWN", new Vector2(160, 28), 2f, Color.White);
        foreach (var button in buttons)
        {
            Rectangle bounds = button.Bounds.Rect;
            DrawPanel(button.Bounds, button.Background);
            DrawIcon(button.Icon, new Vector2(bounds.X + 13, bounds.Center.Y));
            DrawText(button.Label, new Vector2(bounds.Center.X + 8, bounds.Center.Y), 1f, Color.White);
        }
    }

    private void DrawLevelList()
    {
        DrawText("Load Level", new Vector2(160, 20), 1f, Color.White);
        DrawPanel(backButton, new Color(24, 100, 127));
        DrawIcon(backIcon, backButton.Center);

        Fill(new Rectangle(ListBox.X - 1, ListBox.Y - 1, ListBox.Width + 2, ListBox.Height + 2), Ink);
        Fill(ListBox, new Color(123, 211, 235));
        if (levels.Length == 0)
        {
            DrawText("No saved levels yet", ListBox.Center.ToVector2(), 1f, Color.White);
            return;
        }

        // Rows are clipped to the list box while they scroll.
        Globals.spriteBatch.End();
        var device = Globals.graphics.GraphicsDevice;
        Rectangle previousScissor = device.ScissorRectangle;
        device.ScissorRectangle = ListScissor();
        Globals.spriteBatch.Begin(samplerState: SamplerState.PointClamp, rasterizerState: Clip,
            transformMatrix: UiTransform());
        int? hovered = HoveredRow();
        int first = (int)(scroll / RowHeight);
        for (int i = first; i < levels.Length && i * RowHeight - scroll < ListBox.Height; i++)
        {
            var row = new Rectangle(ListBox.X, ListBox.Y + (int)MathF.Round(i * RowHeight - scroll), ListBox.Width, RowHeight);
            if (i == hovered) Fill(row, Color.White * 0.5f);
            else if (i % 2 == 1) Fill(row, new Color(24, 100, 127) * 0.15f);
            string name = Path.GetFileNameWithoutExtension(levels[i]);
            Vector2 size = font.MeasureString(name);
            Globals.spriteBatch.DrawString(font, name,
                new Vector2(row.X + 6, row.Y + MathF.Round((RowHeight - size.Y) / 2)), Ink);
        }
        Globals.spriteBatch.End();
        device.ScissorRectangle = previousScissor;
        Globals.spriteBatch.Begin(samplerState: SamplerState.PointClamp, transformMatrix: UiTransform());

        // Scrollbar thumb, only when the list overflows.
        float max = MaxScroll();
        if (max > 0)
        {
            int thumb = Math.Max(12, ListBox.Height * ListBox.Height / (levels.Length * RowHeight));
            int y = ListBox.Y + (int)MathF.Round(scroll / max * (ListBox.Height - thumb));
            Fill(new Rectangle(ListBox.Right - 4, y, 3, thumb), Ink);
        }
    }

    private void RefreshLayout()
    {
        var bounds = Globals.graphics.GraphicsDevice.PresentationParameters.Bounds;
        SetViewport(bounds.Width, bounds.Height);
    }

    private void SetViewport(int width, int height)
    {
        if (width <= 0 || height <= 0 || (width == windowWidth && height == windowHeight)) return;
        windowWidth = width;
        windowHeight = height;
        uiScale = WindowRendering.ScaleFor(width, height);
        // Center the menu's layout without rendering it to an intermediate canvas.
        uiOffset = new Vector2(MathF.Round((width - 320 * uiScale) / 2f),
            MathF.Round((height - 180 * uiScale) / 2f));
    }

    private Matrix UiTransform() =>
        Matrix.CreateScale(uiScale) * Matrix.CreateTranslation(uiOffset.X, uiOffset.Y, 0);

    private Vector2 ScreenToMenu(Vector2 screen) => (screen - uiOffset) / uiScale;

    private Rectangle ListScissor() => Rectangle.Intersect(
        new Rectangle((int)uiOffset.X + ListBox.X * uiScale, (int)uiOffset.Y + ListBox.Y * uiScale,
            ListBox.Width * uiScale, ListBox.Height * uiScale),
        new Rectangle(0, 0, windowWidth, windowHeight));

    private static void DrawIcon(Texture2D icon, Vector2 center) =>
        Globals.spriteBatch.Draw(icon, center, null, Color.White, 0,
            new Vector2(icon.Width / 2f, icon.Height / 2f), 1f, SpriteEffects.None, 0);

    private void DrawPanel(CollisionRect button, Color background)
    {
        Rectangle bounds = button.Rect;
        bool hovered = button.Contains(pointer);
        button.Draw(hovered ? Color.White : Ink);
        Fill(new Rectangle(bounds.X + 1, bounds.Y + 1, bounds.Width - 2, bounds.Height - 2),
            hovered ? Color.Lerp(background, Color.White, 0.2f) : background);
    }

    private static void Fill(Rectangle rectangle, Color color) => Globals.spriteBatch.Draw(Globals.Pixel, rectangle, color);

    // Centered text with a 1px drop shadow.
    private void DrawText(string text, Vector2 center, float scale, Color color)
    {
        Vector2 origin = font.MeasureString(text) / 2;
        origin = new Vector2(MathF.Round(origin.X), MathF.Round(origin.Y));
        Globals.spriteBatch.DrawString(font, text, center + new Vector2(1, 1) * scale, Ink, 0f, origin, scale, SpriteEffects.None, 0f);
        Globals.spriteBatch.DrawString(font, text, center, color, 0f, origin, scale, SpriteEffects.None, 0f);
    }
}
