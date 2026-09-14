using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using NodeTesting.models;
using System;
using System.IO;

namespace uptown
{
    public class Game1 : Game
    {
        private GraphicsDeviceManager _graphics;
        private SpriteBatch _spriteBatch;
        private Canvas _canvas;
        private TileMap _tileMap;
        private Player _player;
        private Camera _camera;
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
            _tileMap = new TileMap("graphics/tileset/basic", 8, 8,
                Path.Combine(AppContext.BaseDirectory, "Content", "maps", "test-map_platforms.csv"));
            var collisions = new CollisionMap("graphics/tileset/collision", 8, 8,
                Path.Combine(AppContext.BaseDirectory, "Content", "maps", "test-map_collisions.csv"));
            _player = new Player(collisions, new Vector2(12, 37 * 8));
            _camera = new Camera { Origin = new Vector2(CanvasWidth / 2f, CanvasHeight / 2f) };
            UpdateCamera();
        }

        protected override void Update(GameTime gameTime)
        {
            Globals.Input.Update();
            if (Keyboard.GetState().IsKeyDown(Keys.Escape))
                Exit();

            _tileMap.Update(gameTime);
            _player.Update(gameTime);
            UpdateCamera();

            base.Update(gameTime);
        }

        protected override void Draw(GameTime gameTime)
        {
            _canvas.Activate();
            GraphicsDevice.Clear(Color.CornflowerBlue);

            _spriteBatch.Begin(samplerState: SamplerState.PointClamp, transformMatrix: _camera.Transform());
            _tileMap.Draw();
            _player.Draw();
            _spriteBatch.End();

            _canvas.Draw(_spriteBatch);

            base.Draw(gameTime);
        }

        private void UpdateCamera()
        {
            float x = MathHelper.Clamp(_player.Position.X - CanvasWidth / 2f, 0,
                Math.Max(0, _tileMap.Width * _tileMap.TileSizeX - CanvasWidth));
            float y = MathHelper.Clamp(_player.Position.Y - CanvasHeight / 2f, 0,
                Math.Max(0, _tileMap.Height * _tileMap.TileSizeY - CanvasHeight));
            _camera.Position = new Vector2(MathF.Round(x), MathF.Round(y)) + _camera.Origin;
        }
    }
}
