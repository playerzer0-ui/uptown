using Microsoft.Xna.Framework;

namespace NodeTesting.models
{
    /// <summary>
    /// Represents a 2D camera used to control the view transformation of the game world.
    /// </summary>
    /// <remarks>
    /// The camera allows you to pan, zoom, and rotate the visible area of the game world.
    /// Use the <see cref="Transform"/> method to obtain the transformation matrix
    /// applied during sprite rendering.
    /// <br /><br />
    /// Use <see cref="MoveTo"/> to direct the camera toward a <see cref="CameraFocus"/> point,
    /// and call <see cref="Update"/> each frame to drive the transition.
    /// </remarks>
    public class Camera
    {
        private Vector2 position;
        private float zoom;
        private float rotation;
        private Vector2 origin;

        private CameraFocus activeFocus;
        private bool isFocusing;

        // How close position/zoom need to be before a OneShot focus is considered "arrived"
        private const float ArrivalThreshold = 0.5f;
        private const float ZoomArrivalThreshold = 0.01f;

        public Camera()
        {
            position = Vector2.Zero;
            zoom = 1f;
            rotation = 0f;
            origin = new Vector2(
                Globals.graphics.GraphicsDevice.Viewport.Width / 2,
                Globals.graphics.GraphicsDevice.Viewport.Height / 2);
        }

        public Vector2 Origin { get => origin; set => origin = value; }
        public Vector2 Position { get => position; set => position = value; }
        public float Zoom { get => zoom; set => zoom = value; }
        public float Rotation { get => rotation; set => rotation = value; }

        /// <summary>Whether the camera is currently moving toward or locked on a focus.</summary>
        public bool IsFocusing => isFocusing;

        /// <summary>
        /// Directs the camera to move toward the given <see cref="CameraFocus"/>.
        /// Call <see cref="Update"/> each frame to drive the transition.
        /// </summary>
        /// <param name="focus">The focus point to move to.</param>
        public void MoveTo(CameraFocus focus)
        {
            activeFocus = focus;
            isFocusing = true;

            if (focus.Mode == FocusMode.Instant)
            {
                ApplyFocusInstant(focus);

                if (focus.Anchor == FocusAnchor.OneShot)
                    isFocusing = false;
            }
        }

        /// <summary>
        /// Releases the active focus, stopping any further tracking or transition.
        /// </summary>
        public void Release()
        {
            activeFocus = null;
            isFocusing = false;
        }

        /// <summary>
        /// Updates the camera's position and zoom toward the active focus each frame.
        /// Call this once per frame in your game's Update loop.
        /// </summary>
        /// <param name="gameTime">The current game time.</param>
        public void Update(GameTime gameTime)
        {
            if (!isFocusing || activeFocus == null)
                return;

            if (activeFocus.Mode == FocusMode.Instant)
            {
                // Persistent instant: keep snapping (e.g. the focus Position may be moving)
                ApplyFocusInstant(activeFocus);
                return;
            }

            // Smooth lerp
            float speed = activeFocus.TransitionSpeed;
            position = Vector2.Lerp(position, activeFocus.Position, speed);
            zoom = MathHelper.Lerp(zoom, activeFocus.TargetZoom, speed);

            // For OneShot, release once close enough
            if (activeFocus.Anchor == FocusAnchor.OneShot)
            {
                bool posArrived = Vector2.Distance(position, activeFocus.Position) < ArrivalThreshold;
                bool zoomArrived = System.MathF.Abs(zoom - activeFocus.TargetZoom) < ZoomArrivalThreshold;

                if (posArrived && zoomArrived)
                {
                    ApplyFocusInstant(activeFocus); // snap clean on arrival
                    isFocusing = false;
                    activeFocus = null;
                }
            }
        }

        /// <summary>
        /// Creates and returns the transformation matrix used by the camera. <br />
        /// _spriteBatch.Begin(transformMatrix: camera.Transform());
        /// </summary>
        /// <returns>
        /// A <see cref="Matrix"/> that applies translation, rotation, scaling, and origin offset
        /// in the correct order for rendering the game world from the camera's perspective.
        /// </returns>
        public Matrix Transform()
        {
            return Matrix.CreateTranslation(new Vector3(-position, 0f)) *
                   Matrix.CreateRotationZ(rotation) *
                   Matrix.CreateScale(zoom) *
                   Matrix.CreateTranslation(new Vector3(origin, 0f));
        }

        public void Reset()
        {
            position = Vector2.Zero;
            zoom = 1f;
            Release();
        }

        // ─── Private helpers ────────────────────────────────────────────────

        private void ApplyFocusInstant(CameraFocus focus)
        {
            position = focus.Position;
            zoom = focus.TargetZoom;
        }
    }
}