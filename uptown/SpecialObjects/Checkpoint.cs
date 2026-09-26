using Microsoft.Xna.Framework;
using NodeTesting.models;

namespace uptown.SpecialObjects;

// 16x32 animated sprite, 5 frames. Touching it makes it the player's respawn point.
// The sprite is white so it can be tinted: the current checkpoint turns green.
public sealed class Checkpoint : SpecialObject
{
    private readonly SpriteAnimation sprite = new("graphics/special_objects/checkpoint", 5, 8);

    /// <summary>True for the checkpoint the player will respawn at. Only one is current at a time.</summary>
    public bool IsCurrent { get; private set; }

    public Checkpoint(Vector2 feet) : base(feet, 8, 16)
    {
        sprite.Origin = new Vector2(8, 32);
    }

    public override void Update(GameTime gameTime) => sprite.Update(gameTime);

    public override void Draw()
    {
        sprite.Position = Position;
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
