using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using System.Collections.Generic;
using System.IO;

namespace NodeTesting.models
{
    /// <summary>
    /// Tracks playback state for a single animated tile definition.
    /// One instance is shared by every cell on the map using that base tile ID.
    /// </summary>
    internal class TileAnimation
    {
        private readonly int[] _frameTileIds;
        private readonly float _frameDuration; // seconds per frame
        private float _elapsed;
        private int _currentFrame;

        public TileAnimation(int[] frameTileIds, float frameDurationSeconds)
        {
            _frameTileIds = frameTileIds;
            _frameDuration = frameDurationSeconds;
        }

        public int CurrentFrameTileId => _frameTileIds[_currentFrame];

        public void Update(GameTime gameTime)
        {
            _elapsed += (float)gameTime.ElapsedGameTime.TotalSeconds;

            while (_elapsed >= _frameDuration)
            {
                _elapsed -= _frameDuration;
                _currentFrame = (_currentFrame + 1) % _frameTileIds.Length;
            }
        }
    }

    public abstract class Map
    {
        protected Texture2D Texture;
        protected Dictionary<int, Rectangle> TileSources;
        protected int[,] MapData;
        protected int MapWidth;
        protected int MapHeight;
        protected int TileWidth;
        protected int TileHeight;

        // Keyed by the tile ID that appears in the CSV. If a tileId shows up here,
        // Draw() substitutes the animation's current frame instead of a static rect.
        private readonly Dictionary<int, TileAnimation> Animations = new Dictionary<int, TileAnimation>();

        public int Width => MapWidth;
        public int Height => MapHeight;
        public int TileSizeX => TileWidth;
        public int TileSizeY => TileHeight;

        protected Map(string texturePath, int tileWidth, int tileHeight)
        {
            TileWidth = tileWidth;
            TileHeight = tileHeight;
            Texture = Globals.Content.Load<Texture2D>(texturePath);
            SplitTileset();
        }

        private void SplitTileset()
        {
            TileSources = new Dictionary<int, Rectangle>();

            int tilesPerRow = Texture.Width / TileWidth;
            int tilesPerColumn = Texture.Height / TileHeight;
            int tileId = 0;

            for (int row = 0; row < tilesPerColumn; row++)
            {
                for (int col = 0; col < tilesPerRow; col++)
                {
                    TileSources.Add(tileId, new Rectangle(
                        col * TileWidth,
                        row * TileHeight,
                        TileWidth,
                        TileHeight
                    ));
                    tileId++;
                }
            }
        }

        protected void LoadCSV(string csvPath)
        {
            string fullPath = FindFile(csvPath);
            string[] lines = File.ReadAllLines(fullPath);
            MapHeight = lines.Length;

            string[] firstLine = lines[0].Split(',');
            MapWidth = firstLine.Length;

            MapData = new int[MapHeight, MapWidth];

            for (int row = 0; row < MapHeight; row++)
            {
                string[] values = lines[row].Split(',');
                for (int col = 0; col < MapWidth; col++)
                {
                    if (col < values.Length && int.TryParse(values[col].Trim(), out int value))
                    {
                        MapData[row, col] = value;
                    }
                }
            }
        }

        private string FindFile(string fileName)
        {
            string[] paths = {
                Path.Combine(Directory.GetCurrentDirectory(), fileName),
                Path.Combine(Directory.GetCurrentDirectory(), "Content", fileName),
                Path.Combine(Directory.GetParent(Directory.GetCurrentDirectory()).Parent.Parent.FullName, "Content", fileName),
                fileName
            };

            foreach (string path in paths)
            {
                if (File.Exists(path))
                    return path;
            }

            throw new FileNotFoundException($"Could not find file '{fileName}'");
        }

        /// <summary>
        /// Marks a tile ID from the CSV as animated. Wherever baseTileId appears in
        /// MapData, Draw() will cycle through frameTileIds instead of drawing it statically.
        /// All frameTileIds must already exist in TileSources (i.e. be valid indices
        /// into the tileset).
        /// </summary>
        public void RegisterAnimation(int baseTileId, int[] frameTileIds, float frameDurationSeconds)
        {
            Animations[baseTileId] = new TileAnimation(frameTileIds, frameDurationSeconds);
        }

        /// <summary>
        /// Advances all registered tile animations. Call this once per frame
        /// from your game's Update(), before Draw().
        /// </summary>
        public virtual void Update(GameTime gameTime)
        {
            foreach (var animation in Animations.Values)
            {
                animation.Update(gameTime);
            }
        }

        public virtual void Draw()
        {
            for (int row = 0; row < MapHeight; row++)
            {
                for (int col = 0; col < MapWidth; col++)
                {
                    int tileId = MapData[row, col];

                    // If this tile ID is animated, swap in whichever frame is currently active.
                    if (Animations.TryGetValue(tileId, out TileAnimation animation))
                    {
                        tileId = animation.CurrentFrameTileId;
                    }

                    if (tileId >= 0 && TileSources.ContainsKey(tileId))
                    {
                        Vector2 position = new Vector2(col * TileWidth, row * TileHeight);
                        Globals.spriteBatch.Draw(Texture, position, TileSources[tileId], Color.White);
                    }
                }
            }
        }
    }
}