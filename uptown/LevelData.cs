using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using Microsoft.Xna.Framework;
using NodeTesting.models;

namespace uptown;

// Keep the original cardinal values compatible with existing level files.
public enum BounceDirection { Up, Right, Down, Left, UpRight, DownRight, DownLeft, UpLeft }

// A special object placed in the editor. X/Y are its feet (bottom-center), like the spawn.
public sealed class LevelObject
{
    public const string Checkpoint = "checkpoint";
    public const string ExitFlag = "exit";
    public const string BounceBall = "bounceball";
    public const string Spring = "spring";

    public string Type { get; set; }
    public int X { get; set; }
    public int Y { get; set; }
    public BounceDirection Direction { get; set; } = BounceDirection.Up;
}

public sealed class LevelData
{
    public int Version { get; set; } = 1;
    public string Name { get; set; } = "Untitled";
    public string Tileset { get; set; } = "basic";
    public int TileSize { get; set; } = 8;
    public int Width { get; set; }
    public int Height { get; set; }
    public int SpawnX { get; set; }
    public int SpawnY { get; set; }
    public bool HasSpawn { get; set; }
    public int[][] Terrain { get; set; }
    public string[] TerrainTilesets { get; set; }
    public int[][] TerrainMaterials { get; set; }
    // Older level files have no objects; the empty default keeps them loading.
    public List<LevelObject> Objects { get; set; } = new();

    public static LevelData Capture(AutoTileMap map, Point? spawn, IEnumerable<LevelObject> objects = null)
    {
        var data = new LevelData { Version = 2, Width = map.Width, Height = map.Height,
            TerrainTilesets = map.Tilesets == null ? new[] { "basic" } : (string[])map.Tilesets.Clone() };
        if (objects != null)
            foreach (var item in objects) data.Objects.Add(new LevelObject
                { Type = item.Type, X = item.X, Y = item.Y, Direction = item.Direction });
        data.Terrain = new int[data.Height][];
        data.TerrainMaterials = new int[data.Height][];
        for (int y = 0; y < data.Height; y++)
        {
            data.Terrain[y] = new int[data.Width];
            data.TerrainMaterials[y] = new int[data.Width];
            for (int x = 0; x < data.Width; x++)
            {
                data.Terrain[y][x] = map.Occupied(x, y) ? 1 : 0;
                data.TerrainMaterials[y][x] = Math.Max(0, map.MaterialAt(x, y));
            }
        }
        if (!spawn.HasValue)
        {
            for (int y = data.Height - 1; y >= 2 && !spawn.HasValue; y--)
                for (int x = 0; x < data.Width; x++)
                    if (map.Occupied(x, y) && !map.Occupied(x, y - 1) && !map.Occupied(x, y - 2))
                    { spawn = new Point(x * 8 + 4, y * 8); break; }
        }
        data.HasSpawn = spawn.HasValue;
        data.SpawnX = spawn?.X ?? 12;
        data.SpawnY = spawn?.Y ?? 16;
        return data;
    }

    public int[,] CreateGrid(bool collision)
    {
        var grid = new int[Height, Width];
        for (int y = 0; y < Height; y++)
            for (int x = 0; x < Width; x++) grid[y, x] = Terrain[y][x] == 1 ? (collision ? 0 : 6) : -1;
        return grid;
    }

    public bool ValidSpawn()
    {
        if (!HasSpawn || SpawnX < 4 || SpawnX > Width * 8 - 4 || SpawnY < 12 || SpawnY > Height * 8) return false;
        for (int y = (SpawnY - 12) / 8; y <= (SpawnY - 1) / 8; y++)
            for (int x = (SpawnX - 4) / 8; x <= (SpawnX + 3) / 8; x++)
                if (Terrain[y][x] != 0) return false;
        return true;
    }

    public int[,] CreateMaterials(string[] targetTilesets)
    {
        var sourceTilesets = TerrainTilesets ?? new[] { Tileset ?? "basic" };
        var grid = new int[Height, Width];
        for (int y = 0; y < Height; y++)
            for (int x = 0; x < Width; x++)
            {
                int source = TerrainMaterials?[y][x] ?? 0;
                grid[y, x] = Math.Max(0, Array.IndexOf(targetTilesets, sourceTilesets[source]));
            }
        return grid;
    }

    public static string SaveFolder()
    {
        for (var dir = new DirectoryInfo(AppContext.BaseDirectory); dir != null; dir = dir.Parent)
            if (File.Exists(Path.Combine(dir.FullName, "uptown.csproj")))
                return Path.Combine(dir.FullName, "Levels");
        return Path.Combine(AppContext.BaseDirectory, "Levels");
    }

    public static LevelData Load(string path)
    {
        var data = JsonSerializer.Deserialize<LevelData>(File.ReadAllText(path));
        if (data == null || data.Width <= 0 || data.Height <= 0 || data.Terrain == null
            || data.Terrain.Length != data.Height || Array.Exists(data.Terrain, row => row == null || row.Length != data.Width))
            throw new InvalidDataException("Level file is missing or has mismatched terrain.");
        data.Objects ??= new();
        var names = data.TerrainTilesets ?? new[] { data.Tileset ?? "basic" };
        if (names.Length == 0 || Array.Exists(names, string.IsNullOrWhiteSpace)
            || (data.TerrainMaterials != null && (data.TerrainMaterials.Length != data.Height
                || Array.Exists(data.TerrainMaterials, row => row == null || row.Length != data.Width
                    || Array.Exists(row, id => id < 0 || id >= names.Length)))))
            throw new InvalidDataException("Level file has invalid terrain materials.");
        data.Objects.RemoveAll(item => item == null);
        return data;
    }

    // Saved levels, newest first.
    public static string[] ListSaves()
    {
        string folder = SaveFolder();
        if (!Directory.Exists(folder)) return Array.Empty<string>();
        string[] files = Directory.GetFiles(folder, "*.uptown");
        Array.Sort(files, (a, b) => File.GetLastWriteTimeUtc(b).CompareTo(File.GetLastWriteTimeUtc(a)));
        return files;
    }

    public void Save(string path)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(path));
        string temp = path + ".tmp";
        File.WriteAllText(temp, JsonSerializer.Serialize(this, new JsonSerializerOptions { WriteIndented = true }));
        File.Move(temp, path, true);
    }
}
