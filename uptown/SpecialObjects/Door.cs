using System;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using NodeTesting.models;

namespace uptown.SpecialObjects;

public sealed class Door : SpecialObject
{
    private readonly SpriteAnimation sprite;
    private bool opensLeft;
    public bool Opened { get; private set; }
    public CollisionRect TopCollider { get; }

    public Door(Vector2 feet) : base(feet, 8, 16)
    {
        sprite = new SpriteAnimation("graphics/special_objects/door", 4, 12);
        sprite.AddState("Idle", 0, 1, 12, false);
        sprite.AddState("Open", 0, 4, 12, false);
        sprite.Play("Idle");
        sprite.Origin = new Vector2(4, 16);
        Rectangle top = PlatformBounds(feet.ToPoint(), false);
        TopCollider = new CollisionRect(top.Center.X, top.Center.Y, top.Width, top.Height);
    }

    public static Rectangle Bounds(Point feet) => new(feet.X - 4, feet.Y - 16, 8, 16);

    // The final frame's top rail occupies source pixels 2 through 13.
    // Its landing surface extends toward the side the door opens into.
    public static Rectangle PlatformBounds(Point feet, bool opensLeft) =>
        new(feet.X + (opensLeft ? -10 : -2), feet.Y - 16, 12, 2);

    private void UpdateTopCollider()
    {
        Rectangle top = PlatformBounds(Position.ToPoint(), opensLeft);
        TopCollider.UpdateRect(top.Center.X, top.Center.Y);
    }

    public static bool Supported(Point feet, Func<int, int, bool> terrain)
    {
        int x = (int)Math.Floor((feet.X - 4) / 8f);
        int y = (int)Math.Floor((feet.Y - 16) / 8f);
        return terrain(x, y - 1) && terrain(x, y + 2);
    }

    public override void OnPlayerEnter(Player player)
    {
        if (Opened) return;
        Opened = true;
        bool fromRight = player.Position.X > Position.X;
        opensLeft = fromRight;
        UpdateTopCollider();
        sprite.SpriteEffect = fromRight ? SpriteEffects.FlipHorizontally : SpriteEffects.None;
        // Mirror around the doorway rather than shifting the 8px footprint.
        sprite.Origin = new Vector2(fromRight ? 12 : 4, 16);
        sprite.Play("Open");
    }

    public override void Update(GameTime gameTime)
    {
        if (Opened && !sprite.IsFinished) sprite.UpdateOnce(gameTime);
    }

    public override void Draw()
    {
        sprite.Position = Position;
        sprite.Draw();
    }

    public override void Reset()
    {
        base.Reset();
        Opened = false;
        opensLeft = false;
        UpdateTopCollider();
        sprite.SpriteEffect = SpriteEffects.None;
        sprite.Origin = new Vector2(4, 16);
        sprite.Play("Idle");
    }
}
