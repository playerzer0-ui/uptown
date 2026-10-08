using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using NodeTesting.models;
using System;

namespace uptown.SpecialObjects;

public sealed class Platform : SpecialObject
{
    public const int Height = 8;
    public const int DefaultWidthTiles = 6;
    private readonly Texture2D texture;
    private readonly bool joinedLeft;
    private readonly bool joinedRight;

    public Platform(Vector2 feet, int widthTiles = DefaultWidthTiles, bool joinedLeft = false, bool joinedRight = false)
        : base(feet, Math.Clamp(widthTiles, 1, 1048576) * 8, Height)
    {
        texture = Globals.Content.Load<Texture2D>("graphics/special_objects/platform");
        this.joinedLeft = joinedLeft;
        this.joinedRight = joinedRight;
    }

    public override bool Touches(Player player) => false;
    public override void Draw() => DrawPieces(texture, Collider.Rect, Color.White, joinedLeft, joinedRight);

    public static Rectangle Bounds(Point feet, int widthTiles) =>
        new(feet.X - Math.Clamp(widthTiles, 1, 1048576) * 4, feet.Y - Height,
            Math.Clamp(widthTiles, 1, 1048576) * 8, Height);

    public static int SelectTile(int index, int count, bool joinedLeft, bool joinedRight)
    {
        // Atlas: leftjoin, middlejoin, rightjoin, middlesolo, leftsolo,
        // rightsolo, leftedge, rightedge. Edge names follow the supporting
        // side: a run extending from left terrain ends with leftedge.
        if (count == 1) return joinedLeft && joinedRight ? 3 : joinedLeft ? 4 : 5;
        if (index == 0) return joinedLeft ? 0 : 7;
        if (index == count - 1) return joinedRight ? 2 : 6;
        return 1;
    }

    public static void DrawPieces(Texture2D texture, Rectangle bounds, Color tint, bool joinedLeft = false, bool joinedRight = false)
    {
        int count = bounds.Width / 8;
        for (int i = 0; i < count; i++)
        {
            int sourceX = SelectTile(i, count, joinedLeft, joinedRight) * 8;
            Globals.spriteBatch.Draw(texture, new Vector2(bounds.Left + i * 8, bounds.Top),
                new Rectangle(sourceX, 0, 8, Height), tint);
        }
    }

    public static bool BlocksDownward(Rectangle before, Rectangle after, Rectangle platform) =>
        after.Bottom > before.Bottom && before.Bottom <= platform.Top && after.Bottom > platform.Top
        && after.Right > platform.Left && after.Left < platform.Right;
}
