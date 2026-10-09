using System;
using System.IO;
using System.Linq;
using System.Text.Json;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using NodeTesting.models;
using uptown.SpecialObjects;

namespace uptown.Modes;

// The lobby's unpaired elevator launches saved levels rather than teleporting.
public sealed class LobbyLevelPicker
{
    public Elevator Elevator { get; }
    public bool Active => choosing || selected != null;
    private readonly Action<LevelData> play;
    private readonly SpriteFont font;
    private string[] levels = Array.Empty<string>();
    private LevelData selected;
    private bool choosing;
    private float elapsed;
    private int firstRow;
    private MouseState previousMouse;
    private string message = "";
    private const int RowHeight = 20;

    public LobbyLevelPicker(string csvPath, int width, int height, Action<LevelData> play, SpriteFont font)
    {
        this.play = play;
        this.font = font;
        var feet = FindFeet(csvPath, width, height);
        Elevator = new Elevator(new LevelObject { Type = LevelObject.Elevator, X = feet.X, Y = feet.Y });
    }

    public static Point FindFeet(string path, int width, int height)
    {
        var rows = File.ReadAllLines(path).Select(line => line.Split(',').Select(int.Parse).ToArray()).ToArray();
        if (rows.Length != height || rows.Any(row => row.Length != width))
            throw new InvalidDataException("Lobby special map dimensions must match terrain.");
        // Choose the leftmost complete 2x3 marker, independently of row order.
        for (int x = 0; x < width - 1; x++)
            for (int y = 0; y < height - 2; y++)
                if (Enumerable.Range(0, 3).All(dy => rows[y + dy][x] == 3 && rows[y + dy][x + 1] == 3))
                    return new Point(x * 8 + 8, (y + 3) * 8);
        throw new InvalidDataException("Lobby special map needs a 2x3 elevator marker (ID 3).");
    }

    private static Rectangle Panel(int width, int height, int scale) =>
        new(width - 144 * scale, 32 * scale, 140 * scale, height - 36 * scale);
    private static int Capacity(Rectangle panel, int scale) => Math.Max(1, (panel.Height / scale - 56) / RowHeight);

    public bool Update(GameTime time, Player player, int width, int height, int scale)
        => Update(time, player, width, height, scale, Mouse.GetState(), Globals.Input.KeyJustDown(Keys.K));

    internal bool Update(GameTime time, Player player, int width, int height, int scale, MouseState mouse, bool interactPressed)
    {
        bool clicked = mouse.LeftButton == ButtonState.Pressed && previousMouse.LeftButton == ButtonState.Released;
        int wheel = mouse.ScrollWheelValue - previousMouse.ScrollWheelValue;
        previousMouse = mouse;
        if (selected != null)
        {
            elapsed += Math.Max(0, (float)time.ElapsedGameTime.TotalSeconds);
            Elevator.SetOccupied(elapsed, false);
            if (elapsed >= Elevator.TravelTime)
            {
                var level = selected;
                selected = null;
                Elevator.ClearOccupied();
                player.EndElevatorTravel();
                play(level);
            }
            return true;
        }
        Elevator.UpdateNearby(player, (float)time.ElapsedGameTime.TotalSeconds);
        if (!choosing)
        {
            if (!interactPressed || !Elevator.CanEnter(player)) return false;
            try { levels = LevelData.ListSaves(); message = levels.Length == 0 ? "No saved levels yet" : ""; }
            catch (Exception error) when (error is IOException || error is UnauthorizedAccessException)
            { levels = Array.Empty<string>(); message = "Cannot list levels"; }
            firstRow = 0;
            choosing = true;
            return true;
        }
        var panel = Panel(width, height, scale);
        int capacity = Capacity(panel, scale);
        firstRow = Math.Clamp(firstRow, 0, Math.Max(0, levels.Length - capacity));
        if (panel.Contains(mouse.Position))
            firstRow = Math.Clamp(firstRow - wheel / 120, 0, Math.Max(0, levels.Length - capacity));
        if (interactPressed || (clicked && !panel.Contains(mouse.Position)))
        { choosing = false; return true; }
        if (!clicked) return true;
        int localY = (mouse.Y - panel.Y) / scale;
        if (localY < 24) { choosing = false; return true; }
        int row = (localY - 32) / RowHeight;
        if (localY < 32 || row >= capacity || row + firstRow >= levels.Length) return true;
        try
        {
            var level = LevelData.Load(levels[row + firstRow]);
            if (!level.ValidSpawn()) throw new InvalidDataException("Level spawn is blocked.");
            selected = level;
            elapsed = 0;
            choosing = false;
            player.BeginElevatorTravel(Elevator.Position);
            Elevator.SetOccupied(0, false);
        }
        catch (Exception error) when (error is IOException || error is UnauthorizedAccessException || error is JsonException)
        { message = "Cannot load this level"; }
        return true;
    }

    public void Cancel(Player player)
    {
        choosing = false;
        selected = null;
        Elevator.ClearOccupied();
        if (player.IsInElevator) player.EndElevatorTravel();
    }

    public void Draw(int width, int height, int scale)
    {
        if (!choosing) return;
        var panel = Panel(width, height, scale);
        var batch = Globals.spriteBatch;
        batch.Draw(Globals.Pixel, panel, new Color(24, 48, 63));
        void Text(string text, int x, int y, Color color) => batch.DrawString(font, text,
            new Vector2(panel.X + x * scale, panel.Y + y * scale), color, 0, Vector2.Zero, scale, SpriteEffects.None, 0);
        Text("LEVELS     X", 6, 6, Color.White);
        int capacity = Capacity(panel, scale);
        for (int row = 0; row < capacity && firstRow + row < levels.Length; row++)
        {
            int index = firstRow + row;
            var bounds = new Rectangle(panel.X + 4 * scale, panel.Y + (32 + row * RowHeight) * scale,
                panel.Width - 8 * scale, RowHeight * scale);
            batch.Draw(Globals.Pixel, bounds, bounds.Contains(previousMouse.Position) ? new Color(24, 100, 127) : new Color(40, 65, 80));
            string name = Path.GetFileNameWithoutExtension(levels[index]);
            while (name.Length > 0 && font.MeasureString(name).X > 124) name = name[..^1];
            Text(name, 8, 34 + row * RowHeight, Color.White);
        }
        string footer = message.Length > 0 ? message : levels.Length > capacity ? "Wheel to scroll" : "K / outside: close";
        Text(footer, 6, panel.Height / scale - 16, Color.White);
    }
}
