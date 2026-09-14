using System;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Content;
using Microsoft.Xna.Framework.Graphics;

namespace NodeTesting.models
{
    public class TextBox
    {
        protected SpriteFont font;
        protected string text = "";
        protected Vector2 textSize;
        protected Vector2 origin;
        public float rotation = 0f;
        public float scale = 1f;
        public TextBox(SpriteFont font) 
        {
            this.font = font;
            textSize = font.MeasureString(text);
        }

        public SpriteFont Font { get => font; set => font = value; }
        public string Text { get => text; set => text = value; }

        public void Draw(int x, int y, Color color)
        {
            textSize = font.MeasureString(text);
            origin = new Vector2(textSize.X / 2, textSize.Y / 2);
            Globals.spriteBatch.DrawString(font, text, new Vector2(x, y), color, rotation, origin, scale, SpriteEffects.None, 0f);
        }
    }
}
