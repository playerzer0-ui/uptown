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

        public CollisionMap(string texturePath, int tileWidth, int tileHeight, int[,] grid)
            : base(texturePath, tileWidth, tileHeight)
        {
            MapData = (int[,])grid.Clone();
            MapHeight = grid.GetLength(0);
            MapWidth = grid.GetLength(1);
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
        public bool CheckCollision(CollisionRect collider) => CheckCollision(collider.Rect);

        /// <summary>
        /// Checks if a standard Rectangle collides with any collision tiles (0 values)
        /// </summary>
        public bool CheckCollision(Rectangle rectangle)
        {
            if (rectangle.Width <= 0 || rectangle.Height <= 0) return false;

            // Only look at the grid cells under the rectangle.
            int leftTile = FloorDiv(rectangle.Left, TileWidth);
            int rightTile = FloorDiv(rectangle.Right - 1, TileWidth);
            int topTile = FloorDiv(rectangle.Top, TileHeight);
            int bottomTile = FloorDiv(rectangle.Bottom - 1, TileHeight);

            for (int y = topTile; y <= bottomTile; y++)
                for (int x = leftTile; x <= rightTile; x++)
                    if (IsSolidTile(x, y)) return true;
            return false;
        }

        /// <summary>
        /// Checks if a world-space pixel lies inside a collision tile (0 values)
        /// </summary>
        public bool IsSolidAt(int x, int y) => IsSolidTile(FloorDiv(x, TileWidth), FloorDiv(y, TileHeight));

        private bool IsSolidTile(int x, int y) =>
            x >= 0 && x < MapWidth && y >= 0 && y < MapHeight && MapData[y, x] == 0;

        // Integer division that rounds toward negative infinity, so -1 / 8 is tile -1, not 0.
        private static int FloorDiv(int value, int divisor) =>
            value >= 0 ? value / divisor : (value - divisor + 1) / divisor;

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
            int leftTile = FloorDiv(target.Left, TileWidth);
            int rightTile = FloorDiv(target.Right - 1, TileWidth);
            int topTile = FloorDiv(target.Top, TileHeight);
            int bottomTile = FloorDiv(target.Bottom - 1, TileHeight);

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
            if (intersectingTiles.Count == 0 || velocityX == 0) return;

            // Snap against the nearest tile in the direction of travel.
            int x = rect.X;
            foreach (Point tilePos in intersectingTiles)
            {
                if (velocityX > 0) // Moving right: the leftmost tile edge wins
                    x = System.Math.Min(x, tilePos.X * TileWidth - rect.Width);
                else // Moving left: the rightmost tile edge wins
                    x = System.Math.Max(x, (tilePos.X + 1) * TileWidth);
            }
            rect.X = x;
        }

        /// <summary>
        /// Resolves collision vertically using the tile-based approach
        /// </summary>
        public void ResolveCollisionVertical(ref Rectangle rect, float velocityY)
        {
            List<Point> intersectingTiles = GetIntersectingTiles(rect);
            if (intersectingTiles.Count == 0 || velocityY == 0) return;

            // Snap against the nearest tile in the direction of travel.
            int y = rect.Y;
            foreach (Point tilePos in intersectingTiles)
            {
                if (velocityY > 0) // Moving down: the topmost tile edge wins
                    y = System.Math.Min(y, tilePos.Y * TileHeight - rect.Height);
                else // Moving up: the bottommost tile edge wins
                    y = System.Math.Max(y, (tilePos.Y + 1) * TileHeight);
            }
            rect.Y = y;
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