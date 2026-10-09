using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using NodeTesting.models;

namespace uptown.SpecialObjects;

public sealed class Elevator : SpecialObject
{
    public const int Width = 16, Height = 24, Frames = 5;
    public const float TravelTime = 0.3f;
    private readonly Texture2D openSheet;
    private readonly Texture2D occupiedSheet;
    private float openness;
    public string PairId { get; }
    public bool IsDestination { get; }
    public bool IsOpen => openness >= Frames - 1;
    public bool Occupied { get; private set; }
    public bool Reversing { get; private set; }
    public float OccupiedTime { get; private set; }

    public Elevator(LevelObject data) : base(new Vector2(data.X, data.Y), Width, Height)
    {
        PairId = data.PairId;
        IsDestination = data.IsDestination;
        openSheet = Globals.Content.Load<Texture2D>("graphics/special_objects/elevatoropen");
        occupiedSheet = Globals.Content.Load<Texture2D>("graphics/special_objects/elevatorclosewithplayer");
    }

    public static Rectangle Bounds(Point feet) => new(feet.X - Width / 2, feet.Y - Height, Width, Height);
    // Center of the lavender 2x2 indicator at source pixels (7,1) through (8,2).
    public static Vector2 Connector(Point feet) => new(feet.X, feet.Y - Height + 2);
    public override bool Touches(Player player) => false;
    public bool Near(Player player) => !player.IsDead && !player.IsInElevator && Math.Abs(player.Position.X - Position.X) <= 24
        && Math.Abs(player.Position.Y - Position.Y) <= 12;
    public bool CanEnter(Player player) => IsOpen && !player.IsInElevator && Near(player)
        && Math.Abs(player.Position.X - Position.X) <= 8 && Math.Abs(player.Position.Y - Position.Y) <= 4;

    public void UpdateNearby(Player player, float dt)
    {
        if (Occupied) return;
        openness = Math.Clamp(openness + (Near(player) ? 1 : -1) * 12f * dt, 0, Frames - 1);
    }

    public void SetOccupied(float time, bool reverse)
    {
        Occupied = true;
        Reversing = reverse;
        OccupiedTime = time;
    }

    public void ClearOccupied() { Occupied = false; openness = Reversing ? Frames - 1 : 0; }

    public static int OccupiedFrame(float time, bool reverse)
    {
        int frame = Math.Clamp((int)(time / TravelTime * Frames), 0, Frames - 1);
        return reverse ? Frames - 1 - frame : frame;
    }

    public override void Draw()
    {
        int frame = Occupied ? OccupiedFrame(OccupiedTime, Reversing) : (int)openness;
        Globals.spriteBatch.Draw(Occupied ? occupiedSheet : openSheet, Bounds(Position.ToPoint()),
            new Rectangle(frame * Width, 0, Width, Height), Color.White);
    }

    public override void Reset() { base.Reset(); openness = 0; Occupied = false; OccupiedTime = 0; Reversing = false; }

    public static bool ValidPairs(IEnumerable<LevelObject> objects) =>
        objects.Where(item => item.Type == LevelObject.Elevator).GroupBy(item => item.PairId)
            .All(pair => !string.IsNullOrWhiteSpace(pair.Key) && pair.Count() == 2
                && pair.Count(item => item.IsDestination) == 1);
}
