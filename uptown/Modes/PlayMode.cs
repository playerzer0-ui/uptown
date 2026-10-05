using System;
using System.Linq;
using System.IO;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
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
    private readonly Texture2D stopIcon;
    private readonly Action returnToEditor;
    private Func<bool> levelCleared;
    private MouseState previousMouse;
    private Point pointer;
    private bool levelComplete;
    private bool levelSaved;
    private bool saveAttempted;
    private float viewWidth;
    private float viewHeight;
    private int windowWidth;
    private int windowHeight;
    private int renderScale = 1;

    public PlayMode(LevelData level, Action returnToEditor, Func<bool> levelCleared = null)
    {
        this.returnToEditor = returnToEditor;
        this.levelCleared = levelCleared;
        tileMap = new AutoTileMap(TerrainCatalog.Paths(), level.CreateGrid(false),
            level.CreateMaterials(TerrainCatalog.Names));
        var collisions = new CollisionMap("graphics/tileset/collision", 8, 8,
            level.CreateGrid(true));
        player = new Player(collisions, new Vector2(level.SpawnX, level.SpawnY));
        camera = new Camera();
        font = Globals.Content.Load<SpriteFont>("File");
        stopIcon = Globals.Content.Load<Texture2D>("graphics/ui/stop");
        foreach (var item in level.Objects)
        {
            var feet = new Vector2(item.X, item.Y);
            if (item.Type == LevelObject.Checkpoint) entities.Add(new Checkpoint(feet, item.Direction));
            else if (item.Type == LevelObject.ExitFlag) entities.Add(new ExitFlag(feet, item.Direction));
            else if (item.Type == LevelObject.BounceBall) entities.Add(new BounceBall(feet, item.Direction));
            else if (item.Type == LevelObject.Spring) entities.Add(new Spring(feet, item.Direction));
        }
        RefreshLayout();
    }

    public override void Enter() => previousMouse = Mouse.GetState();
    public override void Leave() => levelCleared = null;

    public override void Update(GameTime gameTime)
    {
        RefreshLayout();
        if (HandleStop(Mouse.GetState())) return;
        tileMap.Update(gameTime);
        // Once the level is complete the player freezes; objects keep animating.
        if (!levelComplete) player.Update(gameTime);
        entities.Update(gameTime);
        if (levelComplete) return;
        TouchSpecialObjects();
        UpdateCamera();
        CheckCompletion();
    }

    private void CheckCompletion()
    {
        if (levelComplete || !entities.OfType<ExitFlag>().Any(flag => flag.Reached)) return;
        levelComplete = true;
        var onCleared = levelCleared;
        levelCleared = null;
        saveAttempted = onCleared != null;
        levelSaved = onCleared?.Invoke() ?? false;
    }

    public override void Draw()
    {
        RefreshLayout();
        Globals.graphics.GraphicsDevice.Clear(PicoPallete.blue);
        Globals.spriteBatch.Begin(samplerState: SamplerState.PointClamp,
            transformMatrix: WindowRendering.PixelAligned(camera.Transform()));
        tileMap.Draw();
        entities.Draw();
        player.Draw();
        Globals.spriteBatch.End();

        if (levelComplete || levelCleared != null)
        {
            // Screen-space overlay, drawn without the camera transform.
            Globals.spriteBatch.Begin(samplerState: SamplerState.PointClamp,
                transformMatrix: Matrix.CreateScale(renderScale));
            if (levelComplete)
            {
                DrawCenteredText(saveAttempted && !levelSaved ? "SAVE" : "LEVEL",
                    new Vector2(viewWidth / 2f, viewHeight / 2f - 14), 2f);
                DrawCenteredText(levelSaved ? "SAVED" : saveAttempted ? "FAILED" : "COMPLETE",
                    new Vector2(viewWidth / 2f, viewHeight / 2f + 14), 2f);
            }
            else DrawCenteredText("CLEAR TO SAVE", new Vector2(viewWidth / 2f, 16), 1f);
            Globals.spriteBatch.End();
        }
        Globals.spriteBatch.Begin(samplerState: SamplerState.PointClamp);
        DrawStopButton();
        Globals.spriteBatch.End();
    }

    private Rectangle StopBounds() => new(windowWidth - 32 * renderScale, 4 * renderScale,
        24 * renderScale, 24 * renderScale);

    private bool HandleStop(MouseState mouse)
    {
        pointer = new Point(mouse.X, mouse.Y);
        bool clicked = mouse.LeftButton == ButtonState.Pressed && previousMouse.LeftButton == ButtonState.Released;
        previousMouse = mouse;
        if (!clicked || !StopBounds().Contains(pointer)) return false;
        returnToEditor();
        return true;
    }

    private void DrawStopButton()
    {
        Rectangle bounds = StopBounds();
        bool hovered = bounds.Contains(pointer);
        var batch = Globals.spriteBatch;
        batch.Draw(Globals.Pixel, bounds, hovered ? Color.White : new Color(24, 48, 63));
        var inset = new Rectangle(bounds.X + renderScale, bounds.Y + renderScale,
            bounds.Width - 2 * renderScale, bounds.Height - 2 * renderScale);
        Color background = new(225, 69, 59);
        batch.Draw(Globals.Pixel, inset, hovered ? Color.Lerp(background, Color.White, 0.2f) : background);
        batch.Draw(stopIcon, new Rectangle(bounds.X + 4 * renderScale, bounds.Y + 4 * renderScale,
            16 * renderScale, 16 * renderScale), Color.White);
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

    private void RefreshLayout()
    {
        var bounds = Globals.graphics.GraphicsDevice.PresentationParameters.Bounds;
        SetViewport(bounds.Width, bounds.Height);
    }

    private void SetViewport(int width, int height)
    {
        if (width <= 0 || height <= 0 || (width == windowWidth && height == windowHeight)) return;
        windowWidth = width;
        windowHeight = height;
        renderScale = WindowRendering.ScaleFor(width, height);
        viewWidth = (float)width / renderScale;
        viewHeight = (float)height / renderScale;
        camera.Zoom = renderScale;
        camera.Origin = new Vector2(width / 2f, height / 2f);
        UpdateCamera();
    }

    private void UpdateCamera()
    {
        float x = MathHelper.Clamp(player.Position.X - viewWidth / 2f, 0,
            Math.Max(0, tileMap.Width * tileMap.TileSizeX - viewWidth));
        float y = MathHelper.Clamp(player.Position.Y - viewHeight / 2f, 0,
            Math.Max(0, tileMap.Height * tileMap.TileSizeY - viewHeight));
        camera.Position = new Vector2(x + viewWidth / 2f, y + viewHeight / 2f);
    }
}
