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
    private readonly Vector2 initialPosition;
    /// <summary>True while the player's hitbox overlaps this object's collider.</summary>
    public bool PlayerInside { get; internal set; }

    protected SpecialObject(Vector2 feet, int width, int height) : base(feet)
    {
        initialPosition = feet;
        // CollisionRect takes a center, so lift it by half the height.
        if (width > 0 && height > 0)
            Collider = new CollisionRect((int)feet.X, (int)feet.Y - height / 2, width, height);
    }

    /// <summary>
    /// Is the player touching this object right now? PlayMode asks every frame and fires the
    /// hooks below from the answer. By default the two CollisionRects must overlap; override it
    /// for another rule (distance, same tile, near + key press). Objects that override it can
    /// pass 0 for width or height to skip the collider.
    /// </summary>
    public virtual bool Touches(Player player) =>
        Collider != null && Collider.Intersects(player.Collider);

    /// <summary>The first frame the player touches this object.</summary>
    public virtual void OnPlayerEnter(Player player) { }

    /// <summary>Every frame the player keeps touching this object, including the first.</summary>
    public virtual void OnPlayerStay(Player player) { }

    /// <summary>The first frame the player stops touching this object.</summary>
    public virtual void OnPlayerExit(Player player) { }

    public virtual void Reset()
    {
        Vector2 delta = initialPosition - Position;
        Collider?.Translate((int)delta.X, (int)delta.Y);
        Position = initialPosition;
        PlayerInside = false;
        Active = Visible = true;
    }
}
