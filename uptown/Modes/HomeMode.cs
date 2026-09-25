using System;
using System.IO;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using NodeTesting.models;

namespace uptown.Modes;

// Title screen plus a level picker. Draws on the 320x180 canvas, so the mouse is mapped into canvas space.
public sealed class HomeMode : GameMode
{
    private const int ButtonWidth = 96;
    private const int ButtonHeight = 22;
    private const int RowHeight = 18;
    private static readonly Color Ink = new(24, 48, 63);
    private static readonly Rectangle ListBox = new(56, 40, 208, 128);
    private static readonly RasterizerState Clip = new() { ScissorTestEnable = true };
    private readonly Func<Vector2, Vector2> screenToCanvas;
    private readonly Func<string[]> listLevels;
    private readonly Action<string> load;
    private readonly SpriteSheet icons;
    private readonly SpriteFont font;
    private readonly MenuButton[] buttons;
    private readonly CollisionRect backButton = new(24, 20, 24, 24);
    private MouseState previousMouse;
    private Point pointer;
    private bool choosingLevel;
    private string[] levels = Array.Empty<string>();
    private float scroll;

    private sealed record MenuButton(string Label, int Icon, Color Background, CollisionRect Bounds, Action Click);

    public HomeMode(Func<Vector2, Vector2> screenToCanvas, Action play, Action create,
        Func<string[]> listLevels, Action<string> load, Action exit)
    {
        this.screenToCanvas = screenToCanvas;
        this.listLevels = listLevels;
        this.load = load;
        icons = new SpriteSheet("graphics/ui/UI_buttons", 5);
        font = Globals.Content.Load<SpriteFont>("File");
        // CollisionRect constructors take center coordinates, not top-left.
        MenuButton Button(string label, int icon, Color background, int y, Action click) =>
            new(label, icon, background, new CollisionRect(160, y, ButtonWidth, ButtonHeight), click);
        buttons = new[]
        {
            Button("Play", 0, new Color(225, 69, 59), 70, play),
            Button("Create", 3, new Color(24, 100, 127), 96, create),
            Button("Load", 1, new Color(125, 151, 161), 122, OpenLevelList),
            Button("Exit", 2, new Color(149, 213, 112), 148, exit),
        };
    }

    public override void Enter()
    {
        previousMouse = Mouse.GetState();
        choosingLevel = false;
    }

    public override void Update(GameTime gameTime)
    {
        MouseState mouse = Mouse.GetState();
        Vector2 canvas = screenToCanvas(new Vector2(mouse.X, mouse.Y));
        pointer = new Point((int)MathF.Floor(canvas.X), (int)MathF.Floor(canvas.Y));
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
        Globals.graphics.GraphicsDevice.Clear(new Color(0, 174, 220));
        Globals.spriteBatch.Begin(samplerState: SamplerState.PointClamp);
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
            icons.DrawFrame(button.Icon, new Vector2(bounds.X + 13, bounds.Center.Y), 1f, Color.White);
            DrawText(button.Label, new Vector2(bounds.Center.X + 8, bounds.Center.Y), 1f, Color.White);
        }
    }

    private void DrawLevelList()
    {
        DrawText("Load Level", new Vector2(160, 20), 1f, Color.White);
        DrawPanel(backButton, new Color(24, 100, 127));
        icons.DrawFrame(3, backButton.Center, 1f, Color.White);

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
        device.ScissorRectangle = ListBox;
        Globals.spriteBatch.Begin(samplerState: SamplerState.PointClamp, rasterizerState: Clip);
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
        Globals.spriteBatch.Begin(samplerState: SamplerState.PointClamp);

        // Scrollbar thumb, only when the list overflows.
        float max = MaxScroll();
        if (max > 0)
        {
            int thumb = Math.Max(12, ListBox.Height * ListBox.Height / (levels.Length * RowHeight));
            int y = ListBox.Y + (int)MathF.Round(scroll / max * (ListBox.Height - thumb));
            Fill(new Rectangle(ListBox.Right - 4, y, 3, thumb), Ink);
        }
    }

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
