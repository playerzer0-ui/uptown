using Microsoft.Xna.Framework;

namespace NodeTesting.models
{
    /// <summary>
    /// A thing that lives in a level: it has a position, updates every frame and draws itself.
    /// Subclass it and override only what you need, then add it to an <see cref="EntityList"/>.
    /// </summary>
    public abstract class Entity
    {
        /// <summary>World position. What this point means (center, feet, ...) is up to the subclass.</summary>
        public Vector2 Position { get; set; }

        /// <summary>Optional hitbox. Null means this entity never collides with anything.</summary>
        public CollisionRect Collider { get; protected set; }

        /// <summary>When false, <see cref="Update"/> is skipped.</summary>
        public bool Active { get; set; } = true;

        /// <summary>When false, <see cref="Draw"/> is skipped.</summary>
        public bool Visible { get; set; } = true;

        /// <summary>The list this entity belongs to, or null before it is added.</summary>
        public EntityList Scene { get; internal set; }

        protected Entity(Vector2 position)
        {
            Position = position;
        }

        /// <summary>Called once, right after the entity joins a list.</summary>
        public virtual void Added() { }

        /// <summary>Called once, right after the entity leaves its list.</summary>
        public virtual void Removed() { }

        public virtual void Update(GameTime gameTime) { }

        public virtual void Draw() { }

        /// <summary>Removes this entity from its list at the end of the current update.</summary>
        public void RemoveSelf() => Scene?.Remove(this);
    }
}
