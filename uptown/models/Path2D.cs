using Microsoft.Xna.Framework;
using System.Collections.Generic;

namespace NodeTesting.models
{
    /// <summary>
    /// Represents a series of waypoints that a sprite can travel along.
    /// Supports looping, reversing, speed control, and per-waypoint delays.
    /// </summary>
    public class Path2D
    {
        private List<Vector2> waypoints = new List<Vector2>();
        private List<float> waypointDelays = new List<float>();

        private int currentIndex = 0;
        private float speed = 100f;           // pixels per second
        private float delayTimer = 0f;
        private bool isWaiting = false;

        public bool IsLooping = false;
        public bool IsPingPong = false;
        public bool IsReversed = false;
        public bool IsFinished = false;
        public bool IsActive = true;

        /// <summary>
        /// The current world position along the path.
        /// Assign this to your sprite's position each frame.
        /// </summary>
        public Vector2 Position { get; private set; }

        /// <summary>
        /// Movement speed in pixels per second.
        /// </summary>
        public float Speed
        {
            get => speed;
            set => speed = MathHelper.Max(0f, value);
        }

        /// <summary>
        /// The index of the waypoint currently being moved toward.
        /// </summary>
        public int CurrentWaypointIndex => currentIndex;

        /// <summary>
        /// Creates an empty path. Add waypoints with <see cref="AddWaypoint"/>.
        /// </summary>
        public Path2D() { }

        /// <summary>
        /// Creates a path from an existing list of waypoints with no per-waypoint delays.
        /// The sprite will start at the first waypoint immediately.
        /// </summary>
        /// <param name="waypoints">Ordered list of positions to travel through.</param>
        /// <param name="speed">Movement speed in pixels per second.</param>
        public Path2D(List<Vector2> waypoints, float speed = 100f)
        {
            Speed = speed;
            foreach (var wp in waypoints)
                AddWaypoint(wp, 0f);

            if (this.waypoints.Count > 0)
                Position = this.waypoints[0];
        }

        /// <summary>
        /// Appends a waypoint to the end of the path.
        /// </summary>
        /// <param name="point">The position to add.</param>
        /// <param name="delay">
        /// How many seconds to pause at this waypoint before continuing.
        /// Use 0 for no delay.
        /// </param>
        public void AddWaypoint(Vector2 point, float delay = 0f)
        {
            waypoints.Add(point);
            waypointDelays.Add(MathHelper.Max(0f, delay));

            // Initialise position to the very first waypoint added
            if (waypoints.Count == 1)
                Position = point;
        }

        /// <summary>
        /// Removes all waypoints and resets path state.
        /// </summary>
        public void Clear()
        {
            waypoints.Clear();
            waypointDelays.Clear();
            currentIndex = 0;
            delayTimer = 0f;
            isWaiting = false;
            IsFinished = false;
        }

        /// <summary>
        /// Reverses the order of waypoints in place and jumps to the equivalent position
        /// in the reversed list so movement continues without a visible snap.
        /// </summary>
        public void ReversePath()
        {
            if (waypoints.Count == 0) return;

            // Mirror the current index so we stay on the same logical segment
            currentIndex = (waypoints.Count - 1) - currentIndex;

            waypoints.Reverse();
            waypointDelays.Reverse();

            IsFinished = false;
        }

        /// <summary>
        /// Flips the traversal direction without reordering the waypoints.
        /// Useful for ping-pong style movement.
        /// </summary>
        public void ReverseDirection()
        {
            IsReversed = !IsReversed;
            IsFinished = false;
        }

        /// <summary>
        /// Resets the path to its starting state (or end, if reversed) without clearing waypoints.
        /// </summary>
        public void Reset()
        {
            currentIndex = IsReversed ? waypoints.Count - 1 : 0;
            delayTimer = 0f;
            isWaiting = false;
            IsFinished = false;

            if (waypoints.Count > 0)
                Position = waypoints[currentIndex];
        }

        /// <summary>
        /// Advances the path each frame. Call this in your game's Update loop,
        /// then apply <see cref="Position"/> to your sprite.
        /// </summary>
        /// <param name="gameTime">The current <see cref="GameTime"/> from MonoGame.</param>
        public void Update(GameTime gameTime)
        {
            if (!IsActive || IsFinished || waypoints.Count < 2)
                return;

            float remaining = (float)gameTime.ElapsedGameTime.TotalSeconds;
            int transitions = 0;
            while (remaining > 0 && !IsFinished && transitions < 4096)
            {
                if (isWaiting)
                {
                    float waited = System.Math.Min(remaining, delayTimer);
                    remaining -= waited;
                    delayTimer -= waited;
                    if (delayTimer > 0) return;
                    isWaiting = false;
                    AdvanceIndex();
                    transitions++;
                    continue;
                }
                Vector2 target = waypoints[currentIndex];
                Vector2 delta = target - Position;
                float distance = delta.Length();
                if (distance > 0 && speed <= 0) return;
                float travelTime = distance > 0 ? distance / speed : 0;
                if (travelTime > remaining)
                {
                    Position += delta / distance * speed * remaining;
                    return;
                }
                Position = target;
                remaining -= travelTime;
                float delay = waypointDelays[currentIndex];
                if (delay > 0) { isWaiting = true; delayTimer = delay; }
                else AdvanceIndex();
                transitions++;
            }
        }

        /// <summary>
        /// Moves currentIndex to the next waypoint, handling looping, ping-pong, and end-of-path.
        /// </summary>
        private void AdvanceIndex()
        {
            if (!IsReversed)
            {
                if (currentIndex < waypoints.Count - 1)
                {
                    currentIndex++;
                }
                else if (IsPingPong)
                {
                    IsReversed = true;
                    currentIndex--;
                }
                else if (IsLooping)
                {
                    currentIndex = 0;
                }
                else
                {
                    IsFinished = true;
                }
            }
            else
            {
                if (currentIndex > 0)
                {
                    currentIndex--;
                }
                else if (IsPingPong)
                {
                    IsReversed = false;
                    currentIndex++;
                }
                else if (IsLooping)
                {
                    currentIndex = waypoints.Count - 1;
                }
                else
                {
                    IsFinished = true;
                }
            }
        }
    }
}
