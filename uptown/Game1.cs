using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using NodeTesting.models;
using System;
using System.Collections.Generic;
using System.IO;
using uptown.Modes;

namespace uptown
{
    public class Game1 : Game
    {
        private GraphicsDeviceManager _graphics;
        private SpriteBatch _spriteBatch;
        private readonly Dictionary<ModeId, GameMode> _modes = new();
        private GameMode _activeMode;
        public ModeId CurrentMode { get; private set; }
        private const int InitialWindowWidth = 1280;
        private const int InitialWindowHeight = 720;

        public Game1()
        {
            _graphics = new GraphicsDeviceManager(this);
            Content.RootDirectory = "Content";
            IsMouseVisible = true;
        }

        protected override void Initialize()
        {
            _graphics.PreferredBackBufferWidth = InitialWindowWidth;
            _graphics.PreferredBackBufferHeight = InitialWindowHeight;
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

            _modes.Add(ModeId.Editor, new EditorMode(this, SwitchMode, message => Window.Title = message, StartSaveValidation));
            _modes.Add(ModeId.Home, new HomeMode(
                play: () => SwitchMode(ModeId.Play),
                create: () => SwitchMode(ModeId.Editor),
                listLevels: LevelData.ListSaves,
                load: LoadLevel,
                exit: Exit));
            SwitchMode(ModeId.Home);
        }

        protected override void Update(GameTime gameTime)
        {
            Globals.Input.Update();
            if (CurrentMode == ModeId.Editor && ((EditorMode)_modes[ModeId.Editor]).IsEditingText)
            {
                _activeMode.Update(gameTime);
                base.Update(gameTime);
                return;
            }
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
            GraphicsDevice.SetRenderTarget(null);
            _activeMode.Draw();

            base.Draw(gameTime);
        }

        protected override void UnloadContent()
        {
            uptown.Decorations.Lightstick.DisposeGlow();
            if (_modes.TryGetValue(ModeId.Editor, out var editor)) ((EditorMode)editor).Dispose();
            base.UnloadContent();
        }

        private void LoadLevel(string path)
        {
            try
            {
                ((EditorMode)_modes[ModeId.Editor]).LoadLevel(path);
                SwitchMode(ModeId.Editor);
                Window.Title = "Loaded: " + path;
            }
            catch (Exception error) when (error is IOException || error is UnauthorizedAccessException
                || error is System.Text.Json.JsonException)
            {
                Window.Title = "Load failed: " + error.Message;
            }
        }

        public void SwitchMode(ModeId mode)
        {
            if (mode == ModeId.Play && CurrentMode != ModeId.Play)
            {
                var data = ((EditorMode)_modes[ModeId.Editor]).Capture();
                if (!data.ValidSpawn())
                {
                    Window.Title = "Paint a platform first, or set a clear spawn with P + click.";
                    return;
                }
                _modes[ModeId.Play] = new PlayMode(data, () => SwitchMode(ModeId.Editor));
            }
            ActivateMode(mode);
        }

        private void StartSaveValidation(LevelData data, Func<bool> saveClearedLevel)
        {
            _modes[ModeId.Play] = new PlayMode(data, () => SwitchMode(ModeId.Editor), saveClearedLevel);
            ActivateMode(ModeId.Play);
            Window.Title = "Clear this level to save it — reach the exit flag | Stop or F2: cancel";
        }

        private void ActivateMode(ModeId mode)
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
