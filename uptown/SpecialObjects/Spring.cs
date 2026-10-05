using Microsoft.Xna.Framework;
using NodeTesting.models;

namespace uptown.SpecialObjects;

public sealed class Spring : BounceObject
{
    public Spring(Vector2 feet, BounceDirection direction = BounceDirection.Up)
        : base(feet, direction, "graphics/special_objects/spring", 10, 24)
    {
    }

    // Frame 0 is compressed against its mount: contact follows that idle pad.
    public override bool Touches(Player player)
    {
        var bounds = Collider.Rect;
        Rectangle pad = Direction switch
        {
            BounceDirection.Right => new Rectangle(bounds.Left, bounds.Top, 4, bounds.Height),
            BounceDirection.Left => new Rectangle(bounds.Right - 4, bounds.Top, 4, bounds.Height),
            BounceDirection.Down => new Rectangle(bounds.Left, bounds.Top, bounds.Width, 4),
            _ => new Rectangle(bounds.Left, bounds.Bottom - 4, bounds.Width, 4)
        };
        return pad.Intersects(player.Collider.Rect);
    }

}
