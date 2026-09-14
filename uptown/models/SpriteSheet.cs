using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace NodeTesting.models
{
    public class SpriteSheet
    {
        protected Texture2D Texture;
        protected Vector2 Position = Vector2.Zero;
        protected Color Color = Color.White;
        protected Vector2 Origin;
        protected float Rotation = 0f;
        protected float Scale = 1f;
        protected SpriteEffects SpriteEffect;
        protected Rectangle[] Rectangles;
        protected int frames;
        protected int width;
        protected int frameIndex = 0;

        /// <summary>Width of a single frame in pixels.</summary>
        public int FrameWidth => width;

        /// <summary>Height of the sheet (same for every frame).</summary>
        public int FrameHeight => Texture.Height;

        public SpriteSheet(string Texture, int frames)
        {
            this.frames = frames;
            this.Texture = Globals.Content.Load<Texture2D>(Texture);
            width = this.Texture.Width / frames;
            Rectangles = new Rectangle[frames];

            for (int i = 0; i < frames; i++)
                Rectangles[i] = new Rectangle(i * width, 0, width, this.Texture.Height);

            Origin = new Vector2(width / 2, this.Texture.Height / 2);
        }

        public void Draw()
        {
            Globals.spriteBatch.Draw(Texture, Position, Rectangles[frameIndex], Color,
                Rotation, Origin, Scale, SpriteEffect, 0f);
        }

        public void DrawFrame(int index, Vector2 position)
        {
            Globals.spriteBatch.Draw(Texture, position, Rectangles[index], Color.White,
                0f, Origin, 1f, SpriteEffects.None, 0f);
        }

        /// <summary>
        /// Draw a specific frame with custom scale.
        /// </summary>
        /// <param name="index">Frame index to draw (0-based).</param>
        /// <param name="position">Position to draw at (center of the frame).</param>
        /// <param name="scale">Scale factor (1.0 = original size).</param>
        public void DrawFrame(int index, Vector2 position, float scale)
        {
            Globals.spriteBatch.Draw(Texture, position, Rectangles[index], Color.White,
                0f, Origin, scale, SpriteEffects.None, 0f);
        }

        /// <summary>
        /// Draw a specific frame with custom scale and color.
        /// </summary>
        /// <param name="index">Frame index to draw (0-based).</param>
        /// <param name="position">Position to draw at (center of the frame).</param>
        /// <param name="scale">Scale factor (1.0 = original size).</param>
        /// <param name="color">Color tint to apply.</param>
        public void DrawFrame(int index, Vector2 position, float scale, Color color)
        {
            Globals.spriteBatch.Draw(Texture, position, Rectangles[index], color,
                0f, Origin, scale, SpriteEffects.None, 0f);
        }

        /// <summary>
        /// Draw a specific frame with full control over parameters.
        /// </summary>
        /// <param name="index">Frame index to draw (0-based).</param>
        /// <param name="position">Position to draw at.</param>
        /// <param name="rotation">Rotation in radians.</param>
        /// <param name="scale">Scale factor.</param>
        /// <param name="effects">Sprite effects (flip horizontally/vertically).</param>
        public void DrawFrame(int index, Vector2 position, float rotation, float scale, SpriteEffects effects = SpriteEffects.None)
        {
            Globals.spriteBatch.Draw(Texture, position, Rectangles[index], Color.White,
                rotation, Origin, scale, effects, 0f);
        }
    }
}