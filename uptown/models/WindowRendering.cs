using System;
using Microsoft.Xna.Framework;

namespace NodeTesting.models;

// Every screen rasterizes sprites directly at window resolution.
public static class WindowRendering
{
    public static int ScaleFor(int width, int height) =>
        Math.Max(1, Math.Min(width / 320, height / 180));

    public static Matrix PixelAligned(Matrix transform)
    {
        transform.M41 = MathF.Round(transform.M41);
        transform.M42 = MathF.Round(transform.M42);
        return transform;
    }
}
