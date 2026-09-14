using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Content;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using System;

namespace NodeTesting.models
{
    /// <summary>
    /// Represents a rectangular collision shape used for detecting overlaps and containment.
    /// </summary>
    public class CollisionRect : ICollider
    {
        private Rectangle rect;
        private Texture2D pixel;
        private int offsetX;
        private int offsetY;
        private int width;
        private int height;

        /// <summary>
        /// Initializes a new instance of the <see cref="CollisionRect"/> class.
        /// </summary>
        /// <param name="x">The x-coordinate of the rectangle's center.</param>
        /// <param name="y">The y-coordinate of the rectangle's center.</param>
        /// <param name="width">The width of the rectangle, in pixels.</param>
        /// <param name="height">The height of the rectangle, in pixels.</param>
        public CollisionRect(int x, int y, int width, int height)
        {
            this.width = width;
            this.height = height;
            rect = new Rectangle(x, y, width, height);
            pixel = new Texture2D(Globals.graphics.GraphicsDevice, 1, 1);
            pixel.SetData(new[] { Color.White });
            rect.Offset(-(width / 2), -(height / 2));
        }

        /// <summary>
        /// Gets the internal <see cref="Rectangle"/> that defines this collider's bounds.
        /// </summary>
        public Rectangle Rect => rect;
        public bool IsStatic { get; set; } = false;

        public Vector2 Center => new Vector2(Pos.X + this.width / 2f, Pos.Y + this.height / 2f);

        /// <summary>
        /// Gets the top-left position of the rectangle in world space.
        /// </summary>
        public Vector2 Pos => new(rect.X, rect.Y);

        /// <summary>
        /// Checks whether this rectangle intersects with another collider.
        /// </summary>
        /// <param name="other">Another collider to test against.</param>
        /// <returns><see langword="true"/> if the colliders overlap; otherwise, <see langword="false"/>.</returns>
        public bool Intersects(ICollider other)
        {
            switch (other)
            {
                case CollisionRect r:
                    return rect.Intersects(r.Rect);
                case CollisionCircle c:
                    return c.Collides(rect);
                default:
                    throw new NotSupportedException("Unsupported collider type.");
            }
        }

        /// <summary>
        /// Checks whether a given point lies within this rectangle.
        /// </summary>
        /// <param name="target">The point to test.</param>
        /// <returns><see langword="true"/> if the point is contained within the rectangle; otherwise, <see langword="false"/>.</returns>
        public bool Contains(Point target)
        {
            return rect.Contains(target);
        }

        /// <summary>
        /// Sets an additional offset applied when updating the rectangle's position.
        /// </summary>
        /// <param name="x">The horizontal offset, in pixels.</param>
        /// <param name="y">The vertical offset, in pixels.</param>
        public void SetOffsetExtra(int x, int y)
        {
            offsetX = x;
            offsetY = y;
        }

        /// <summary>
        /// Updates the rectangle's position while keeping it centered and applying any extra offsets.
        /// </summary>
        /// <param name="x">The new x-coordinate of the rectangle's center.</param>
        /// <param name="y">The new y-coordinate of the rectangle's center.</param>
        public void UpdateRect(int x, int y)
        {
            rect.X = x - (rect.Width / 2) + offsetX;
            rect.Y = y - (rect.Height / 2) + offsetY;
        }

        /// <summary>
        /// Draws the rectangle collider for debugging or visualization.
        /// </summary>
        /// <param name="color">The color to draw the collider with.</param>
        public void Draw(Color color)
        {
            Globals.spriteBatch.Draw(pixel, rect, color);
        }

        public Vector2 ResolveAgainst(ICollider moving)
        {
            if (!IsStatic || !Intersects(moving)) return Vector2.Zero;

            switch (moving)
            {
                case CollisionRect r:
                    return ResolveRectVsRect(r);
                case CollisionCircle c:
                    return ResolveRectVsCircle(c);
                default:
                    return Vector2.Zero;
            }
        }

        private Vector2 ResolveRectVsRect(CollisionRect moving)
        {
            int overlapLeft = moving.Rect.Right - rect.Left;
            int overlapRight = rect.Right - moving.Rect.Left;
            int overlapTop = moving.Rect.Bottom - rect.Top;
            int overlapBottom = rect.Bottom - moving.Rect.Top;

            if (Math.Min(overlapLeft, overlapRight) < Math.Min(overlapTop, overlapBottom))
            {
                int pushX = overlapLeft < overlapRight ? -overlapLeft : overlapRight;
                moving.Translate(pushX, 0);
                return new Vector2(pushX, 0);
            }
            else
            {
                int pushY = overlapTop < overlapBottom ? -overlapTop : overlapBottom;
                moving.Translate(0, pushY);
                return new Vector2(0, pushY);
            }
        }

        private Vector2 ResolveRectVsCircle(CollisionCircle moving)
        {
            // Find the closest point on this rect to the circle center
            float closestX = MathHelper.Clamp(moving.Center.X, rect.Left, rect.Right);
            float closestY = MathHelper.Clamp(moving.Center.Y, rect.Top, rect.Bottom);

            Vector2 delta = moving.Center - new Vector2(closestX, closestY);
            float distSq = delta.LengthSquared();

            if (distSq >= moving.Radius * moving.Radius) return Vector2.Zero;

            float dist = (float)Math.Sqrt(distSq);
            Vector2 push = (dist > 0 ? delta / dist : Vector2.UnitY) * (moving.Radius - dist);
            moving.Center += push;
            return push;
        }

        public void Translate(int dx, int dy)
        {
            rect.X += dx;
            rect.Y += dy;
        }
    }

}
