using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Content;
using Microsoft.Xna.Framework.Graphics;

namespace NodeTesting.models
{
    public interface ICollider
    {
        bool IsStatic { get; set; }
        bool Intersects(ICollider other);
        bool Contains(Point point);
        void Draw(Color color);

        /// <summary>
        /// If this collider is static, resolves the moving collider out of it.
        /// Returns the correction vector, or Vector2.Zero if not static / no overlap.
        /// </summary>
        Vector2 ResolveAgainst(ICollider moving);
    }

}
