using System;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using NodeTesting.models;

namespace uptown.Decorations;

// Decoration only: no collider or player interaction.
public sealed class Lightstick : Entity
{
    public const int Size = 8;
    public const int GlowRadius = 24;
    private readonly Texture2D texture;
    private static Texture2D glow;

    public Lightstick(Vector2 feet) : base(feet) =>
        texture = Globals.Content.Load<Texture2D>("graphics/decoration/lightstick");

    public static Rectangle Bounds(Point feet) => new(feet.X - Size / 2, feet.Y - Size, Size, Size);

    public static bool Supported(Point feet, Func<int, int, bool> background)
    {
        Rectangle bounds = Bounds(feet);
        if (bounds.Left < 0 || bounds.Top < 0) return false;
        for (int y = bounds.Top / Size; y <= (bounds.Bottom - 1) / Size; y++)
            for (int x = bounds.Left / Size; x <= (bounds.Right - 1) / Size; x++)
                if (!background(x, y)) return false;
        return true;
    }

    public override void Draw() => Globals.spriteBatch.Draw(texture, Bounds(Position.ToPoint()), Color.White);

    public void DrawGlow() => DrawGlow(Position);

    public static void DrawGlow(Vector2 feet, float opacity = 1f)
    {
        if (glow == null)
        {
            int diameter = GlowRadius * 2;
            glow = new Texture2D(Globals.graphics.GraphicsDevice, diameter, diameter);
            var pixels = new Color[diameter * diameter];
            for (int y = 0; y < diameter; y++)
                for (int x = 0; x < diameter; x++)
                {
                    float distance = Vector2.Distance(new Vector2(x + 0.5f, y + 0.5f), new Vector2(GlowRadius));
                    float falloff = Math.Max(0f, 1f - distance / GlowRadius);
                    // Premultiplied color for SpriteBatch's default AlphaBlend.
                    pixels[y * diameter + x] = new Color(255, 235, 175) * (falloff * falloff * 0.45f);
                }
            glow.SetData(pixels);
        }
        Globals.spriteBatch.Draw(glow, feet - new Vector2(0, Size / 2), null, Color.White * opacity,
            0f, new Vector2(GlowRadius), 1f, SpriteEffects.None, 0f);
    }

    public static void DisposeGlow()
    {
        glow?.Dispose();
        glow = null;
    }
}
