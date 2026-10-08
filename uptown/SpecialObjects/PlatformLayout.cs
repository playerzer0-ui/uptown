using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;

namespace uptown.SpecialObjects;

// Stored as horizontal runs; editing operates on individual 8px cells.
public static class PlatformLayout
{
    public static HashSet<Point> Cells(IEnumerable<LevelObject> objects)
    {
        var cells = new HashSet<Point>();
        foreach (var item in objects)
        {
            if (item.Type != LevelObject.Platform) continue;
            var bounds = Platform.Bounds(new Point(item.X, item.Y), item.WidthTiles);
            int left = (int)Math.Floor(bounds.Left / 8f);
            int row = (int)Math.Floor(bounds.Top / 8f);
            for (int x = left; x < left + bounds.Width / 8; x++) cells.Add(new Point(x, row));
        }
        return cells;
    }

    public static void Rebuild(List<LevelObject> objects, HashSet<Point> cells, int width, int height, Func<int, int, bool> terrain)
    {
        cells.RemoveWhere(p => p.X < 0 || p.Y < 0 || p.X >= width || p.Y >= height || terrain(p.X, p.Y));
        var sorted = new List<Point>(cells);
        sorted.Sort((a, b) => a.Y == b.Y ? a.X.CompareTo(b.X) : a.Y.CompareTo(b.Y));
        objects.RemoveAll(item => item.Type == LevelObject.Platform);
        for (int i = 0; i < sorted.Count;)
        {
            Point start = sorted[i++];
            int end = start.X;
            while (i < sorted.Count && sorted[i].Y == start.Y && sorted[i].X == end + 1) end = sorted[i++].X;
            // A horizontal run needs terrain immediately beside at least one end.
            if (!terrain(start.X - 1, start.Y) && !terrain(end + 1, start.Y)) continue;
            int count = end - start.X + 1;
            objects.Add(new LevelObject { Type = LevelObject.Platform, X = start.X * 8 + count * 4,
                Y = (start.Y + 1) * 8, WidthTiles = count });
        }
    }
}
