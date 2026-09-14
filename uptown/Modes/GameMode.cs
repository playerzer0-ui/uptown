using Microsoft.Xna.Framework;

namespace uptown.Modes;

public enum ModeId { Play, Editor, Home }

// Game1 owns input polling and canvas presentation. Only the active mode runs.
public abstract class GameMode
{
    public virtual void Enter() { }
    public virtual void Leave() { }
    public abstract void Update(GameTime gameTime);
    public abstract void Draw();
}
