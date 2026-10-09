using Microsoft.Xna.Framework;

namespace uptown.SpecialObjects;

public static class ObjectRotation
{
    private static readonly BounceDirection[] Eight =
    {
        BounceDirection.Up, BounceDirection.UpRight, BounceDirection.Right, BounceDirection.DownRight,
        BounceDirection.Down, BounceDirection.DownLeft, BounceDirection.Left, BounceDirection.UpLeft
    };

    public static int Step(BounceDirection direction) => direction switch
    {
        BounceDirection.UpRight => 1, BounceDirection.Right => 2, BounceDirection.DownRight => 3,
        BounceDirection.Down => 4, BounceDirection.DownLeft => 5, BounceDirection.Left => 6,
        BounceDirection.UpLeft => 7, _ => 0
    };

    public static float Angle(BounceDirection direction) => Step(direction) * MathHelper.PiOver4;

    public static Vector2 Vector(BounceDirection direction)
    {
        const float diagonal = 0.70710678f;
        return direction switch
        {
            BounceDirection.UpRight => new Vector2(diagonal, -diagonal),
            BounceDirection.DownRight => new Vector2(diagonal, diagonal),
            BounceDirection.DownLeft => new Vector2(-diagonal, diagonal),
            BounceDirection.UpLeft => new Vector2(-diagonal, -diagonal),
            BounceDirection.Right => Vector2.UnitX, BounceDirection.Down => Vector2.UnitY,
            BounceDirection.Left => -Vector2.UnitX, _ => -Vector2.UnitY
        };
    }

    public static BounceDirection Next(string type, BounceDirection direction) =>
        Eight[(Step(direction) + (type == LevelObject.BounceBall ? 1 : 2)) % 8];

    public static Rectangle Bounds(Point feet, string type, BounceDirection direction, bool collision = false)
    {
        if (type == LevelObject.Spike) return new Rectangle(feet.X - 4, feet.Y - 8, 8, 8);
        bool launcher = type == LevelObject.BounceBall || type == LevelObject.Spring;
        int width = launcher ? 16 : collision ? 8 : 16;
        int height = launcher ? 16 : collision ? 16 : 32;
        if (!launcher && direction is BounceDirection.Left or BounceDirection.Right)
            (width, height) = (height, width);
        // The ball is circular, so its collision footprint stays unchanged at diagonal angles.
        return new Rectangle(feet.X - width / 2, feet.Y - height, width, height);
    }
}
