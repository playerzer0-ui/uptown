using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using NodeTesting.models;
using System;
using System.Collections.Generic;
using uptown.Modes;

namespace uptown
{
    public class Game1 : Game
    {
        private GraphicsDeviceManager _graphics;
        private SpriteBatch _spriteBatch;
        private Canvas _canvas;
        private readonly Dictionary<ModeId, GameMode> _modes = new();
        private GameMode _activeMode;
        public ModeId CurrentMode { get; private set; }
        private const int CanvasWidth = 320;
        private const int CanvasHeight = 180;

        public Game1()
        {
            _graphics = new GraphicsDeviceManager(this);
            Content.RootDirectory = "Content";
            IsMouseVisible = true;
        }

        protected override void Initialize()
        {
            _graphics.PreferredBackBufferWidth = CanvasWidth * 4;
            _graphics.PreferredBackBufferHeight = CanvasHeight * 4;
            Window.AllowUserResizing = true;
            _graphics.ApplyChanges();

            base.Initialize();
        }

        protected override void LoadContent()
        {
            _spriteBatch = new SpriteBatch(GraphicsDevice);

            Globals.Content = Content;
            Globals.spriteBatch = _spriteBatch;
            Globals.graphics = _graphics;

            _canvas = new Canvas(GraphicsDevice, Window, CanvasWidth, CanvasHeight);
            _modes.Add(ModeId.Play, new PlayMode(CanvasWidth, CanvasHeight));
            _modes.Add(ModeId.Editor, new EditorMode(CanvasWidth, CanvasHeight, _canvas, SwitchMode));
            _modes.Add(ModeId.Home, new HomeMode());
            SwitchMode(ModeId.Play);
        }

        protected override void Update(GameTime gameTime)
        {
            Globals.Input.Update();
            if (Keyboard.GetState().IsKeyDown(Keys.Escape))
                Exit();

            // Skip simulation on a switch frame so navigation input cannot
            // also trigger an action in the newly entered mode.
            if (Globals.Input.KeyJustDown(Keys.F1)) SwitchMode(ModeId.Play);
            else if (Globals.Input.KeyJustDown(Keys.F2)) SwitchMode(ModeId.Editor);
            else if (Globals.Input.KeyJustDown(Keys.F3)) SwitchMode(ModeId.Home);
            else _activeMode.Update(gameTime);

            base.Update(gameTime);
        }

        protected override void Draw(GameTime gameTime)
        {
            _canvas.Activate();
            _activeMode.Draw();

            _canvas.Draw(_spriteBatch);

            base.Draw(gameTime);
        }

        public void SwitchMode(ModeId mode)
        {
            if (!_modes.TryGetValue(mode, out var next))
                throw new ArgumentOutOfRangeException(nameof(mode));
            if (ReferenceEquals(next, _activeMode)) return;
            _activeMode?.Leave();
            CurrentMode = mode;
            _activeMode = next;
            _activeMode.Enter();
            Window.Title = $"Uptown — {mode} | F1 Play · F2 Editor · F3 Home · Esc Exit";
        }
    }
}
