using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Content;
using Microsoft.Xna.Framework.Graphics;

namespace NodeTesting.models
{
    public class Globals
    {
        public static ContentManager Content;
        public static SpriteBatch spriteBatch;
        public static GraphicsDeviceManager graphics;
        public static InputManager Input = new InputManager();

        private static Texture2D pixel;

        /// <summary>A shared 1x1 white texture for drawing rectangles and lines.</summary>
        public static Texture2D Pixel
        {
            get
            {
                if (pixel == null)
                {
                    pixel = new Texture2D(graphics.GraphicsDevice, 1, 1);
                    pixel.SetData(new[] { Color.White });
                }
                return pixel;
            }
        }
    }
}
