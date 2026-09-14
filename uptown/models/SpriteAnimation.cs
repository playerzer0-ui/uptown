using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using System.Collections.Generic;

namespace NodeTesting.models
{
    /// <summary>
    /// Represents a single animation state (e.g., Idle, Walk, Run)
    /// </summary>
    public class AnimationState
    {
        public string Name { get; set; }
        public int RowIndex { get; set; }
        public int FrameCount { get; set; }
        public int FramesPerSecond { get; set; }
        public bool IsLooping { get; set; } = true;
        public int StartFrame { get; set; } = 0;

        public AnimationState(string name, int rowIndex, int frameCount, int fps = 8, bool looping = true)
        {
            Name = name;
            RowIndex = rowIndex;
            FrameCount = frameCount;
            FramesPerSecond = fps;
            IsLooping = looping;
        }
    }

    /// <summary>
    /// Enhanced sprite animation with multi-row support for different animation states.
    /// </summary>
    public class SpriteAnimationMultiRow
    {
        protected Texture2D Texture;
        public Vector2 Position = Vector2.Zero;
        public Color Color = Color.White;
        public Vector2 Origin;
        public float Rotation = 0f;
        public float Scale = 1f;
        public SpriteEffects SpriteEffect;

        protected int FrameWidth;
        protected int FrameHeight;
        protected int Columns;
        protected int Rows;
        protected Rectangle[] Rectangles;

        // Animation state management
        protected Dictionary<string, AnimationState> States = new Dictionary<string, AnimationState>();
        protected AnimationState CurrentState;
        protected string CurrentStateName = "";

        // Frame tracking
        public int FrameIndex = 0;
        protected float TimeElapsed = 0f;
        protected float TimeToUpdate = 0.125f; // Default 8 FPS

        public bool IsFinished = false;
        public bool IsReversed = false;

        /// <summary>
        /// Initializes a new instance with multi-row sprite sheet support.
        /// </summary>
        /// <param name="texturePath">The path to the texture in the Content folder.</param>
        /// <param name="frameWidth">Width of each frame in pixels.</param>
        /// <param name="frameHeight">Height of each frame in pixels.</param>
        /// <param name="columns">Number of columns in the sprite sheet.</param>
        /// <param name="rows">Number of rows in the sprite sheet.</param>
        public SpriteAnimationMultiRow(string texturePath, int frameWidth, int frameHeight, int columns, int rows)
        {
            Texture = Globals.Content.Load<Texture2D>(texturePath);
            FrameWidth = frameWidth;
            FrameHeight = frameHeight;
            Columns = columns;
            Rows = rows;

            // Create rectangles for all frames
            Rectangles = new Rectangle[columns * rows];
            for (int row = 0; row < rows; row++)
            {
                for (int col = 0; col < columns; col++)
                {
                    int index = row * columns + col;
                    Rectangles[index] = new Rectangle(col * frameWidth, row * frameHeight, frameWidth, frameHeight);
                }
            }

            Origin = new Vector2(frameWidth / 2f, frameHeight / 2f);
        }

        /// <summary>
        /// Alternative constructor that auto-detects frame dimensions.
        /// </summary>
        public SpriteAnimationMultiRow(string texturePath, int columns, int rows)
        {
            Texture = Globals.Content.Load<Texture2D>(texturePath);
            Columns = columns;
            Rows = rows;
            FrameWidth = Texture.Width / columns;
            FrameHeight = Texture.Height / rows;

            // Create rectangles for all frames
            Rectangles = new Rectangle[columns * rows];
            for (int row = 0; row < rows; row++)
            {
                for (int col = 0; col < columns; col++)
                {
                    int index = row * columns + col;
                    Rectangles[index] = new Rectangle(col * FrameWidth, row * FrameHeight, FrameWidth, FrameHeight);
                }
            }

            Origin = new Vector2(FrameWidth / 2f, FrameHeight / 2f);
        }

        /// <summary>
        /// Adds an animation state with the specified parameters.
        /// </summary>
        public void AddState(string name, int rowIndex, int frameCount, int fps = 8, bool looping = true, int startFrame = 0)
        {
            var state = new AnimationState(name, rowIndex, frameCount, fps, looping)
            {
                StartFrame = startFrame
            };
            States[name] = state;
        }

        /// <summary>
        /// Adds an animation state with automatic frame detection.
        /// </summary>
        public void AddState(string name, int rowIndex, int fps = 8, bool looping = true)
        {
            int frameCount = Columns; // Assuming full row
            AddState(name, rowIndex, frameCount, fps, looping);
        }

        /// <summary>
        /// Plays the specified animation state.
        /// </summary>
        public void Play(string stateName, bool restart = true)
        {
            if (!States.ContainsKey(stateName))
                return;

            if (CurrentStateName != stateName || restart)
            {
                CurrentStateName = stateName;
                CurrentState = States[stateName];
                FrameIndex = CurrentState.StartFrame;
                TimeElapsed = 0f;
                IsFinished = false;
                TimeToUpdate = 1f / CurrentState.FramesPerSecond;
            }
        }

