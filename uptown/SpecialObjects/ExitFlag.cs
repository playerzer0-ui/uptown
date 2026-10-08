using Microsoft.Xna.Framework;
using NodeTesting.models;

namespace uptown.SpecialObjects;

// 16x32 waving flag, 5 frames. Touching it completes the level.
public sealed class ExitFlag : SpecialObject
{
    private readonly SpriteAnimation sprite = new("graphics/special_objects/exit_flag", 5, 8);
    private readonly BounceDirection direction;

    /// <summary>Set the first time the player touches the flag; PlayMode reads it to end the level.</summary>
    public bool Reached { get; private set; }

    public ExitFlag(Vector2 feet, BounceDirection direction = BounceDirection.Up) : base(feet, 8, 16)
    {
        this.direction = direction;
        sprite.Origin = new Vector2(8, 16);
        sprite.Rotation = ObjectRotation.Angle(direction);
        var bounds = ObjectRotation.Bounds(feet.ToPoint(), LevelObject.ExitFlag, direction, true);
        Collider = new CollisionRect(bounds.Center.X, bounds.Center.Y, bounds.Width, bounds.Height);
    }

    public override void Update(GameTime gameTime) => sprite.Update(gameTime);

    public override void Reset()
    {
        base.Reset();
        Reached = false;
        sprite.Reset();
    }

    public override void Draw()
    {
        var bounds = ObjectRotation.Bounds(Position.ToPoint(), LevelObject.ExitFlag, direction);
        sprite.Position = bounds.Center.ToVector2();
        sprite.Draw();
    }

    public override void OnPlayerEnter(Player player) => Reached = true;
}
