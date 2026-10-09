using System;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using NodeTesting.models;

namespace NodeTesting.models
{
    /// <summary>A single-line input. Create in LoadContent and draw inside SpriteBatch.Begin/End.</summary>
    public sealed class TextInput : IDisposable
    {
        private readonly Game game;
        private readonly SpriteFont font;
        private readonly Texture2D pixel;
        private MouseState previousMouse;
        private KeyboardState previousKeys;
        private string text = "";
        private int caret;
        private int visibleStart;
        private float blinkTime;
        private Keys? repeatingKey;
        private float repeatTime;
        private bool disposed;
        private const int Padding = 10;

        public CollisionRect Collider { get; }
        public Rectangle Bounds => Collider.Rect;
        /// <summary>
        /// Optional conversion from screen coordinates to the coordinates used to draw this input.
        /// Leave null when drawing directly to the screen without a transform.
        /// </summary>
        public Func<Vector2, Vector2> ScreenToLocal { get; set; }
        public string Placeholder { get; set; } = "Type here...";
        public int MaxLength { get; set; } = 256;
        public bool IsFocused { get; private set; }
        public event Action<string> Submitted;
        public event Action<string> TextChanged;

        public string Text
        {
            get => text;
            set
            {
                string cleaned = "";
                foreach (char character in value ?? "")
                    if (!char.IsControl(character) && font.Characters.Contains(character))
                        cleaned += character;
                if (cleaned.Length > Math.Max(0, MaxLength))
                    cleaned = cleaned.Substring(0, Math.Max(0, MaxLength));
                if (text == cleaned) return;
                text = cleaned;
                caret = text.Length;
                visibleStart = 0;
                blinkTime = 0;
                TextChanged?.Invoke(text);
            }
        }

        public TextInput(Game game, SpriteFont font, CollisionRect collider,
            string placeholder = "Type here...")
        {
            this.game = game;
            this.font = font;
            Collider = collider ?? throw new ArgumentNullException(nameof(collider));
            Placeholder = placeholder;
            pixel = new Texture2D(game.GraphicsDevice, 1, 1);
            pixel.SetData(new[] { Color.White });
            game.Window.TextInput += OnTextInput;
            game.Deactivated += OnDeactivated;
        }

        private void OnDeactivated(object sender, EventArgs args) => Blur();

        public void Focus()
        {
            IsFocused = true;
            blinkTime = 0;
            repeatingKey = null;
        }

        public void Blur()
        {
            IsFocused = false;
            repeatingKey = null;
        }

        public void SyncInput()
        {
            previousMouse = Mouse.GetState();
            previousKeys = Keyboard.GetState();
        }

        private void OnTextInput(object sender, TextInputEventArgs args)
        {
            // MonoGame's text event handles keyboard layouts, Shift, and native character repeat.
            if (disposed || !IsFocused || !game.IsActive || char.IsControl(args.Character)
                || !font.Characters.Contains(args.Character) || text.Length >= MaxLength)
                return;

            text = text.Insert(caret, args.Character.ToString());
            caret++;
            blinkTime = 0;
            TextChanged?.Invoke(text);
        }

