using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using NodeTesting.models;

namespace uptown.SpecialObjects;

public sealed class MovingPlatform : SpecialObject
{
    public const int Width = 24;
    public const int Height = 8;
    private readonly Texture2D texture;
    private readonly Path2D path;
    private readonly List<Vector2> route;
    private readonly List<Spike> mountedSpikes = new();
    public void AttachSpike(Spike spike) => mountedSpikes.Add(spike);

    public MovingPlatform(LevelObject data) : base(new Vector2(data.X, data.Y), Math.Max(3, data.WidthTiles) * 8, Height)
    {
        texture = Globals.Content.Load<Texture2D>("graphics/special_objects/moving_platform");
        route = Route(data);
        path = new Path2D(route, data.MoveSpeed) { IsPingPong = true };
    }

    public static Rectangle Bounds(Point feet, int widthTiles = 3) =>
        new(feet.X - Math.Max(3, widthTiles) * 4, feet.Y - Height, Math.Max(3, widthTiles) * 8, Height);
    public override bool Touches(Player player) => false;

    public override void Reset()
    {
        base.Reset();
        path.IsReversed = false;
        path.IsActive = true;
        path.Reset();
    }

    public static List<Vector2> Route(LevelObject data)
    {
        var points = new List<Vector2> { new(data.X, data.Y) };
        if (data.Waypoints != null)
            foreach (var p in data.Waypoints)
                if (p != null && points[^1] != new Vector2(p.X, p.Y)) points.Add(new Vector2(p.X, p.Y));
        return points;
    }

    public static Point SnapWaypoint(Point from, Point target)
    {
        int dx = target.X - from.X, dy = target.Y - from.Y;
        int ax = Math.Abs(dx), ay = Math.Abs(dy);
        if (ax > ay * 2) return new Point(target.X, from.Y);
        if (ay > ax * 2) return new Point(from.X, target.Y);
        int length = Math.Max(ax, ay);
        return new Point(from.X + Math.Sign(dx) * length, from.Y + Math.Sign(dy) * length);
    }

    public void Advance(GameTime gameTime, Player player, CollisionMap terrain)
    {
        // Keep all intermediate waypoint turns even when a frame covers a corner.
        double remaining = Math.Min(gameTime.ElapsedGameTime.TotalSeconds, 0.25);
        while (remaining > 0)
        {
            double dt = Math.Min(remaining, 1.0 / Math.Max(1, path.Speed));
            path.Update(new GameTime(gameTime.TotalGameTime, TimeSpan.FromSeconds(dt)));
            Point target = new((int)MathF.Round(path.Position.X), (int)MathF.Round(path.Position.Y));
            int dx = target.X - (int)Position.X, dy = target.Y - (int)Position.Y;
            while (dx != 0 || dy != 0)
            {
                int sx = Math.Sign(dx), sy = Math.Sign(dy);
                if (sx != 0 && !Step(sx, 0, player, terrain)) return;
                if (sy != 0 && !Step(0, sy, player, terrain)) return;
                dx -= sx;
                dy -= sy;
            }
            remaining -= dt;
        }
    }

    private bool Step(int dx, int dy, Player player, CollisionMap terrain)
    {
        int respawns = player.RespawnCount;
        Rectangle before = Collider.Rect;
        Rectangle next = before;
        next.Offset(dx, dy);
        if (terrain.CheckCollision(next)) { path.IsActive = false; return false; }
        bool rider = player.Rides(before) || player.Grabs(before);
        Position += new Vector2(dx, dy);
        Collider.Translate(dx, dy);
        if (rider) player.Transport(this, dx, dy);
        else if (next.Intersects(player.Collider.Rect))
        {
            Rectangle actor = player.Collider.Rect;
            int pushX = dx > 0 ? next.Right - actor.Left : dx < 0 ? next.Left - actor.Right : 0;
            int pushY = dy > 0 ? next.Bottom - actor.Top : dy < 0 ? next.Top - actor.Bottom : 0;
            player.Transport(this, pushX, pushY);
        }
        foreach (var spike in mountedSpikes)
        {
            spike.FollowPlatform();
            if (spike.Touches(player)) { spike.OnPlayerEnter(player); break; }
        }
        return player.RespawnCount == respawns;
    }

    public override void Draw()
    {
        DrawRoute(route, Color.White);
        DrawPieces(texture, Collider.Rect, Color.White);
    }

    public static int PieceIndex(int index, int count) => index == 0 ? 0 : index == count - 1 ? 2 : 1;

    public static void DrawPieces(Texture2D texture, Rectangle bounds, Color tint)
    {
        int count = bounds.Width / 8;
        for (int i = 0; i < count; i++)
            Globals.spriteBatch.Draw(texture, new Vector2(bounds.Left + i * 8, bounds.Top),
                new Rectangle(PieceIndex(i, count) * 8, 0, 8, Height), tint);
    }

    public static void DrawRoute(IReadOnlyList<Vector2> points, Color color)
    {
        for (int i = 1; i < points.Count; i++)
        {
            Vector2 start = points[i - 1] - new Vector2(0, Height / 2);
            Vector2 delta = points[i] - points[i - 1];
            float length = delta.Length();
            if (length == 0) continue;
            Vector2 direction = delta / length;
            for (float distance = 0; distance < length; distance += 8)
                Globals.spriteBatch.Draw(Globals.Pixel, start + direction * distance, null, color,
                    MathF.Atan2(delta.Y, delta.X), Vector2.Zero,
                    new Vector2(Math.Min(4, length - distance), 1), SpriteEffects.None, 0);
        }
        foreach (var point in points)
            Globals.spriteBatch.Draw(Globals.Pixel, new Rectangle((int)point.X - 1, (int)point.Y - Height / 2 - 1, 3, 3), color);
    }
}
