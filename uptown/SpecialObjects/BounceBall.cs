using Microsoft.Xna.Framework;
using NodeTesting.models;

namespace uptown.SpecialObjects;

public sealed class BounceBall : BounceObject
{
    private readonly CollisionCircle circle;

    public BounceBall(Vector2 feet, BounceDirection direction = BounceDirection.Up)
        : base(feet, direction, "graphics/special_objects/bounceball", 7, 24)
    {
        circle = new CollisionCircle((int)feet.X, (int)feet.Y - 8, 7);
    }

    public override bool Touches(Player player) => circle.Intersects(player.Collider);
}