        public void Update(GameTime gameTime)
        {
            MouseState mouse = Mouse.GetState();
            KeyboardState keys = Keyboard.GetState();
            float elapsed = (float)gameTime.ElapsedGameTime.TotalSeconds;
            if (!game.IsActive) Blur();

            if (game.IsActive && mouse.LeftButton == ButtonState.Pressed
                && previousMouse.LeftButton == ButtonState.Released)
            {
                Vector2 screenPosition = new Vector2(mouse.X, mouse.Y);
                Vector2 position = ScreenToLocal?.Invoke(screenPosition) ?? screenPosition;
                if (Collider.Contains(new Point((int)position.X, (int)position.Y)))
                {
                    Focus();
                    EnsureCaretVisible();
                    float localX = position.X - Bounds.X - Padding;
                    caret = visibleStart;
                    while (caret < text.Length)
                    {
                        float left = Measure(visibleStart, caret);
                        float right = Measure(visibleStart, caret + 1);
                        if (localX < (left + right) / 2f) break;
                        caret++;
                    }
                }
                else Blur();
            }

            if (IsFocused)
            {
                blinkTime = (blinkTime + elapsed) % 1f;
                if (JustPressed(keys, Keys.Enter)) Submitted?.Invoke(text);
                if (JustPressed(keys, Keys.Tab) || JustPressed(keys, Keys.Escape)) Blur();

                if (IsFocused)
                {
                    Keys? held = null;
                    foreach (Keys key in new[] { Keys.Back, Keys.Delete, Keys.Left, Keys.Right, Keys.Home, Keys.End })
                    {
                        if (JustPressed(keys, key))
                        {
                            Edit(key);
                            repeatingKey = key;
                            repeatTime = 0.4f;
                            held = key;
                            break;
                        }
                        if (repeatingKey == key && keys.IsKeyDown(key)) held = key;
                    }

                    if (!held.HasValue) repeatingKey = null;
                    else if (!JustPressed(keys, held.Value))
                    {
                        repeatTime -= elapsed;
                        while (repeatTime <= 0)
                        {
                            Edit(held.Value);
                            repeatTime += 0.05f;
                        }
                    }
                    EnsureCaretVisible();
                }
            }

            previousMouse = mouse;
            previousKeys = keys;
        }

        private bool JustPressed(KeyboardState keys, Keys key) =>
            keys.IsKeyDown(key) && previousKeys.IsKeyUp(key);

        private void Edit(Keys key)
        {
            string oldText = text;
            switch (key)
            {
                case Keys.Back when caret > 0:
                    text = text.Remove(--caret, 1);
                    break;
                case Keys.Delete when caret < text.Length:
                    text = text.Remove(caret, 1);
                    break;
                case Keys.Left: caret = Math.Max(0, caret - 1); break;
                case Keys.Right: caret = Math.Min(text.Length, caret + 1); break;
                case Keys.Home: caret = 0; break;
                case Keys.End: caret = text.Length; break;
            }
            blinkTime = 0;
            if (oldText != text) TextChanged?.Invoke(text);
        }

        private float Measure(int start, int end) =>
            font.MeasureString(text.Substring(start, end - start)).X;

        private void EnsureCaretVisible()
        {
            visibleStart = Math.Min(visibleStart, caret);
            float width = Math.Max(0, Bounds.Width - Padding * 2 - 2);
            while (visibleStart < caret && Measure(visibleStart, caret) > width)
                visibleStart++;
            while (visibleStart > 0 && Measure(visibleStart - 1, caret) <= width)
                visibleStart--;
        }

        public void Draw()
        {
            EnsureCaretVisible();
            SpriteBatch batch = Globals.spriteBatch;
            Color border = IsFocused ? PicoPallete.yellow : PicoPallete.dark_blue;
            Collider.Draw(border);
            batch.Draw(pixel, new Rectangle(Bounds.X + 2, Bounds.Y + 2,
                Math.Max(0, Bounds.Width - 4), Math.Max(0, Bounds.Height - 4)), PicoPallete.white);

            float availableWidth = Math.Max(0, Bounds.Width - Padding * 2 - 2);
            string displayed = text.Length == 0 ? Placeholder ?? "" : text.Substring(visibleStart);
            while (displayed.Length > 0 && font.MeasureString(displayed).X > availableWidth)
                displayed = displayed.Substring(0, displayed.Length - 1);

            float y = Bounds.Y + (Bounds.Height - font.LineSpacing) / 2f;
            batch.DrawString(font, displayed, new Vector2(Bounds.X + Padding, y),
                text.Length == 0 ? PicoPallete.lavender : PicoPallete.black);

            if (IsFocused && blinkTime < 0.5f)
            {
                int x = Bounds.X + Padding + (int)MathF.Round(Measure(visibleStart, caret));
                batch.Draw(pixel, new Rectangle(x, (int)y, 2, font.LineSpacing), PicoPallete.black);
            }
        }

        public void Dispose()
        {
            if (disposed) return;
            disposed = true;
            game.Window.TextInput -= OnTextInput;
            game.Deactivated -= OnDeactivated;
            pixel.Dispose();
        }
    }
}
