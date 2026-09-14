using Microsoft.Xna.Framework;
using NodeTesting.models;

namespace uptown.Modes;

// Placeholder for the future home menu. Navigation is currently in Game1.
public sealed class HomeMode : GameMode
{
    public override void Update(GameTime gameTime) { }
    public override void Draw() => Globals.graphics.GraphicsDevice.Clear(new Color(24, 30, 46));
}
