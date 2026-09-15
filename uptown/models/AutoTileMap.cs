using Microsoft.Xna.Framework;

namespace NodeTesting.models;

// Basic terrain atlas: 3x3 outer tiles, plus inner DR/DL/UR/UL at IDs 3/4/8/9.
public sealed class AutoTileMap : Map
{
    private Rectangle[,,] quarters;
    private static readonly int[] Outer = { 0, 2, 10, 12 };
    private static readonly int[] Sides = { 5, 7, 5, 7 };
    private static readonly int[] Caps = { 1, 1, 11, 11 };
    private static readonly int[] Inner = { 9, 8, 4, 3 };

    public AutoTileMap(string texturePath, string csvPath) : base(texturePath, 8, 8)
    {
        LoadCSV(csvPath);
        quarters = new Rectangle[Height, Width, 4];
        for (int y = 0; y < Height; y++)
            for (int x = 0; x < Width; x++) Refresh(x, y);
    }

    public AutoTileMap(string texturePath, int[,] grid) : base(texturePath, 8, 8)
    {
        MapData = (int[,])grid.Clone();
        MapHeight = grid.GetLength(0);
        MapWidth = grid.GetLength(1);
        quarters = new Rectangle[Height, Width, 4];
        for (int y = 0; y < Height; y++)
            for (int x = 0; x < Width; x++) Refresh(x, y);
    }

    public bool InBounds(int x, int y) => x >= 0 && y >= 0 && x < Width && y < Height;
    public bool Occupied(int x, int y) => InBounds(x, y) && MapData[y, x] >= 0;

    // Extension only: preserve every existing cell and leave new space empty.
    public void Expand(int width, int height)
    {
        if (width < Width || height < Height || (long)width * height > 1048576)
            throw new System.ArgumentOutOfRangeException(nameof(width), "Expansion must preserve the level and stay within 1,048,576 cells.");
        if (width == Width && height == Height) return;
        var grid = new int[height, width];
        for (int y = 0; y < height; y++)
            for (int x = 0; x < width; x++)
                grid[y, x] = InBounds(x, y) ? MapData[y, x] : -1;
        MapData = grid;
        MapWidth = width;
        MapHeight = height;
        quarters = new Rectangle[height, width, 4];
        for (int y = 0; y < height; y++)
            for (int x = 0; x < width; x++) Refresh(x, y);
    }

    public void Paint(int x, int y, bool solid)
    {
        if (!InBounds(x, y) || Occupied(x, y) == solid) return;
        MapData[y, x] = solid ? 6 : -1;
        for (int row = y - 1; row <= y + 1; row++)
            for (int col = x - 1; col <= x + 1; col++) Refresh(col, row);
    }

    // Each quadrant uses its horizontal, vertical, and diagonal neighbors.
    public static int SelectTile(int quadrant, bool horizontal, bool vertical, bool diagonal)
    {
        if (!horizontal && !vertical) return Outer[quadrant];
        if (!horizontal) return Sides[quadrant];
        if (!vertical) return Caps[quadrant];
        return diagonal ? 6 : Inner[quadrant];
    }

    private void Refresh(int x, int y)
    {
        if (!Occupied(x, y)) return;
        for (int q = 0; q < 4; q++)
        {
            int dx = q % 2 == 0 ? -1 : 1;
            int dy = q < 2 ? -1 : 1;
            int id = SelectTile(q, Occupied(x + dx, y), Occupied(x, y + dy), Occupied(x + dx, y + dy));
            Rectangle source = TileSources[id];
            quarters[y, x, q] = new Rectangle(source.X + q % 2 * 4, source.Y + q / 2 * 4, 4, 4);
        }
    }

    public override void Draw()
    {
        for (int y = 0; y < Height; y++)
            for (int x = 0; x < Width; x++)
            {
                if (!Occupied(x, y)) continue;
                for (int q = 0; q < 4; q++)
                    Globals.spriteBatch.Draw(Texture, new Vector2(x * 8 + q % 2 * 4, y * 8 + q / 2 * 4),
                        quarters[y, x, q], Color.White);
            }
    }
}
