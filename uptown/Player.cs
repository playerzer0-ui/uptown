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
    private const float WallSlideSpeed = 30f;
    private const float WallJumpSpeed = 110f;
    private readonly CollisionMap map;
    private readonly Vector2 spawn;
    private readonly SpriteAnimation idle = new("graphics/player/idle", 2, 4);
    private readonly SpriteAnimation walk = new("graphics/player/walk", 8, 12);
    private readonly SpriteAnimation push = new("graphics/player/push", 2, 6);
    private readonly SpriteAnimation climb = new("graphics/player/climb", 2, 8);
    private readonly SpriteAnimation jump = new("graphics/player/jump", 3, 8);
    private SpriteAnimation animation;
    private Vector2 remainder;
    private Vector2 velocity;
    private int facing = 1;
    private bool airJumpAvailable = true;
    private float coyoteTime;
    private float wallJumpTime;
    private bool isClimbHopping;
    private float climbHopTargetX;

    // Position is the bottom-center of the player, matching the sprite's feet.
    public Vector2 Position { get; private set; }
    public bool IsGrounded { get; private set; }
    public bool IsClimbing { get; private set; }
    public bool IsWallSliding { get; private set; }
    public CollisionRect Collider { get; }

    public Player(CollisionMap map, Vector2 spawn)
    {
        this.map = map;
        this.spawn = spawn;
        Position = spawn;
        Collider = new CollisionRect((int)spawn.X, (int)spawn.Y - 6, 8, 12);
        animation = idle;
        foreach (var sprite in new[] { idle, walk, push, climb, jump })
            sprite.Origin = new Vector2(8, 16);

        Globals.Input.Register("Left", Keys.A, Buttons.DPadLeft);
        Globals.Input.Register("Right", Keys.D, Buttons.DPadRight);
        Globals.Input.Register("Up", Keys.W, Buttons.DPadUp);
        Globals.Input.Register("Down", Keys.S, Buttons.DPadDown);
        Globals.Input.Register("Jump", Keys.Space, Buttons.A);
        Globals.Input.Register("Climb", Keys.J, Buttons.RightShoulder);
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
        const float stickDeadzone = 0.25f;
        if (moveX == 0 && Math.Abs(input.LeftStick.X) > stickDeadzone)
            moveX = Math.Sign(input.LeftStick.X);
        if (moveY == 0 && Math.Abs(input.LeftStick.Y) > stickDeadzone)
            moveY = -Math.Sign(input.LeftStick.Y);

        IsGrounded = SolidAt(0, 1);
        coyoteTime = IsGrounded ? 0.1f : Math.Max(0, coyoteTime - dt);
        if (IsGrounded) airJumpAvailable = true;
        wallJumpTime = Math.Max(0, wallJumpTime - dt);
        bool wasClimbing = IsClimbing;
        if (moveX != 0 && wallJumpTime == 0 && !isClimbHopping) facing = moveX;
        // Only grab real terrain beside the upper body. Feet brushing a ledge
        // and the invisible map boundary must not count as climbable walls.
        int wall = CanGrabWall(facing) ? facing : 0;
        // An existing upward climb may finish clearing the ledge with its feet.
        if (wall == 0 && wasClimbing && moveY < 0 && !IsGrounded && SolidAt(facing, 0))
            wall = facing;
        IsClimbing = input.IsPressed("Climb") && wall != 0 && wallJumpTime == 0 && !isClimbHopping;

        if (IsClimbing)
        {
            facing = wall;
            velocity = new Vector2(0, moveY * 45f);
        }
        else
        {
            if (wallJumpTime == 0 && !isClimbHopping)
                velocity.X = Approach(velocity.X, moveX * WalkSpeed, 700f * dt);
            velocity.Y = Math.Min(velocity.Y + Gravity * dt, 200f);
        }

        // Wall jumps work from either side even without grab input, and take
        // priority over the double jump while airborne beside real terrain.
        int jumpWall = IsClimbing ? wall : !IsGrounded && wallJumpTime == 0 && !isClimbHopping
            ? (CanGrabWall(facing) ? facing : CanGrabWall(-facing) ? -facing : 0) : 0;
        if (input.JustPressed("Jump") && (coyoteTime > 0 || jumpWall != 0 || airJumpAvailable))
        {
            if (jumpWall != 0)
            {
                velocity.X = -jumpWall * WallJumpSpeed;
                remainder.X = 0;
                facing = -jumpWall;
                wallJumpTime = 0.15f;
            }
            else if (coyoteTime <= 0) airJumpAvailable = false;
            velocity.Y = -JumpSpeed;
            isClimbHopping = false;
            remainder.Y = 0;
            coyoteTime = 0;
            IsGrounded = false;
            IsClimbing = false;
            jump.Reset();
        }
        // Releasing jump early makes a shorter hop.
        if (!input.IsPressed("Jump") && velocity.Y < -60f && !IsClimbing && !isClimbHopping)
            velocity.Y = -60f;

        float horizontalDistance = velocity.X * dt;
        if (isClimbHopping)
        {
            float remaining = climbHopTargetX - Position.X;
            horizontalDistance = Math.Sign(remaining) * Math.Min(Math.Abs(remaining), 60f * dt);
        }
        Move(horizontalDistance, true);
        if (isClimbHopping && Position.X == climbHopTargetX)
        {
            velocity.X = 0;
            remainder.X = 0;
        }
        // Check after horizontal movement so reaching a wall slows the fall
        // immediately. Rising jumps are never slowed by wall contact.
        IsWallSliding = CanWallSlide(moveX);
        if (IsWallSliding)
        {
            facing = moveX;
            velocity.Y = Math.Min(velocity.Y, WallSlideSpeed);
        }
        Move(velocity.Y * dt, false);
        // Once the feet clear the wall, carry the player over its edge instead
        // of letting gravity drop them back into the same climbing contact.
        if (IsClimbing && moveY < 0 && !SolidAt(facing, 0))
        {
            IsClimbing = false;
            if (!SolidAt(0, -1) && !SolidAt(facing, -1))
            {
                velocity = new Vector2(facing * 60f, -60f);
                remainder = Vector2.Zero;
                isClimbHopping = true;
                climbHopTargetX = Position.X + facing * 8;
            }
        }
        IsGrounded = velocity.Y >= 0 && SolidAt(0, 1);
        if (IsGrounded)
        {
            airJumpAvailable = true;
            isClimbHopping = false;
        }
        if (Position.Y > map.Height * map.TileSizeY + 32) Respawn();
        IsWallSliding = CanWallSlide(moveX);

        // Input and wall contact remain stable even when collision resolution
        // alternates between zero velocity and subpixel acceleration.
        bool isPushing = IsGrounded && moveX != 0 && SolidAt(moveX, 0);
        var next = IsClimbing || IsWallSliding ? climb : !IsGrounded ? jump : isPushing ? push
            : Math.Abs(velocity.X) > 1 ? walk : idle;
        if (next != animation)
        {
            animation = next;
            animation.Reset();
        }
        if (IsWallSliding)
            climb.SetFrame(0);
        else if (animation == jump)
            jump.SetFrame(velocity.Y < -30 ? 0 : velocity.Y > 30 ? 2 : 1);
        else if (!IsClimbing || moveY != 0)
            animation.Update(gameTime);
    }

    private bool CanWallSlide(int moveX) =>
        !IsGrounded && !IsClimbing && !isClimbHopping && wallJumpTime == 0
        && velocity.Y > 0 && moveX != 0 && CanGrabWall(moveX);

    private bool SolidAt(int dx, int dy)
    {
        // Probe with the existing collider, then restore it without allocating another.
        Collider.Translate(dx, dy);
        try
        {
            if (Collider.Rect.Left < 0 || Collider.Rect.Right > map.Width * map.TileSizeX)
                return true;
            return map.CheckCollision(Collider);
        }
        finally
        {
            Collider.Translate(-dx, -dy);
        }
    }

    private bool CanGrabWall(int direction)
    {
        int x = direction > 0 ? Collider.Rect.Right : Collider.Rect.Left - 1;
        // Include the torso so a single 8px tile is reachable by the 12px
        // hitbox, but exclude the lowest 3px to reject foot-only contact.
        foreach (var tile in map.GetCollisionRects())
        {
            for (int y = Collider.Rect.Top + 3; y < Collider.Rect.Bottom - 3; y++)
                if (tile.Contains(new Point(x, y))) return true;
        }
        return false;
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
            Collider.UpdateRect((int)Position.X, (int)Position.Y - 6);
            pixels -= step;
        }
    }

    private void Respawn()
    {
        Position = spawn;
        Collider.UpdateRect((int)Position.X, (int)Position.Y - 6);
        velocity = remainder = Vector2.Zero;
        coyoteTime = wallJumpTime = 0;
        isClimbHopping = false;
        airJumpAvailable = true;
        IsClimbing = false;
        IsWallSliding = false;
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
