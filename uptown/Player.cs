using System;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using NodeTesting.models;

namespace uptown;

public class Player
{
    private const float WalkSpeed = 80f;
    private const float JumpSpeed = 150f;
    private const float Gravity = 500f;
    private readonly CollisionMap map;
    private readonly Vector2 spawn;
    private readonly SpriteAnimation idle = new("graphics/player/idle", 2, 4);
    private readonly SpriteAnimation walk = new("graphics/player/walk", 8, 12);
    private readonly SpriteAnimation climb = new("graphics/player/climb", 2, 8);
    private readonly SpriteAnimation jump = new("graphics/player/jump", 3, 8);
    private SpriteAnimation animation;
    private Vector2 remainder;
    private Vector2 velocity;
    private int facing = 1;
    private bool airJumpAvailable = true;
    private float coyoteTime;
    private float wallJumpTime;

    // Position is the bottom-center of the player, matching the sprite's feet.
    public Vector2 Position { get; private set; }
    public bool IsGrounded { get; private set; }
    public bool IsClimbing { get; private set; }
    public Rectangle Bounds => new((int)Position.X - 4, (int)Position.Y - 12, 8, 12);

    public Player(CollisionMap map, Vector2 spawn)
    {
        this.map = map;
        this.spawn = spawn;
        Position = spawn;
        animation = idle;
        foreach (var sprite in new[] { idle, walk, climb, jump })
            sprite.Origin = new Vector2(8, 16);

        Globals.Input.Register("Left", Keys.A, Buttons.DPadLeft);
        Globals.Input.Register("Right", Keys.D, Buttons.DPadRight);
        Globals.Input.Register("Up", Keys.W, Buttons.DPadUp);
        Globals.Input.Register("Down", Keys.S, Buttons.DPadDown);
        Globals.Input.Register("Jump", Keys.Space, Buttons.A);
        Globals.Input.Register("Climb", Keys.LeftShift, Buttons.RightShoulder);
        Globals.Input.Register("Respawn", Keys.R, Buttons.Back);
    }

    public void Update(GameTime gameTime)
    {
        float dt = (float)gameTime.ElapsedGameTime.TotalSeconds;
        var input = Globals.Input;
        if (input.JustPressed("Respawn")) Respawn();
        int moveX = (input.IsPressed("Right") || input.KeyDown(Keys.Right) ? 1 : 0)
                  - (input.IsPressed("Left") || input.KeyDown(Keys.Left) ? 1 : 0);
        int moveY = (input.IsPressed("Down") || input.KeyDown(Keys.Down) ? 1 : 0)
                  - (input.IsPressed("Up") || input.KeyDown(Keys.Up) ? 1 : 0);

        IsGrounded = SolidAt(0, 1);
        coyoteTime = IsGrounded ? 0.1f : Math.Max(0, coyoteTime - dt);
        if (IsGrounded) airJumpAvailable = true;
        wallJumpTime = Math.Max(0, wallJumpTime - dt);
        if (moveX != 0 && wallJumpTime == 0) facing = moveX;
        int wall = SolidAt(facing, 0) ? facing : SolidAt(-facing, 0) ? -facing : 0;
        IsClimbing = input.IsPressed("Climb") && wall != 0 && wallJumpTime == 0;

        if (IsClimbing)
        {
            facing = wall;
            velocity = new Vector2(0, moveY * 45f);
        }
        else
        {
            if (wallJumpTime == 0)
                velocity.X = Approach(velocity.X, moveX * WalkSpeed, 700f * dt);
            velocity.Y = Math.Min(velocity.Y + Gravity * dt, 200f);
        }

        if (input.JustPressed("Jump") && (coyoteTime > 0 || IsClimbing || airJumpAvailable))
        {
            if (IsClimbing)
            {
                velocity.X = -wall * WalkSpeed;
                facing = -wall;
                wallJumpTime = 0.15f;
            }
            else if (coyoteTime <= 0) airJumpAvailable = false;
            velocity.Y = -JumpSpeed;
            remainder.Y = 0;
            coyoteTime = 0;
            IsGrounded = false;
            IsClimbing = false;
            jump.Reset();
        }
        // Releasing jump early makes a shorter hop.
        if (!input.IsPressed("Jump") && velocity.Y < -60f && !IsClimbing)
            velocity.Y = -60f;

        Move(velocity.X * dt, true);
        Move(velocity.Y * dt, false);
        IsGrounded = velocity.Y >= 0 && SolidAt(0, 1);
        if (IsGrounded) airJumpAvailable = true;
        if (Position.Y > map.Height * map.TileSizeY + 32) Respawn();

        var next = IsClimbing ? climb : !IsGrounded ? jump : Math.Abs(velocity.X) > 1 ? walk : idle;
        if (next != animation)
        {
            animation = next;
            animation.Reset();
        }
        if (animation == jump)
            jump.SetFrame(velocity.Y < -30 ? 0 : velocity.Y > 30 ? 2 : 1);
        else if (!IsClimbing || moveY != 0)
            animation.Update(gameTime);
    }

    private bool SolidAt(int dx, int dy)
    {
        Rectangle bounds = Bounds;
        bounds.Offset(dx, dy);
        if (bounds.Left < 0 || bounds.Right > map.Width * map.TileSizeX) return true;
        // Clamp tile queries to the map: above/below the level is empty space.
        if (bounds.Bottom <= 0 || bounds.Top >= map.Height * map.TileSizeY) return false;
        return map.GetIntersectingTiles(bounds).Count > 0;
    }

    private void Move(float distance, bool horizontal)
    {
        float total = distance + (horizontal ? remainder.X : remainder.Y);
        int pixels = (int)MathF.Round(total);
        if (horizontal) remainder.X = total - pixels;
        else remainder.Y = total - pixels;
        int step = Math.Sign(pixels);
        // One-pixel steps prevent passing through thin tiles at high speed.
        while (pixels != 0)
        {
            if (SolidAt(horizontal ? step : 0, horizontal ? 0 : step))
            {
                if (horizontal) { velocity.X = 0; remainder.X = 0; }
                else { velocity.Y = 0; remainder.Y = 0; }
                break;
            }
            Position += horizontal ? new Vector2(step, 0) : new Vector2(0, step);
            pixels -= step;
        }
    }

    private void Respawn()
    {
        Position = spawn;
        velocity = remainder = Vector2.Zero;
        coyoteTime = wallJumpTime = 0;
        airJumpAvailable = true;
        IsClimbing = false;
    }

    private static float Approach(float value, float target, float amount) =>
        value < target ? Math.Min(value + amount, target) : Math.Max(value - amount, target);

    public void Draw()
    {
        animation.Position = Position;
        animation.SpriteEffect = facing < 0 ? SpriteEffects.FlipHorizontally : SpriteEffects.None;
        animation.Draw();
    }
}
