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
    public const string Platform = "platform";
    public const string MovingPlatform = "movingplatform";
    public const string Spike = "spike";
    public const string Door = "door";
    public const string Lightstick = "lightstick";

    public string Type { get; set; }
    public int X { get; set; }
    public int Y { get; set; }
    public int WidthTiles { get; set; } = 6;
    public List<LevelWaypoint> Waypoints { get; set; } = new();
    public float MoveSpeed { get; set; } = 40f;
    public BounceDirection Direction { get; set; } = BounceDirection.Up;
}

public sealed class LevelWaypoint
{
    public int X { get; set; }
    public int Y { get; set; }
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
    public int[][] Background { get; set; }
    public string[] BackgroundTilesets { get; set; }
    // Older level files have no objects; the empty default keeps them loading.
    public List<LevelObject> Objects { get; set; } = new();

    public static LevelData Capture(AutoTileMap map, Point? spawn, IEnumerable<LevelObject> objects = null, BackgroundMap background = null)
    {
        var data = new LevelData { Version = 2, Width = map.Width, Height = map.Height,
            TerrainTilesets = map.Tilesets == null ? new[] { "basic" } : (string[])map.Tilesets.Clone() };
        if (objects != null)
            foreach (var item in objects) data.Objects.Add(new LevelObject
                { Type = item.Type, X = item.X, Y = item.Y, Direction = item.Direction, WidthTiles = item.WidthTiles,
                    MoveSpeed = item.MoveSpeed, Waypoints = item.Waypoints?.ConvertAll(p => new LevelWaypoint { X = p.X, Y = p.Y }) ?? new() });
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
        data.Background = background?.Capture();
        data.BackgroundTilesets = background == null ? null : (string[])BackgroundMap.Names.Clone();
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

    public int[,] CreateBackgroundGrid()
    {
        var grid = BackgroundMap.EmptyGrid(Width, Height);
        if (Background == null) return grid;
        var names = BackgroundTilesets ?? BackgroundMap.Names;
        for (int y = 0; y < Height; y++)
            for (int x = 0; x < Width; x++)
                if (Background[y][x] >= 0) grid[y, x] = Array.IndexOf(BackgroundMap.Names, names[Background[y][x]]);
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
        var backgroundNames = data.BackgroundTilesets ?? BackgroundMap.Names;
        if (data.Background != null && (backgroundNames.Length == 0 || Array.Exists(backgroundNames, string.IsNullOrWhiteSpace)
            || data.Background.Length != data.Height || Array.Exists(data.Background, row => row == null || row.Length != data.Width
                || Array.Exists(row, id => id < -1 || id >= backgroundNames.Length))))
            throw new InvalidDataException("Level file has invalid background tiles.");
        var names = data.TerrainTilesets ?? new[] { data.Tileset ?? "basic" };
        if (names.Length == 0 || Array.Exists(names, string.IsNullOrWhiteSpace)
            || (data.TerrainMaterials != null && (data.TerrainMaterials.Length != data.Height
                || Array.Exists(data.TerrainMaterials, row => row == null || row.Length != data.Width
                    || Array.Exists(row, id => id < 0 || id >= names.Length)))))
            throw new InvalidDataException("Level file has invalid terrain materials.");
        data.Objects.RemoveAll(item => item == null);
        foreach (var item in data.Objects)
        {
            item.Waypoints ??= new();
            if (item.Type != LevelObject.MovingPlatform) continue;
            if (!float.IsFinite(item.MoveSpeed) || item.MoveSpeed <= 0 || item.MoveSpeed > 240 || item.Waypoints.Count == 0)
                throw new InvalidDataException("Moving platform needs a valid speed and a route.");
            if (item.WidthTiles < 3 || item.WidthTiles > data.Width)
                throw new InvalidDataException("Moving platform has invalid length.");
            int halfWidth = item.WidthTiles * 4;
            int lastX = item.X, lastY = item.Y;
            if (lastX < halfWidth || lastX > data.Width * 8 - halfWidth || lastY < 8 || lastY > data.Height * 8)
                throw new InvalidDataException("Moving platform starts outside the level.");
            foreach (var point in item.Waypoints)
            {
                if (point == null || point.X < halfWidth || point.X > data.Width * 8 - halfWidth || point.Y < 8 || point.Y > data.Height * 8)
                    throw new InvalidDataException("Moving platform route leaves the level.");
                long dx = (long)point.X - lastX, dy = (long)point.Y - lastY;
                if ((dx == 0 && dy == 0) || (dx != 0 && dy != 0 && Math.Abs(dx) != Math.Abs(dy)))
                    throw new InvalidDataException("Moving platform route must use eight-direction segments.");
                lastX = point.X;
                lastY = point.Y;
            }
        }
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

    public static string NamedSavePath(string name)
    {
        name = name.Trim();
        if (name.Length == 0 || name.Length > 48 || name.EndsWith('.') || name.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0)
            throw new ArgumentException("Use a level name up to 48 characters without filename symbols or a trailing dot.");
        string stem = name.Split('.')[0].ToUpperInvariant();
        if (stem is "CON" or "PRN" or "AUX" or "NUL" || (stem.Length == 4
            && (stem.StartsWith("COM") || stem.StartsWith("LPT")) && stem[3] >= '1' && stem[3] <= '9'))
            throw new ArgumentException("That level name is reserved by Windows. Choose another name.");
        return Path.Combine(SaveFolder(), name + ".uptown");
    }

    public void SaveAs(string path, string previousPath)
    {
        bool samePath = string.Equals(path, previousPath, StringComparison.OrdinalIgnoreCase);
        Save(path, samePath);
        if (!samePath && previousPath != null && File.Exists(previousPath)) File.Delete(previousPath);
    }

    public void Save(string path, bool overwrite = true)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(path));
        string temp = path + ".tmp";
        File.WriteAllText(temp, JsonSerializer.Serialize(this, new JsonSerializerOptions { WriteIndented = true }));
        try { File.Move(temp, path, overwrite); }
        finally { if (File.Exists(temp)) File.Delete(temp); }
    }
}
