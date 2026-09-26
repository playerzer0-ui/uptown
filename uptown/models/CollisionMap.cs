using Microsoft.Xna.Framework;
using System;
using System.Collections.Generic;

namespace NodeTesting.models
{
    /// <summary>
    /// Tile-based collision map. Instead of storing one collider per solid tile and
    /// testing all of them, it looks up only the tiles a collider actually overlaps.
    /// Implements <see cref="ICollider"/> so it can sit alongside other static colliders.
    /// </summary>
    public class CollisionMap : Map, ICollider
    {
        // 0 means solid collision (walls/borders)
        private const int SolidTile = 0;

        // Reused for every tile test so no allocations happen per frame
        private readonly CollisionRect _probe;

        public CollisionMap(string texturePath, int tileWidth, int tileHeight, string csvPath)
            : base(texturePath, tileWidth, tileHeight)
        {
            LoadCSV(csvPath);
            _probe = new CollisionRect(0, 0, TileWidth, TileHeight) { IsStatic = true };
        }

        public CollisionMap(string texturePath, int tileWidth, int tileHeight, int[,] grid)
            : base(texturePath, tileWidth, tileHeight)
        {
            MapData = (int[,])grid.Clone();
            MapHeight = grid.GetLength(0);
            MapWidth = grid.GetLength(1);
            _probe = new CollisionRect(0, 0, TileWidth, TileHeight) { IsStatic = true };
        }

        public bool IsStatic { get => true; set { } }

        /// <summary>
        /// Returns true if the tile at (col, row) is solid. Out-of-bounds is treated as empty.
        /// </summary>
        public bool IsSolid(int col, int row)
        {
            return col >= 0 && col < MapWidth && row >= 0 && row < MapHeight
                && MapData[row, col] == SolidTile;
        }

        /// <summary>
        /// Returns true if the world-space pixel (x, y) lies inside a solid tile.
        /// </summary>
        public bool IsSolidAt(int x, int y) => IsSolid(FloorDiv(x, TileWidth), FloorDiv(y, TileHeight));

        /// <summary>
        /// Gets the tile index range covered by a world-space rectangle, clamped to the map.
        /// </summary>
        private void GetTileRange(Rectangle area, out int left, out int top, out int right, out int bottom)
        {
            left = Math.Max(0, FloorDiv(area.Left, TileWidth));
            top = Math.Max(0, FloorDiv(area.Top, TileHeight));
            right = Math.Min(MapWidth - 1, FloorDiv(area.Right - 1, TileWidth));
            bottom = Math.Min(MapHeight - 1, FloorDiv(area.Bottom - 1, TileHeight));
        }

        // Integer division that rounds toward negative infinity, so negative coords map to the correct tile
        private static int FloorDiv(int a, int b) => (a >= 0) ? a / b : ((a + 1) / b) - 1;

        private static Rectangle GetBounds(ICollider collider)
        {
            switch (collider)
            {
                case CollisionRect r:
                    return r.Rect;
                case CollisionCircle c:
                    return new Rectangle(
                        (int)(c.Center.X - c.Radius), (int)(c.Center.Y - c.Radius),
                        c.Radius * 2, c.Radius * 2);
                default:
                    throw new NotSupportedException("Unsupported collider type.");
            }
        }

        /// <summary>
        /// Moves the reusable probe onto the given tile.
        /// </summary>
        private void PlaceProbe(int col, int row)
        {
            _probe.UpdateRect(col * TileWidth + TileWidth / 2, row * TileHeight + TileHeight / 2);
        }

        /// <summary>
        /// Checks whether a collider overlaps any solid tile. Only the tiles under its bounds are tested.
        /// </summary>
        public bool Intersects(ICollider other)
        {
            GetTileRange(GetBounds(other), out int l, out int t, out int r, out int b);

            for (int row = t; row <= b; row++)
            {
                for (int col = l; col <= r; col++)
                {
                    if (!IsSolid(col, row)) continue;
                    PlaceProbe(col, row);
                    if (_probe.Intersects(other)) return true;
                }
            }
            return false;
        }

