using System;
using System.IO;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using NodeTesting.models;

namespace uptown.Modes;

public sealed class PlayMode : GameMode
{
    private readonly AutoTileMap tileMap;
    private readonly Player player;
    private readonly Camera camera;
    private readonly int viewWidth;
    private readonly int viewHeight;

    public PlayMode(int viewWidth, int viewHeight, LevelData level)
    {
        this.viewWidth = viewWidth;
        this.viewHeight = viewHeight;
        tileMap = new AutoTileMap("graphics/tileset/basic", level.CreateGrid(false));
        var collisions = new CollisionMap("graphics/tileset/collision", 8, 8,
            level.CreateGrid(true));
        player = new Player(collisions, new Vector2(level.SpawnX, level.SpawnY));
        camera = new Camera { Origin = new Vector2(viewWidth / 2f, viewHeight / 2f) };
        UpdateCamera();
    }

    public override void Update(GameTime gameTime)
    {
        tileMap.Update(gameTime);
        player.Update(gameTime);
        UpdateCamera();
    }

    public override void Draw()
    {
        Globals.graphics.GraphicsDevice.Clear(Color.CornflowerBlue);
        Globals.spriteBatch.Begin(samplerState: SamplerState.PointClamp, transformMatrix: camera.Transform());
        tileMap.Draw();
        player.Draw();
        Globals.spriteBatch.End();
    }

    private void UpdateCamera()
    {
        float x = MathHelper.Clamp(player.Position.X - viewWidth / 2f, 0,
            Math.Max(0, tileMap.Width * tileMap.TileSizeX - viewWidth));
        float y = MathHelper.Clamp(player.Position.Y - viewHeight / 2f, 0,
            Math.Max(0, tileMap.Height * tileMap.TileSizeY - viewHeight));
        camera.Position = new Vector2(MathF.Round(x), MathF.Round(y)) + camera.Origin;
    }
}
