using Microsoft.Xna.Framework;
using NodeTesting.models;

namespace uptown.SpecialObjects;

/// <summary>
/// An entity placed in a level that reacts to the player: checkpoints, exit flags,
/// and later springs, moving platforms, boost circles, breakable blocks...
/// Position is the bottom-center (feet), like the player, so objects stand on tiles.
/// PlayMode calls the touch hooks; override only the ones you need.
/// </summary>
public abstract class SpecialObject : Entity
{
    /// <summary>True while the player's hitbox overlaps this object's collider.</summary>
    public bool PlayerInside { get; internal set; }

    protected SpecialObject(Vector2 feet, int width, int height) : base(feet)
    {
        // CollisionRect takes a center, so lift it by half the height.
        Collider = new CollisionRect((int)feet.X, (int)feet.Y - height / 2, width, height);
    }

    /// <summary>The first frame the player touches this object.</summary>
    public virtual void OnPlayerEnter(Player player) { }

    /// <summary>Every frame the player keeps touching this object, including the first.</summary>
    public virtual void OnPlayerStay(Player player) { }

    /// <summary>The first frame the player stops touching this object.</summary>
    public virtual void OnPlayerExit(Player player) { }
}
