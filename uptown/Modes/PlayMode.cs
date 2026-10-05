using System;
using System.Linq;
using System.IO;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using NodeTesting.models;
using uptown.SpecialObjects;

namespace uptown.Modes;

public sealed class PlayMode : GameMode
{
    private readonly AutoTileMap tileMap;
    private readonly Player player;
    private readonly Camera camera;
    private readonly EntityList entities = new();
    private readonly SpriteFont font;
    private bool levelComplete;
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
        font = Globals.Content.Load<SpriteFont>("File");
        foreach (var item in level.Objects)
        {
            var feet = new Vector2(item.X, item.Y);
            if (item.Type == LevelObject.Checkpoint) entities.Add(new Checkpoint(feet));
            else if (item.Type == LevelObject.ExitFlag) entities.Add(new ExitFlag(feet));
            else if (item.Type == LevelObject.BounceBall) entities.Add(new BounceBall(feet, item.Direction));
            else if (item.Type == LevelObject.Spring) entities.Add(new Spring(feet, item.Direction));
        }
        UpdateCamera();
    }

    public override void Update(GameTime gameTime)
    {
        tileMap.Update(gameTime);
        // Once the level is complete the player freezes; objects keep animating.
        if (!levelComplete) player.Update(gameTime);
        entities.Update(gameTime);
        if (levelComplete) return;
        TouchSpecialObjects();
        levelComplete = entities.OfType<ExitFlag>().Any(flag => flag.Reached);
        UpdateCamera();
    }

    public override void Draw()
    {
        Globals.graphics.GraphicsDevice.Clear(PicoPallete.blue);
        Globals.spriteBatch.Begin(samplerState: SamplerState.PointClamp, transformMatrix: camera.Transform());
        tileMap.Draw();
        entities.Draw();
        player.Draw();
        Globals.spriteBatch.End();

        if (levelComplete)
        {
            // Screen-space overlay, drawn without the camera transform.
            Globals.spriteBatch.Begin(samplerState: SamplerState.PointClamp);
            DrawCenteredText("LEVEL", new Vector2(viewWidth / 2f, viewHeight / 2f - 14), 2f);
            DrawCenteredText("COMPLETE", new Vector2(viewWidth / 2f, viewHeight / 2f + 14), 2f);
            Globals.spriteBatch.End();
        }
    }

    // Fires enter/stay/exit on every special object the player overlaps or leaves.
    private void TouchSpecialObjects()
    {
        // Copy first: a hook may remove its object (e.g. a broken block).
        foreach (var special in entities.OfType<SpecialObject>().ToList())
        {
            bool inside = special.Touches(player);
            if (inside && !special.PlayerInside) special.OnPlayerEnter(player);
            if (inside) special.OnPlayerStay(player);
            if (!inside && special.PlayerInside) special.OnPlayerExit(player);
            special.PlayerInside = inside;
        }
    }

    // Centered white text with a dark drop shadow.
    private void DrawCenteredText(string text, Vector2 center, float scale)
    {
        Vector2 origin = font.MeasureString(text) / 2;
        origin = new Vector2(MathF.Round(origin.X), MathF.Round(origin.Y));
        Globals.spriteBatch.DrawString(font, text, center + new Vector2(1, 1) * scale, PicoPallete.dark_blue,
            0f, origin, scale, SpriteEffects.None, 0f);
        Globals.spriteBatch.DrawString(font, text, center, Color.White, 0f, origin, scale, SpriteEffects.None, 0f);
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
