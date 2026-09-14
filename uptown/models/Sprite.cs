using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Content;
using Microsoft.Xna.Framework.Graphics;

namespace NodeTesting.models
{
    /// <summary>
    /// Represents a sprite
    /// </summary>
    public class Sprite
    {
        protected Texture2D texture;
        protected Vector2 pos;
        protected Vector2 origin;
        protected float rotation = 0f;
        protected float scale = 1f;

        /// <summary>
        /// Gets the top-left position of the sprite in world space.
        /// </summary>
        public Vector2 Pos { get => pos; set => pos = value; }

        public Vector2 Origin { get => origin; set => origin = value; }

        /// <summary>
        /// Creates a new sprite instance with the specified texture and position.
        /// </summary>
        /// <param name="texture">
        /// The path to the texture within the Content folder. This texture will be loaded using
        /// <see cref="Globals.Content.Load{Texture2D}(string)"/>.
        /// </param>
        /// <param name="pos">
        /// The position of the sprite in the game world, measured in pixels.
        /// </param>
        public Sprite(string texture, Vector2 pos) 
        {
            this.texture = Globals.Content.Load<Texture2D>(texture);
            this.pos = pos;
            origin = new Vector2(this.texture.Width / 2, this.texture.Height / 2);
        }

        /// <summary>
        /// Draws the sprite to the screen using the provided color tint.
        /// </summary>
        /// <param name="color">
        /// The color to tint the sprite with. Use <see cref="Color.White"/> for no tint.
        /// </param>
        public void Draw(Color color)
        {
            Globals.spriteBatch.Draw(texture, pos, null, color, rotation, origin, scale, SpriteEffects.None, 0f);
        }
    }
}
