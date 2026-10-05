using Microsoft.Xna.Framework;
using NodeTesting.models;

namespace uptown.SpecialObjects;

// Both launchers share the same impulse and direction convention.
public abstract class BounceObject : SpecialObject
{
    public const float BounceSpeed = 240f;
    protected readonly SpriteAnimation Sprite;
    private bool animating;
    public BounceDirection Direction { get; }

    protected BounceObject(Vector2 feet, BounceDirection direction, string texture, int frames, int fps)
        : base(feet, 16, 16)
    {
        Direction = direction;
        Sprite = new SpriteAnimation(texture, frames, fps);
        Sprite.Origin = new Vector2(8, 8);
        Sprite.Rotation = ObjectRotation.Angle(direction);
        Sprite.AddState("Idle", 0, 1, fps, false);
        Sprite.AddState("Bounce", 0, frames, fps, false);
        Sprite.Play("Idle");
    }

    public override void Draw()
    {
        Sprite.Position = Position - new Vector2(0, 8);
        Sprite.Draw();
    }

    public override void OnPlayerEnter(Player player)
    {
        player.Bounce(Direction, BounceSpeed);
        Sprite.Play("Bounce");
        animating = true;
    }

    public override void Update(GameTime gameTime)
    {
        if (!animating) return;
        Sprite.Update(gameTime);
        if (!Sprite.IsFinished) return;
        animating = false;
        Sprite.Play("Idle");
    }
}