        /// <summary>
        /// Updates the current animation (looping).
        /// </summary>
        public void Update(GameTime gameTime)
        {
            if (CurrentState == null)
                return;

            TimeElapsed += (float)gameTime.ElapsedGameTime.TotalSeconds;

            if (TimeElapsed > TimeToUpdate)
            {
                TimeElapsed -= TimeToUpdate;

                if (!IsReversed)
                {
                    if (FrameIndex < CurrentState.StartFrame + CurrentState.FrameCount - 1)
                        FrameIndex++;
                    else if (CurrentState.IsLooping)
                        FrameIndex = CurrentState.StartFrame;
                    else
                        IsFinished = true;
                }
                else
                {
                    if (FrameIndex > CurrentState.StartFrame)
                        FrameIndex--;
                    else if (CurrentState.IsLooping)
                        FrameIndex = CurrentState.StartFrame + CurrentState.FrameCount - 1;
                    else
                        IsFinished = true;
                }
            }
        }

        /// <summary>
        /// Updates the animation once without looping (stops at last frame).
        /// </summary>
        public void UpdateOnce(GameTime gameTime)
        {
            if (CurrentState == null)
                return;

            TimeElapsed += (float)gameTime.ElapsedGameTime.TotalSeconds;

            if (TimeElapsed > TimeToUpdate)
            {
                TimeElapsed -= TimeToUpdate;

                if (!IsReversed)
                {
                    if (FrameIndex < CurrentState.StartFrame + CurrentState.FrameCount - 1)
                        FrameIndex++;
                    else
                        IsFinished = true;
                }
                else
                {
                    if (FrameIndex > CurrentState.StartFrame)
                        FrameIndex--;
                    else
                        IsFinished = true;
                }
            }
        }

        /// <summary>
        /// Draws the current frame.
        /// </summary>
        public void Draw()
        {
            if (CurrentState == null)
                return;

            Globals.spriteBatch.Draw(Texture, Position, Rectangles[FrameIndex], Color,
                Rotation, Origin, Scale, SpriteEffect, 0f);
        }

        /// <summary>
        /// Draws a specific frame by index.
        /// </summary>
        public void DrawFrame(int index)
        {
            if (index < 0 || index >= Rectangles.Length)
                return;

            Globals.spriteBatch.Draw(Texture, Position, Rectangles[index], Color,
                Rotation, Origin, Scale, SpriteEffect, 0f);
        }

        /// <summary>
        /// Gets the rectangle for the current frame.
        /// </summary>
        public Rectangle GetCurrentFrameRect()
        {
            if (CurrentState == null)
                return Rectangle.Empty;

            return Rectangles[FrameIndex];
        }

        /// <summary>
        /// Gets a specific frame rectangle by index.
        /// </summary>
        public Rectangle GetFrameRect(int index)
        {
            if (index < 0 || index >= Rectangles.Length)
                return Rectangle.Empty;

            return Rectangles[index];
        }

        /// <summary>
        /// Sets the frame manually.
        /// </summary>
        public void SetFrame(int frameIndex)
        {
            if (frameIndex >= 0 && frameIndex < Rectangles.Length)
                FrameIndex = frameIndex;
        }

        /// <summary>
        /// Gets the current state name.
        /// </summary>
        public string GetCurrentState() => CurrentStateName;

        /// <summary>
        /// Checks if the current animation has finished (for non-looping animations).
        /// </summary>
        public bool IsAnimationFinished() => IsFinished;

        /// <summary>
        /// Resets the animation to the start of the current state.
        /// </summary>
        public void Reset()
        {
            if (CurrentState != null)
            {
                FrameIndex = CurrentState.StartFrame;
                TimeElapsed = 0f;
                IsFinished = false;
            }
        }

        /// <summary>
        /// Toggles reverse playback.
        /// </summary>
        public void ToggleReverse() => IsReversed = !IsReversed;

        /// <summary>
        /// Gets the number of rows in the sprite sheet.
        /// </summary>
        public int GetRowCount() => Rows;

        /// <summary>
        /// Gets the number of columns in the sprite sheet.
        /// </summary>
        public int GetColumnCount() => Columns;

        /// <summary>
        /// Gets the total number of frames.
        /// </summary>
        public int GetTotalFrames() => Rectangles.Length;
    }

    // Optional: Backward compatibility wrapper for old single-row animations
    public class SpriteAnimation : SpriteAnimationMultiRow
    {
        public SpriteAnimation(string texturePath, int frames, int fps)
            : base(texturePath, frames, 1)
        {
            AddState("Default", 0, frames, fps);
            Play("Default");
        }

        //public void UpdateOnce(GameTime gameTime)
        //{
        //    // For single-row backward compatibility
        //    var oldState = CurrentState;
        //    if (oldState != null)
        //        oldState.IsLooping = false;
        //    Update(gameTime);
        //    if (oldState != null)
        //        oldState.IsLooping = true;
        //}
    }
}