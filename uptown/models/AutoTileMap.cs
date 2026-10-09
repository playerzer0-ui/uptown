using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using System;

namespace NodeTesting.models;

// Basic terrain atlas: 3x3 outer tiles, plus inner DR/DL/UR/UL at IDs 3/4/8/9.
public sealed class AutoTileMap : Map
{
    private Rectangle[,,] quarters;
    private int[,] materials;
    private Texture2D[] textures;
    public string[] Tilesets { get; private set; }
    private static readonly int[] Outer = { 0, 2, 10, 12 };
    private static readonly int[] Sides = { 5, 7, 5, 7 };
    private static readonly int[] Caps = { 1, 1, 11, 11 };
    private static readonly int[] Inner = { 9, 8, 4, 3 };

    public AutoTileMap(string texturePath, string csvPath) : base(texturePath, 8, 8)
    {
        LoadCSV(csvPath);
        InitializeMaterials(texturePath);
        quarters = new Rectangle[Height, Width, 4];
        for (int y = 0; y < Height; y++)
            for (int x = 0; x < Width; x++) Refresh(x, y);
    }

    public AutoTileMap(string texturePath, int[,] grid) : base(texturePath, 8, 8)
    {
        MapData = (int[,])grid.Clone();
        MapHeight = grid.GetLength(0);
        MapWidth = grid.GetLength(1);
        InitializeMaterials(texturePath);
        quarters = new Rectangle[Height, Width, 4];
        for (int y = 0; y < Height; y++)
            for (int x = 0; x < Width; x++) Refresh(x, y);
    }

    public bool InBounds(int x, int y) => x >= 0 && y >= 0 && x < Width && y < Height;
    public bool Occupied(int x, int y) => InBounds(x, y) && MapData[y, x] >= 0;
    public int MaterialAt(int x, int y) => Occupied(x, y) ? materials?[y, x] ?? 0 : -1;

    private void InitializeMaterials(string texturePath)
    {
        materials = new int[Height, Width];
        textures = new[] { Texture };
        Tilesets = new[] { texturePath.Substring(texturePath.LastIndexOf('/') + 1) };
    }

    public AutoTileMap(string[] texturePaths, int[,] grid, int[,] materialGrid)
        : this(texturePaths[0], grid)
    {
        if (materialGrid.GetLength(0) != Height || materialGrid.GetLength(1) != Width)
            throw new ArgumentException("Material grid must match terrain dimensions.");
        materials = (int[,])materialGrid.Clone();
        textures = new Texture2D[texturePaths.Length];
        Tilesets = new string[texturePaths.Length];
        for (int i = 0; i < textures.Length; i++)
        {
            textures[i] = Globals.Content.Load<Texture2D>(texturePaths[i]);
            if (textures[i].Width != Texture.Width || textures[i].Height != Texture.Height)
                throw new ArgumentException("Terrain atlases must share the same layout.");
            Tilesets[i] = texturePaths[i].Substring(texturePaths[i].LastIndexOf('/') + 1);
        }
        for (int y = 0; y < Height; y++)
            for (int x = 0; x < Width; x++)
            {
                if (materials[y, x] < 0 || materials[y, x] >= textures.Length)
                    throw new ArgumentException("Unknown terrain material.");
                Refresh(x, y);
            }
    }

    // Extension only: preserve every existing cell and leave new space empty.
    public void Expand(int width, int height)
    {
        if (width < Width || height < Height || (long)width * height > 1048576)
            throw new System.ArgumentOutOfRangeException(nameof(width), "Expansion must preserve the level and stay within 1,048,576 cells.");
        if (width == Width && height == Height) return;
        var grid = new int[height, width];
        var expandedMaterials = new int[height, width];
        for (int y = 0; y < height; y++)
            for (int x = 0; x < width; x++)
            {
                grid[y, x] = InBounds(x, y) ? MapData[y, x] : -1;
                expandedMaterials[y, x] = InBounds(x, y) ? materials[y, x] : 0;
            }
        MapData = grid;
        materials = expandedMaterials;
        MapWidth = width;
        MapHeight = height;
        quarters = new Rectangle[height, width, 4];
        for (int y = 0; y < height; y++)
            for (int x = 0; x < width; x++) Refresh(x, y);
    }

    public void Paint(int x, int y, bool solid, int material = 0)
    {
        if (!InBounds(x, y)) return;
        if (material < 0 || material >= textures.Length) throw new ArgumentOutOfRangeException(nameof(material));
        if (Occupied(x, y) == solid && (!solid || MaterialAt(x, y) == material)) return;
        MapData[y, x] = solid ? 6 : -1;
        materials[y, x] = solid ? material : 0;
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

    // Recognize shapes supplied as complete tiles by the atlas. Preserve their
    // inward-facing detail instead of replacing it with quarters of the center.
    // Other shapes (thin strips, isolated cells, multiple notches) need assembly.
    public static int SelectFullTile(int topLeft, int topRight, int bottomLeft, int bottomRight) =>
        (topLeft, topRight, bottomLeft, bottomRight) switch
        {
            (0, 1, 5, 6) => 0,
            (1, 1, 6, 6) => 1,
            (1, 2, 6, 7) => 2,
            (5, 6, 5, 6) => 5,
            (6, 6, 6, 6) => 6,
            (6, 7, 6, 7) => 7,
            (5, 6, 10, 11) => 10,
            (6, 6, 11, 11) => 11,
            (6, 7, 11, 12) => 12,
            (9, 6, 6, 6) => 9,
            (6, 8, 6, 6) => 8,
            (6, 6, 4, 6) => 4,
            (6, 6, 6, 3) => 3,
            _ => -1
        };

    private void Refresh(int x, int y)
    {
        if (!Occupied(x, y)) return;
        Span<int> ids = stackalloc int[4];
        for (int q = 0; q < 4; q++)
        {
            int dx = q % 2 == 0 ? -1 : 1;
            int dy = q < 2 ? -1 : 1;
            int material = MaterialAt(x, y);
            int id = SelectTile(q, MaterialAt(x + dx, y) == material,
                MaterialAt(x, y + dy) == material, MaterialAt(x + dx, y + dy) == material);
            ids[q] = id;
            Rectangle source = TileSources[id];
            quarters[y, x, q] = new Rectangle(source.X + q % 2 * 4, source.Y + q / 2 * 4, 4, 4);
        }
        int fullTile = SelectFullTile(ids[0], ids[1], ids[2], ids[3]);
        if (fullTile >= 0)
        {
            Rectangle source = TileSources[fullTile];
            for (int q = 0; q < 4; q++)
                quarters[y, x, q] = new Rectangle(source.X + q % 2 * 4, source.Y + q / 2 * 4, 4, 4);
        }
    }

    public override void Draw() => Draw(1f);

    public void Draw(float opacity)
    {
        for (int y = 0; y < Height; y++)
            for (int x = 0; x < Width; x++)
            {
                if (!Occupied(x, y)) continue;
                for (int q = 0; q < 4; q++)
                    Globals.spriteBatch.Draw(textures[MaterialAt(x, y)], new Vector2(x * 8 + q % 2 * 4, y * 8 + q / 2 * 4),
                        quarters[y, x, q], Color.White * opacity);
            }
    }
}
