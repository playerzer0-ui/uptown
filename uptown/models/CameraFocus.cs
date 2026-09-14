using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using System;
using System.Xml.Linq;

namespace NodeTesting.models
{
    /// <summary>
    /// Defines how the camera should transition to a focus point.
    /// </summary>
    public enum FocusMode
    {
        /// <summary>Snaps the camera instantly to the target.</summary>
        Instant,

        /// <summary>Smoothly lerps the camera toward the target each frame.</summary>
        Smooth
    }

    /// <summary>
    /// Defines whether the camera releases after arriving or stays locked on.
    /// </summary>
    public enum FocusAnchor
    {
        /// <summary>Camera moves to the target once, then stops tracking.</summary>
        OneShot,

        /// <summary>Camera continuously follows the target position.</summary>
        Persistent
    }

    /// <summary>
    /// Represents a point of interest the camera can zoom in to and focus on.
    /// // Smooth, one-shot zoom to a node's position (camera moves then releases)
    /// camera.MoveTo(new CameraFocus(node.Position, targetZoom: 3f, transitionSpeed: 0.08f));
    /// 
    /// // Persistent lock-on that follows a moving target (call every frame or on change)
    ///camera.MoveTo(new CameraFocus(enemy.Position, targetZoom: 2f, anchor: FocusAnchor.Persistent));
    ///
    /// // Instant snap
    ///camera.MoveTo(new CameraFocus(node.Position, mode: FocusMode.Instant));
    ///
    /// // Stop tracking
    /// camera.Release();
    /// </summary>
    public class CameraFocus
    {
        /// <summary>The world-space position to focus on.</summary>
        public Vector2 Position { get; set; }

        /// <summary>The zoom level to reach when focused on this point.</summary>
        public float TargetZoom { get; set; }

        /// <summary>
        /// Speed of the lerp transition (0f–1f). Higher = snappier.
        /// Only used when <see cref="Mode"/> is <see cref="FocusMode.Smooth"/>.
        /// </summary>
        public float TransitionSpeed { get; set; }

        /// <summary>Whether to snap instantly or lerp smoothly.</summary>
        public FocusMode Mode { get; set; }

        /// <summary>Whether to release after arriving or stay locked on.</summary>
        public FocusAnchor Anchor { get; set; }

        public CameraFocus(
            Vector2 position,
            float targetZoom = 2f,
            float transitionSpeed = 0.1f,
            FocusMode mode = FocusMode.Smooth,
            FocusAnchor anchor = FocusAnchor.OneShot)
        {
            Position = position;
            TargetZoom = targetZoom;
            TransitionSpeed = transitionSpeed;
            Mode = mode;
            Anchor = anchor;
        }
    }
}