using Microsoft.Xna.Framework;
using NodeTesting.models;

namespace uptown.SpecialObjects;

// 16x32 animated sprite, 5 frames. Touching it makes it the player's respawn point.
// The sprite is white so it can be tinted: the current checkpoint turns green.
public sealed class Checkpoint : SpecialObject
{
    private readonly SpriteAnimation sprite = new("graphics/special_objects/checkpoint", 5, 8);
    private readonly BounceDirection direction;

    /// <summary>True for the checkpoint the player will respawn at. Only one is current at a time.</summary>
    public bool IsCurrent { get; private set; }

    public Checkpoint(Vector2 feet, BounceDirection direction = BounceDirection.Up) : base(feet, 8, 16)
    {
        this.direction = direction;
        sprite.Origin = new Vector2(8, 16);
        sprite.Rotation = ObjectRotation.Angle(direction);
        var bounds = ObjectRotation.Bounds(feet.ToPoint(), LevelObject.Checkpoint, direction, true);
        Collider = new CollisionRect(bounds.Center.X, bounds.Center.Y, bounds.Width, bounds.Height);
    }

    public override void Update(GameTime gameTime) => sprite.Update(gameTime);

    public override void Reset()
    {
        base.Reset();
        sprite.Reset();
        // Keep the active checkpoint and player spawn across deaths.
    }

    public override void Draw()
    {
        var bounds = ObjectRotation.Bounds(Position.ToPoint(), LevelObject.Checkpoint, direction);
        sprite.Position = bounds.Center.ToVector2();
        sprite.Color = IsCurrent ? PicoPallete.green : Color.White;
        sprite.Draw();
    }

    public override void OnPlayerEnter(Player player)
    {
        if (IsCurrent) return;
        foreach (var other in Scene.OfType<Checkpoint>()) other.IsCurrent = false;
        IsCurrent = true;
        player.Spawn = Position;
    }
}
