using System.Collections;
using System.Collections.Generic;
using Microsoft.Xna.Framework;

namespace NodeTesting.models
{
    /// <summary>
    /// Owns a set of entities and updates and draws them in the order they were added.
    /// Adds and removals made during <see cref="Update"/> are applied after the loop,
    /// so an entity can safely spawn or remove others (or itself) while updating.
    /// </summary>
    public class EntityList : IEnumerable<Entity>
    {
        private readonly List<Entity> entities = new();
        private readonly List<Entity> toAdd = new();
        private readonly List<Entity> toRemove = new();
        private bool updating;

        public int Count => entities.Count;

        public void Add(Entity entity)
        {
            if (entity.Scene != null) return;
            entity.Scene = this;
            if (updating) toAdd.Add(entity);
            else Attach(entity);
        }

        public void Remove(Entity entity)
        {
            if (entity.Scene != this) return;
            // Added and removed in the same update: it never really joined.
            if (toAdd.Remove(entity)) { entity.Scene = null; return; }
            if (updating) { if (!toRemove.Contains(entity)) toRemove.Add(entity); }
            else Detach(entity);
        }

        public void Update(GameTime gameTime)
        {
            updating = true;
            foreach (var entity in entities)
                if (entity.Active && entity.Scene == this) entity.Update(gameTime);
            updating = false;

            foreach (var entity in toRemove) Detach(entity);
            toRemove.Clear();
            foreach (var entity in toAdd) if (entity.Scene == this) Attach(entity);
            toAdd.Clear();
        }

        public void Draw()
        {
            foreach (var entity in entities)
                if (entity.Visible) entity.Draw();
        }

        /// <summary>Every entity of type <typeparamref name="T"/> whose collider overlaps <paramref name="area"/>.</summary>
        public IEnumerable<T> Colliding<T>(Rectangle area) where T : Entity
        {
            foreach (var entity in entities)
                if (entity is T match && entity.Collider != null && entity.Collider.Rect.Intersects(area))
                    yield return match;
        }

        public IEnumerable<T> OfType<T>() where T : Entity
        {
            foreach (var entity in entities)
                if (entity is T match) yield return match;
        }

        public IEnumerator<Entity> GetEnumerator() => entities.GetEnumerator();
        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

        private void Attach(Entity entity)
        {
            entities.Add(entity);
            entity.Added();
        }

        private void Detach(Entity entity)
        {
            entities.Remove(entity);
            toAdd.Remove(entity);
            entity.Scene = null;
            entity.Removed();
        }
    }
}
