using System;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace NodeTesting.models;

// A procedural expanding bubble and burst; no animation sheet required.
public sealed class DeathEffect
{
    private const int TextureSize = 64;
    private static Texture2D bubble;
    private readonly ParticleSystem particles = new();
    private Vector2 center;
    private float elapsed;
    public bool Active { get; private set; }
    public float Duration { get; }

    public DeathEffect(float duration) => Duration = duration;

    public void Start(Vector2 position)
    {
        center = position;
        elapsed = 0;
        Active = true;
        particles.Clear();
        particles.Burst(center, 18, new Color(210, 245, 255));
    }

    public void Update(GameTime gameTime)
    {
        if (!Active) return;
        elapsed += Math.Max(0, (float)gameTime.ElapsedGameTime.TotalSeconds);
        particles.Update(gameTime);
        if (elapsed >= Duration - 0.000001f) { Active = false; particles.Clear(); }
    }

    public void Draw()
    {
        if (!Active) return;
        if (bubble == null)
        {
            bubble = new Texture2D(Globals.graphics.GraphicsDevice, TextureSize, TextureSize);
            var pixels = new Color[TextureSize * TextureSize];
            for (int y = 0; y < TextureSize; y++)
                for (int x = 0; x < TextureSize; x++)
                {
                    float radius = Vector2.Distance(new Vector2(x + 0.5f, y + 0.5f), new Vector2(TextureSize / 2));
                    float ringDistance = (radius - 27f) / 1.5f;
                    float ring = MathF.Exp(-ringDistance * ringDistance);
                    float glow = Math.Max(0, 1f - radius / 32f);
                    pixels[y * TextureSize + x] = new Color(190, 235, 255) * Math.Min(1f, ring + glow * glow * 0.3f);
                }
            bubble.SetData(pixels);
        }
        float progress = Math.Clamp(elapsed / Duration, 0, 1);
        float scale = 0.15f + 0.85f * (1f - (1f - progress) * (1f - progress));
        Globals.spriteBatch.Draw(bubble, center, null, Color.White * (1f - progress),
            0f, new Vector2(TextureSize / 2), scale, SpriteEffects.None, 0f);
        particles.Draw();
    }

    public static void DisposeTexture() { bubble?.Dispose(); bubble = null; }
}
