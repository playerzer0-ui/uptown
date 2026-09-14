using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using System.Collections.Generic;

namespace NodeTesting.models
{
    public class CollisionMap : Map
    {
        private List<CollisionRect> _collisionRects; // Store CollisionRect objects directly

        public CollisionMap(string texturePath, int tileWidth, int tileHeight, string csvPath)
            : base(texturePath, tileWidth, tileHeight)
        {
            LoadCSV(csvPath);
            _collisionRects = new List<CollisionRect>();
            BuildCollisionRectangles();
        }

        private void BuildCollisionRectangles()
        {
            for (int row = 0; row < MapHeight; row++)
            {
                for (int col = 0; col < MapWidth; col++)
                {
                    // 0 means solid collision (walls/borders)
                    if (MapData[row, col] == 0)
                    {
                        // Calculate the center position of the tile
                        int centerX = (col * TileWidth) + (TileWidth / 2);
                        int centerY = (row * TileHeight) + (TileHeight / 2);

                        // Create a CollisionRect for this tile
                        CollisionRect tileCollider = new CollisionRect(centerX, centerY, TileWidth, TileHeight);
                        tileCollider.IsStatic = true;
                        _collisionRects.Add(tileCollider);
                    }
                }
            }
        }

        /// <summary>
        /// Checks if a CollisionRect collides with any collision tiles (0 values)
        /// </summary>
        public bool CheckCollision(CollisionRect collider)
        {
            foreach (CollisionRect tileRect in _collisionRects)
            {
                if (collider.Intersects(tileRect))
                {
                    return true;
                }
            }
            return false;
        }

        /// <summary>
        /// Checks if a standard Rectangle collides with any collision tiles (0 values)
        /// </summary>
        public bool CheckCollision(Rectangle rectangle)
        {
            // Create a temporary CollisionRect from the rectangle
            CollisionRect tempCollider = new CollisionRect(
                rectangle.Center.X,
                rectangle.Center.Y,
                rectangle.Width,
                rectangle.Height
            );

            return CheckCollision(tempCollider);
        }

        /// <summary>
        /// Gets all collision rectangles for advanced collision handling
        /// </summary>
        public List<CollisionRect> GetCollisionRects()
        {
            return _collisionRects;
        }

        /// <summary>
        /// Gets all tile positions that a rectangle intersects with
        /// </summary>
        public List<Point> GetIntersectingTiles(Rectangle target)
        {
            List<Point> intersections = new List<Point>();

            // Calculate which tiles the rectangle overlaps
            int leftTile = target.Left / TileWidth;
            int rightTile = (target.Right - 1) / TileWidth;
            int topTile = target.Top / TileHeight;
            int bottomTile = (target.Bottom - 1) / TileHeight;

            for (int x = leftTile; x <= rightTile; x++)
            {
                for (int y = topTile; y <= bottomTile; y++)
                {
                    // Check if within map bounds
                    if (x >= 0 && x < MapWidth && y >= 0 && y < MapHeight)
                    {
                        // Check if this tile is a collision tile (0 in your case)
                        if (MapData[y, x] == 0)
                        {
                            intersections.Add(new Point(x, y));
                        }
                    }
                }
            }

            return intersections;
        }

        /// <summary>
        /// Resolves collision horizontally using the tile-based approach
        /// </summary>
        public void ResolveCollisionHorizontal(ref Rectangle rect, float velocityX)
        {
            List<Point> intersectingTiles = GetIntersectingTiles(rect);

            foreach (Point tilePos in intersectingTiles)
            {
                Rectangle tileRect = new Rectangle(
                    tilePos.X * TileWidth,
                    tilePos.Y * TileHeight,
                    TileWidth,
                    TileHeight
                );

                if (velocityX > 0) // Moving right
                {
                    rect.X = tileRect.Left - rect.Width;
                }
                else if (velocityX < 0) // Moving left
                {
                    rect.X = tileRect.Right;
                }
            }
        }

        /// <summary>
        /// Resolves collision vertically using the tile-based approach
        /// </summary>
        public void ResolveCollisionVertical(ref Rectangle rect, float velocityY)
        {
            List<Point> intersectingTiles = GetIntersectingTiles(rect);

            foreach (Point tilePos in intersectingTiles)
            {
                Rectangle tileRect = new Rectangle(
                    tilePos.X * TileWidth,
                    tilePos.Y * TileHeight,
                    TileWidth,
                    TileHeight
                );

                if (velocityY > 0) // Moving down
                {
                    rect.Y = tileRect.Top - rect.Height;
                }
                else if (velocityY < 0) // Moving up
                {
                    rect.Y = tileRect.Bottom;
                }
            }
        }

        /// <summary>
        /// Override Draw - draws the collision tiles (0 values) so you can see them for debugging
        /// </summary>
        public override void Draw()
        {
            for (int row = 0; row < MapHeight; row++)
            {
                for (int col = 0; col < MapWidth; col++)
                {
                    int tileId = MapData[row, col];
                    // Draw only collision tiles (0) and any other non-negative tiles
                    // Skip -1 because those are empty spaces
                    if (tileId >= 0 && TileSources.ContainsKey(tileId))
                    {
                        Vector2 position = new Vector2(col * TileWidth, row * TileHeight);
                        Globals.spriteBatch.Draw(Texture, position, TileSources[tileId], Color.White);
                    }
                }
            }
        }

        /// <summary>
        /// Draws all collision rectangles for debugging purposes
        /// </summary>
        public void DrawAllCollisionRects(Color color)
        {
            foreach (CollisionRect collisionRect in _collisionRects)
            {
                collisionRect.Draw(color);
            }
        }
    }
}