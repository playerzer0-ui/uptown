using Microsoft.Xna.Framework;
using NodeTesting.models;

namespace uptown.SpecialObjects;

// 16x32 waving flag, 5 frames. Touching it completes the level.
public sealed class ExitFlag : SpecialObject
{
    private readonly SpriteAnimation sprite = new("graphics/special_objects/exit_flag", 5, 8);

    /// <summary>Set the first time the player touches the flag; PlayMode reads it to end the level.</summary>
    public bool Reached { get; private set; }

    public ExitFlag(Vector2 feet) : base(feet, 8, 16)
    {
        sprite.Origin = new Vector2(8, 32);
    }

    public override void Update(GameTime gameTime) => sprite.Update(gameTime);

    public override void Draw()
    {
        sprite.Position = Position;
        sprite.Draw();
    }

    public override void OnPlayerEnter(Player player) => Reached = true;
}
