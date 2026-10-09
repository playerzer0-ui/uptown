using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using NodeTesting.models;
using System;
using System.Collections.Generic;

namespace uptown.SpecialObjects;

public sealed class Spike : SpecialObject
{
    private readonly Texture2D texture;
    private readonly BounceDirection direction;
    private readonly MovingPlatform mount;
    private readonly Vector2 mountOffset;

    public Spike(Vector2 feet, BounceDirection direction = BounceDirection.Up, MovingPlatform mount = null) : base(feet, 8, 8)
    {
        this.direction = direction;
        texture = Globals.Content.Load<Texture2D>("graphics/special_objects/spike");
        this.mount = mount;
        if (mount != null)
        {
            mountOffset = feet - mount.Position;
            mount.AttachSpike(this);
        }
    }

    public override void OnPlayerEnter(Player player) => player.Die();

    public static bool Supported(Point feet, BounceDirection direction, Func<int, int, bool> terrain,
        IEnumerable<LevelObject> objects = null)
    {
        int x = (int)Math.Floor((feet.X - 4) / 8f);
        int y = (int)Math.Floor((feet.Y - 8) / 8f);
        bool terrainSupport = direction switch
        {
            BounceDirection.Up => terrain(x, y + 1),
            BounceDirection.Right => terrain(x - 1, y),
            BounceDirection.Down => terrain(x, y - 1),
            BounceDirection.Left => terrain(x + 1, y),
            _ => false
        };
        return terrainSupport || SupportingObject(feet, direction, objects) != null;
    }

    public static bool SupportedOn(Point feet, BounceDirection direction, Rectangle support, bool oneWay)
    {
        Rectangle spike = ObjectRotation.Bounds(feet, LevelObject.Spike, direction);
        if (oneWay && direction != BounceDirection.Up) return false;
        return direction switch
        {
            BounceDirection.Up => spike.Bottom == support.Top && spike.Left >= support.Left && spike.Right <= support.Right,
            BounceDirection.Down => spike.Top == support.Bottom && spike.Left >= support.Left && spike.Right <= support.Right,
            BounceDirection.Right => spike.Left == support.Right && spike.Top >= support.Top && spike.Bottom <= support.Bottom,
            BounceDirection.Left => spike.Right == support.Left && spike.Top >= support.Top && spike.Bottom <= support.Bottom,
            _ => false
        };
    }

    public static LevelObject SupportingObject(Point feet, BounceDirection direction, IEnumerable<LevelObject> objects)
    {
        if (objects == null) return null;
        foreach (var item in objects)
        {
            bool oneWay = item.Type == LevelObject.Platform;
            if (!oneWay && item.Type != LevelObject.MovingPlatform) continue;
            Rectangle bounds = oneWay ? Platform.Bounds(new Point(item.X, item.Y), item.WidthTiles)
                : MovingPlatform.Bounds(new Point(item.X, item.Y), item.WidthTiles);
            if (SupportedOn(feet, direction, bounds, oneWay)) return item;
        }
        return null;
    }

    public void FollowPlatform()
    {
        if (mount == null) return;
        Vector2 target = mount.Position + mountOffset;
        Vector2 delta = target - Position;
        Collider.Translate((int)delta.X, (int)delta.Y);
        Position = target;
    }

    public override void Draw() => Globals.spriteBatch.Draw(texture, Collider.Rect.Center.ToVector2(),
        null, Color.White, ObjectRotation.Angle(direction), new Vector2(4, 4), 1f, SpriteEffects.None, 0);
}
