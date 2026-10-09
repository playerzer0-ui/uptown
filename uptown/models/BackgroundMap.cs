using System;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace NodeTesting.models;

// Decoration only: no neighbor selection, collision, or terrain support.
public sealed class BackgroundMap
{
    public static readonly string[] Names = { "texturewall", "texturewallgrey", "whitewall", "blackwall", "textureblackwall" };
    private readonly Texture2D[] textures;
    private int[,] cells;
    public int Width => cells.GetLength(1);
    public int Height => cells.GetLength(0);

    public BackgroundMap(int width, int height, int[,] grid = null)
    {
        cells = grid == null ? EmptyGrid(width, height) : (int[,])grid.Clone();
        textures = new Texture2D[Names.Length];
        for (int i = 0; i < Names.Length; i++)
            textures[i] = Globals.Content.Load<Texture2D>("graphics/backgroundtiles/" + Names[i]);
    }

    public static int[,] EmptyGrid(int width, int height)
    {
        var grid = new int[height, width];
        for (int y = 0; y < height; y++)
            for (int x = 0; x < width; x++) grid[y, x] = -1;
        return grid;
    }

    public void Paint(int x, int y, int tile)
    {
        if (x < 0 || y < 0 || x >= Width || y >= Height) return;
        if (tile < -1 || tile >= Names.Length) throw new ArgumentOutOfRangeException(nameof(tile));
        cells[y, x] = tile;
    }

    public bool Occupied(int x, int y) =>
        x >= 0 && y >= 0 && x < Width && y < Height && cells[y, x] >= 0;

    public int[][] Capture()
    {
        var grid = new int[Height][];
        for (int y = 0; y < Height; y++)
        {
            grid[y] = new int[Width];
            for (int x = 0; x < Width; x++) grid[y][x] = cells[y, x];
        }
        return grid;
    }

    public void Expand(int width, int height)
    {
        if (width < Width || height < Height) throw new ArgumentOutOfRangeException(nameof(width));
        var grid = EmptyGrid(width, height);
        for (int y = 0; y < Height; y++)
            for (int x = 0; x < Width; x++) grid[y, x] = cells[y, x];
        cells = grid;
    }

    public void Draw()
    {
        for (int y = 0; y < Height; y++)
            for (int x = 0; x < Width; x++)
                if (cells[y, x] >= 0)
                    Globals.spriteBatch.Draw(textures[cells[y, x]], new Vector2(x * 8, y * 8), Color.White);
    }
}
