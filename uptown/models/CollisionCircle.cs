using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Content;
using Microsoft.Xna.Framework.Graphics;
using System;

namespace NodeTesting.models
{
    /// <summary>
    /// Represents a circular collision shape used for detecting overlaps and containment.
    /// </summary>
    public class CollisionCircle : ICollider
    {
        private Vector2 center;
        private int radius;
        private Texture2D pixel;
        private int offsetX;
        private int offsetY;

        /// <summary>
        /// Initializes a new instance of the <see cref="CollisionCircle"/> class.
        /// </summary>
        /// <param name="x">The x-coordinate of the circle's center.</param>
        /// <param name="y">The y-coordinate of the circle's center.</param>
        /// <param name="radius">The radius of the circle, in pixels.</param>
        public CollisionCircle(int x, int y, int radius)
        {
            this.center = new Vector2(x, y);
            this.radius = radius;
            pixel = new Texture2D(Globals.graphics.GraphicsDevice, 1, 1);
            pixel.SetData(new[] { Color.White });
        }

        /// <summary>
        /// Gets or sets the center position of the circle.
        /// </summary>
        public Vector2 Center { get => center; set => center = value; }

        /// <summary>
        /// Gets or sets the radius of the circle.
        /// </summary>
        public int Radius { get => radius; set => radius = value; }

        public bool IsStatic { get; set; } = false;

        /// <summary>
        /// Determines whether this circle intersects with another collider.
        /// </summary>
        /// <param name="other">Another collider to test against.</param>
        /// <returns><see langword="true"/> if the colliders overlap; otherwise, <see langword="false"/>.</returns>
        public bool Intersects(ICollider other)
        {
            switch (other)
            {
                case CollisionCircle c:
                    float distSq = Vector2.DistanceSquared(center, c.Center);
                    float radiusSum = radius + c.Radius;
                    return distSq < (radiusSum * radiusSum);

                case CollisionRect r:
                    return Collides(r.Rect);

                default:
                    throw new NotSupportedException("Unsupported collider type.");
            }
        }

        /// <summary>
        /// Sets an additional offset applied when updating the circle's position.
        /// </summary>
        public void SetOffsetExtra(int x, int y)
        {
            offsetX = x;
            offsetY = y;
        }

        /// <summary>
        /// Updates the circle's center position, applying any extra offsets.
        /// </summary>
        public void UpdateCenter(int x, int y)
        {
            center.X = x + offsetX;
            center.Y = y + offsetY;
        }

        /// <summary>
        /// Determines whether this circle overlaps a given rectangle.
        /// </summary>
        /// <param name="target">The rectangle to test against.</param>
        /// <returns><see langword="true"/> if the circle overlaps the rectangle; otherwise, <see langword="false"/>.</returns>
        public bool Collides(Rectangle target)
        {
            float closestX = MathHelper.Clamp(center.X, target.Left, target.Right);
            float closestY = MathHelper.Clamp(center.Y, target.Top, target.Bottom);

            float dx = center.X - closestX;
            float dy = center.Y - closestY;

            return (dx * dx + dy * dy) < (radius * radius);
        }

        /// <summary>
        /// Checks whether a given point lies inside the circle.
        /// </summary>
        /// <param name="target">The point to test.</param>
        /// <returns><see langword="true"/> if the point is within the circle's radius; otherwise, <see langword="false"/>.</returns>
        public bool Contains(Point target)
        {
            float dx = target.X - center.X;
            float dy = target.Y - center.Y;
            return (dx * dx + dy * dy) < (radius * radius);
        }

        /// <summary>
        /// Draws the circular collider outline for debugging or visualization.
        /// </summary>
        /// <param name="color">The color to draw the collider with.</param>
        public void Draw(Color color)
        {
            int segments = 30;
            Vector2[] points = new Vector2[segments];

            for (int i = 0; i < segments; i++)
            {
                float angle = i * MathHelper.TwoPi / segments;
                points[i] = center + new Vector2(
                    (float)Math.Cos(angle),
                    (float)Math.Sin(angle)
                ) * radius;
            }

            for (int i = 0; i < segments - 1; i++)
                DrawLine(points[i], points[i + 1], color);

            DrawLine(points[^1], points[0], color);
        }

        /// <summary>
        /// Draws a line segment between two points. Used internally for rendering the circle outline.
        /// </summary>
        /// <param name="start">The start position of the line.</param>
        /// <param name="end">The end position of the line.</param>
        /// <param name="color">The color to draw the line with.</param>
        /// <param name="thickness">The thickness of the line in pixels. Default is 2.</param>
        private void DrawLine(Vector2 start, Vector2 end, Color color, int thickness = 2)
        {
            Vector2 delta = end - start;
            float angle = (float)Math.Atan2(delta.Y, delta.X);
            float length = delta.Length();

            Globals.spriteBatch.Draw(
                pixel,
                start,
                null,
                color,
                angle,
                Vector2.Zero,
                new Vector2(length, thickness),
                SpriteEffects.None,
                0
            );
        }

        public Vector2 ResolveAgainst(ICollider moving)
        {
            if (!IsStatic || !Intersects(moving)) return Vector2.Zero;

            switch (moving)
            {
                case CollisionCircle c:
                    return ResolveCircleVsCircle(c);
                case CollisionRect r:
                    // Delegate to rect's own resolve logic (rect vs circle is handled there)
                    return ResolveCircleVsRect(r);
                default:
                    return Vector2.Zero;
            }
        }

        private Vector2 ResolveCircleVsCircle(CollisionCircle moving)
        {
            Vector2 delta = moving.Center - center;
            float distSq = delta.LengthSquared();
            float minDist = radius + moving.Radius;

            if (distSq >= minDist * minDist) return Vector2.Zero;

            float dist = (float)Math.Sqrt(distSq);
            Vector2 push = (dist > 0 ? delta / dist : Vector2.UnitX) * (minDist - dist);
            moving.Center += push;
            return push;
        }

        private Vector2 ResolveCircleVsRect(CollisionRect moving)
        {
            // Closest point on the moving rect to this circle
            float closestX = MathHelper.Clamp(center.X, moving.Rect.Left, moving.Rect.Right);
            float closestY = MathHelper.Clamp(center.Y, moving.Rect.Top, moving.Rect.Bottom);

            Vector2 delta = new Vector2(closestX, closestY) - center;
            float distSq = delta.LengthSquared();

            if (distSq >= radius * radius) return Vector2.Zero;

            float dist = (float)Math.Sqrt(distSq);
            // Push the rect away from this circle
            Vector2 push = (dist > 0 ? delta / dist : Vector2.UnitY) * (radius - dist);
            moving.Translate((int)push.X, (int)push.Y);
            return push;
        }
    }

}
