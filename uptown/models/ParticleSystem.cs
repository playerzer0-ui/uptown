using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;

namespace NodeTesting.models;

// Small visual particles with no collision or gameplay state.
public sealed class ParticleSystem
{
    private struct Particle
    {
        public Vector2 Position, Velocity;
        public float Age, Lifetime;
        public Color Color;
        public int Size;
    }

    private readonly List<Particle> particles = new();
    private readonly Random random = new();
    public int Count => particles.Count;
    public float Gravity { get; set; } = 100f;

    public void Burst(Vector2 center, int count, Color color)
    {
        for (int i = 0; i < count; i++)
        {
            float angle = MathHelper.TwoPi * (i + (float)random.NextDouble() * 0.6f) / count;
            float speed = 40f + (float)random.NextDouble() * 65f;
            particles.Add(new Particle { Position = center,
                Velocity = new Vector2(MathF.Cos(angle), MathF.Sin(angle)) * speed,
                Lifetime = 0.3f + (float)random.NextDouble() * 0.15f,
                Color = color, Size = random.Next(1, 3) });
        }
    }

    public void Update(GameTime gameTime)
    {
        float dt = Math.Max(0, (float)gameTime.ElapsedGameTime.TotalSeconds);
        for (int i = particles.Count - 1; i >= 0; i--)
        {
            Particle particle = particles[i];
            particle.Age += dt;
            if (particle.Age >= particle.Lifetime) { particles.RemoveAt(i); continue; }
            particle.Position += particle.Velocity * dt + new Vector2(0, Gravity * dt * dt / 2);
            particle.Velocity.Y += Gravity * dt;
            particles[i] = particle;
        }
    }

    public void Draw()
    {
        foreach (var particle in particles)
        {
            float opacity = 1f - particle.Age / particle.Lifetime;
            Globals.spriteBatch.Draw(Globals.Pixel,
                new Rectangle((int)MathF.Round(particle.Position.X), (int)MathF.Round(particle.Position.Y),
                    particle.Size, particle.Size), particle.Color * opacity);
        }
    }

    public void Clear() => particles.Clear();
}