        /// <summary>
        /// Checks if a standard Rectangle collides with any collision tiles (0 values)
        /// </summary>
        public bool CheckCollision(Rectangle rectangle)
        {
            if (rectangle.Width <= 0 || rectangle.Height <= 0) return false;
            GetTileRange(rectangle, out int l, out int t, out int r, out int b);

            for (int row = t; row <= b; row++)
                for (int col = l; col <= r; col++)
                    if (IsSolid(col, row)) return true;

            return false;
        }

        /// <summary>
        /// Checks if a CollisionRect collides with any collision tiles (0 values)
        /// </summary>
        public bool CheckCollision(CollisionRect collider) => CheckCollision(collider.Rect);

        public bool Contains(Point point) => IsSolidAt(point.X, point.Y);

        /// <summary>
        /// Pushes the moving collider out of every nearby solid tile.
        /// Returns the total correction applied.
        /// </summary>
        public Vector2 ResolveAgainst(ICollider moving)
        {
            Rectangle bounds = GetBounds(moving);
            // Expand by one tile since earlier pushes can move the collider into a neighbouring tile
            bounds.Inflate(TileWidth, TileHeight);
            GetTileRange(bounds, out int l, out int t, out int r, out int b);

            Vector2 total = Vector2.Zero;
            for (int row = t; row <= b; row++)
            {
                for (int col = l; col <= r; col++)
                {
                    if (!IsSolid(col, row)) continue;
                    PlaceProbe(col, row);
                    total += _probe.ResolveAgainst(moving);
                }
            }
            return total;
        }

        /// <summary>
        /// Gets all tile positions that a rectangle intersects with
        /// </summary>
        public List<Point> GetIntersectingTiles(Rectangle target)
        {
            List<Point> intersections = new List<Point>();
            GetTileRange(target, out int l, out int t, out int r, out int b);

            for (int x = l; x <= r; x++)
                for (int y = t; y <= b; y++)
                    if (IsSolid(x, y))
                        intersections.Add(new Point(x, y));

            return intersections;
        }

        /// <summary>
        /// Resolves collision horizontally using the tile-based approach
        /// </summary>
        public void ResolveCollisionHorizontal(ref Rectangle rect, float velocityX)
        {
            if (velocityX == 0) return;

            // Snap against the nearest tile in the direction of travel.
            int x = rect.X;
            foreach (Point tilePos in GetIntersectingTiles(rect))
            {
                if (velocityX > 0) // Moving right: the leftmost tile edge wins
                    x = Math.Min(x, tilePos.X * TileWidth - rect.Width);
                else // Moving left: the rightmost tile edge wins
                    x = Math.Max(x, (tilePos.X + 1) * TileWidth);
            }
            rect.X = x;
        }

        /// <summary>
        /// Resolves collision vertically using the tile-based approach
        /// </summary>
        public void ResolveCollisionVertical(ref Rectangle rect, float velocityY)
        {
            if (velocityY == 0) return;

            // Snap against the nearest tile in the direction of travel.
            int y = rect.Y;
            foreach (Point tilePos in GetIntersectingTiles(rect))
            {
                if (velocityY > 0) // Moving down: the topmost tile edge wins
                    y = Math.Min(y, tilePos.Y * TileHeight - rect.Height);
                else // Moving up: the bottommost tile edge wins
                    y = Math.Max(y, (tilePos.Y + 1) * TileHeight);
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
        /// Draws all solid tiles as collision rects for debugging purposes
        /// </summary>
        public void Draw(Color color)
        {
            for (int row = 0; row < MapHeight; row++)
            {
                for (int col = 0; col < MapWidth; col++)
                {
                    if (!IsSolid(col, row)) continue;
                    PlaceProbe(col, row);
                    _probe.Draw(color);
                }
            }
        }

        public void DrawAllCollisionRects(Color color) => Draw(color);
    }
}
