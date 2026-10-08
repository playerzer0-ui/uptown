using System;
using System.Collections.Generic;
using uptown.SpecialObjects;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using NodeTesting.models;

namespace uptown;

public class Player
{
    private const float WalkSpeed = 90f;
    private const float JumpSpeed = 105f;
    private const float Gravity = 900f;
    private const float RunAcceleration = 1000f;
    private const float AirControl = 0.65f;
    private const float JumpHoldDuration = 0.16f;
    private const float JumpBufferDuration = 0.1f;
    private const float PerfectBounceWindow = 0.1f;
    private const float WallSlideSpeed = 30f;
    private const float WallJumpSpeed = 110f;
    private readonly CollisionMap map;
    private readonly List<Platform> platforms;
    private readonly HashSet<int> ignoredPlatformTops = new();
    private float platformCrouchTime;
    // Where the player reappears after dying; checkpoints move it.
    public Vector2 Spawn { get; set; }
    private readonly SpriteAnimation idle = new("graphics/player/idle", 2, 4);
    private readonly SpriteAnimation walk = new("graphics/player/walk", 8, 12);
    private readonly SpriteAnimation push = new("graphics/player/push", 2, 6);
    private readonly SpriteAnimation climb = new("graphics/player/climb", 2, 8);
    private readonly SpriteAnimation jump = new("graphics/player/jump", 3, 8);
    private readonly SpriteAnimation crouch = new("graphics/player/crouch", 1, 1);
    private readonly SpriteAnimation crawl = new("graphics/player/crawl", 2, 8);
    private SpriteAnimation animation;
    private Vector2 remainder;
    private Vector2 velocity;
    private int facing = 1;
    private bool airJumpAvailable = true;
    private float coyoteTime;
    private float wallJumpTime;
    private bool isClimbHopping;
    private bool isClimbJumping;
    private float climbHopTargetX;
    private float jumpBuffer;
    private float jumpHoldTime;
    // Independent of jumpBuffer: an ordinary jump may consume that buffer
    // before PlayMode detects launcher contact later in the same frame.
    private float recentJumpPress;
    private float bounceBoostWindow;
    private float bounceBoostSpeed;
    private BounceDirection bounceBoostDirection;
    private Vector2 visualStretch = Vector2.One;

    // Position is the bottom-center of the player, matching the sprite's feet.
    public Vector2 Position { get; private set; }
    public bool IsGrounded { get; private set; }
    public bool IsClimbing { get; private set; }
    public bool IsWallSliding { get; private set; }
    public bool IsCrouching { get; private set; }
    public CollisionRect Collider { get; }

    public Player(CollisionMap map, Vector2 spawn, List<Platform> platforms = null)
    {
        this.map = map;
        this.platforms = platforms ?? new List<Platform>();
        Spawn = spawn;
        Position = spawn;
        Collider = new CollisionRect((int)spawn.X, (int)spawn.Y - 6, 8, 12);
        animation = idle;
        foreach (var sprite in new[] { idle, walk, push, climb, jump, crouch, crawl })
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
        bool jumpPressed = input.JustPressed("Jump");
        recentJumpPress = jumpPressed ? PerfectBounceWindow : Math.Max(0, recentJumpPress - dt);
        bounceBoostWindow = Math.Max(0, bounceBoostWindow - dt);
        jumpBuffer = jumpPressed ? JumpBufferDuration : Math.Max(0, jumpBuffer - dt);
        if (jumpPressed && bounceBoostWindow > 0)
            Bounce(bounceBoostDirection, bounceBoostSpeed);
        jumpHoldTime = Math.Max(0, jumpHoldTime - dt);
        visualStretch = Vector2.Lerp(visualStretch, Vector2.One, 1f - MathF.Exp(-18f * dt));
        int moveX = (input.IsPressed("Right") || input.KeyDown(Keys.Right) ? 1 : 0)
                  - (input.IsPressed("Left") || input.KeyDown(Keys.Left) ? 1 : 0);
        int moveY = (input.IsPressed("Down") || input.KeyDown(Keys.Down) ? 1 : 0)
                  - (input.IsPressed("Up") || input.KeyDown(Keys.Up) ? 1 : 0);
        const float stickDeadzone = 0.25f;
        if (moveX == 0 && Math.Abs(input.LeftStick.X) > stickDeadzone)
            moveX = Math.Sign(input.LeftStick.X);
        if (moveY == 0 && Math.Abs(input.LeftStick.Y) > stickDeadzone)
            moveY = -Math.Sign(input.LeftStick.Y);

        ignoredPlatformTops.RemoveWhere(top => Collider.Rect.Top > top);
        IsGrounded = velocity.Y >= 0 && SolidAt(0, 1);
        if (IsGrounded && moveY > 0 && !IsClimbing)
            SetCrouching(true);
        else if (IsCrouching)
            TryStand();
        Platform support = SupportingPlatform();
        platformCrouchTime = IsGrounded && IsCrouching && moveY > 0 && support != null
            ? platformCrouchTime + dt : 0;
        if (platformCrouchTime >= 0.3f)
        {
            ignoredPlatformTops.Add(support.Collider.Rect.Top);
            platformCrouchTime = 0;
            IsGrounded = false;
            coyoteTime = jumpBuffer = jumpHoldTime = 0;
            velocity.Y = Math.Max(velocity.Y, 30f);
        }
        coyoteTime = IsGrounded ? 0.1f : Math.Max(0, coyoteTime - dt);
        if (IsGrounded) airJumpAvailable = true;
        wallJumpTime = Math.Max(0, wallJumpTime - dt);
        bool wasClimbing = IsClimbing;
        if (velocity.Y >= 0 || !input.IsPressed("Climb")) isClimbJumping = false;
        if (moveX != 0 && wallJumpTime == 0 && !isClimbHopping) facing = moveX;
        // Only grab real terrain beside the upper body. Feet brushing a ledge
        // and the invisible map boundary must not count as climbable walls.
        int wall = CanGrabWall(facing) ? facing : 0;
        // An existing upward climb may finish clearing the ledge with its feet.
        if (wall == 0 && wasClimbing && moveY < 0 && !IsGrounded && SolidAt(facing, 0))
            wall = facing;
        IsClimbing = input.IsPressed("Climb") && wall != 0 && wallJumpTime == 0
            && !isClimbHopping && !isClimbJumping && !IsCrouching;

        if (IsClimbing)
        {
            jumpHoldTime = 0;
            facing = wall;
            velocity = new Vector2(0, moveY * 45f);
        }
        else
        {
            if (wallJumpTime == 0 && !isClimbHopping)
            {
                float acceleration = Math.Abs(velocity.X) > WalkSpeed && Math.Sign(velocity.X) == moveX
                    ? 400f : RunAcceleration;
                velocity.X = Approach(velocity.X, moveX * (IsCrouching ? 30f : WalkSpeed),
                    acceleration * (IsGrounded ? 1f : AirControl) * dt);
            }
            float gravity = isClimbHopping ? 500f : Gravity;
            if (!isClimbHopping && input.IsPressed("Jump") && Math.Abs(velocity.Y) < 40f)
                gravity *= 0.5f;
            velocity.Y = Math.Min(velocity.Y + gravity * dt, 180f);
            if (jumpHoldTime > 0 && input.IsPressed("Jump"))
                velocity.Y = Math.Min(velocity.Y, -JumpSpeed);
        }

        // Wall jumps work from either side even without grab input, and take
        // priority over the double jump while airborne beside real terrain.
        int jumpWall = IsClimbing ? wall : !IsGrounded && wallJumpTime == 0 && !isClimbHopping
            ? (CanGrabWall(facing) ? facing : CanGrabWall(-facing) ? -facing : 0) : 0;
        if (jumpBuffer > 0 && (coyoteTime > 0 || jumpWall != 0 || airJumpAvailable) && TryStand())
        {
            bool climbJump = jumpWall != 0 && input.IsPressed("Climb");
            if (climbJump)
            {
                velocity.X = 0;
                remainder.X = 0;
            }
            else if (jumpWall != 0)
            {
                velocity.X = -jumpWall * WallJumpSpeed;
                remainder.X = 0;
                facing = -jumpWall;
                wallJumpTime = 0.15f;
            }
            else if (coyoteTime <= 0) airJumpAvailable = false;
            else velocity.X += moveX * 15f;
            BeginJump();
            // Keep the upward impulse until the apex instead of immediately
            // replacing it with the climbing speed on the next frame.
            isClimbJumping = climbJump;
        }
        // Release ends the upward hold; gravity then produces a natural short
        // arc rather than abruptly cutting the launch velocity.
        if (!input.IsPressed("Jump")) jumpHoldTime = 0;

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
        float landingSpeed = velocity.Y;
        bool wasGrounded = IsGrounded;
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
                jumpHoldTime = 0;
                climbHopTargetX = Position.X + facing * 8;
            }
        }
        IsGrounded = velocity.Y >= 0 && SolidAt(0, 1);
        if (IsGrounded)
        {
            airJumpAvailable = true;
            isClimbHopping = false;
            jumpHoldTime = 0;
            if (!wasGrounded && landingSpeed > 0)
            {
                float impact = MathHelper.Clamp(landingSpeed / 180f, 0, 1);
                visualStretch = new Vector2(1 + impact * 0.22f, 1 - impact * 0.22f);
            }
            // Consume a late press on the landing frame, without an idle frame
            // between landing and jumping. A held button alone never repeats.
            if (jumpBuffer > 0 && TryStand()) BeginJump();
        }
        if (Position.Y > map.Height * map.TileSizeY + 32) Respawn();
        IsWallSliding = CanWallSlide(moveX);

        // Input and wall contact remain stable even when collision resolution
        // alternates between zero velocity and subpixel acceleration.
        bool isPushing = IsGrounded && moveX != 0 && SolidAt(moveX, 0);
        bool isCrawling = IsCrouching && IsGrounded && Math.Abs(velocity.X) > 1
            && !SolidAt(Math.Sign(velocity.X), 0);
        var next = IsCrouching ? (isCrawling ? crawl : crouch) : IsClimbing || IsWallSliding ? climb : !IsGrounded ? jump : isPushing ? push
            : Math.Abs(velocity.X) > 1 ? walk : idle;
        if (next != animation)
        {
            animation = next;
            animation.Reset();
        }
        if (animation == crouch)
            crouch.SetFrame(0);
        else if (IsWallSliding)
            climb.SetFrame(0);
        else if (animation == jump)
            jump.SetFrame(velocity.Y < -30 ? 0 : velocity.Y > 30 ? 2 : 1);
        else if (!IsClimbing || moveY != 0)
            animation.Update(gameTime);
    }

    private void BeginJump()
    {
        velocity.Y = -JumpSpeed;
        jumpHoldTime = Globals.Input.IsPressed("Jump") ? JumpHoldDuration : 0;
        jumpBuffer = coyoteTime = 0;
        remainder.Y = 0;
        isClimbHopping = false;
        isClimbJumping = false;
        IsGrounded = IsClimbing = IsWallSliding = false;
        visualStretch = new Vector2(0.85f, 1.15f);
        jump.Reset();
    }

    public void Bounce(BounceDirection direction, float speed)
    {
        recentJumpPress = bounceBoostWindow = 0;
        TryStand();
        velocity = uptown.SpecialObjects.ObjectRotation.Vector(direction) * speed;
        remainder = Vector2.Zero;
        jumpBuffer = jumpHoldTime = coyoteTime = 0;
        // Preserve sideways launch momentum and prevent an immediate wall grab.
        wallJumpTime = 0.2f;
        isClimbHopping = isClimbJumping = false;
        IsGrounded = IsClimbing = IsWallSliding = false;
        airJumpAvailable = true;
        visualStretch = new Vector2(0.85f, 1.15f);
        jump.Reset();
    }

    public void BounceTimed(BounceDirection direction, float speed, float perfectSpeed)
    {
        bool perfect = recentJumpPress > 0;
        Bounce(direction, perfect ? perfectSpeed : speed);
        if (!perfect)
        {
            // A fresh press shortly after contact upgrades this launch once.
            bounceBoostDirection = direction;
            bounceBoostSpeed = perfectSpeed;
            bounceBoostWindow = PerfectBounceWindow;
        }
    }

    private bool CanWallSlide(int moveX) =>
        !IsGrounded && !IsClimbing && !IsCrouching && !isClimbHopping && wallJumpTime == 0
        && velocity.Y > 0 && moveX != 0 && CanGrabWall(moveX);

    private bool SolidAt(int dx, int dy)
    {
        Rectangle before = Collider.Rect;
        // Probe with the existing collider, then restore it without allocating another.
        Collider.Translate(dx, dy);
        try
        {
            if (Collider.Rect.Left < 0 || Collider.Rect.Right > map.Width * map.TileSizeX)
                return true;
            if (map.CheckCollision(Collider)) return true;
            if (dx == 0 && dy > 0)
                foreach (var platform in platforms)
                    if (!ignoredPlatformTops.Contains(platform.Collider.Rect.Top)
                        && Platform.BlocksDownward(before, Collider.Rect, platform.Collider.Rect)) return true;
            return false;
        }
        finally
        {
            Collider.Translate(-dx, -dy);
        }
    }

    private Platform SupportingPlatform()
    {
        foreach (var platform in platforms)
        {
            Rectangle bounds = platform.Collider.Rect;
            if (!ignoredPlatformTops.Contains(bounds.Top) && Collider.Rect.Bottom == bounds.Top
                && Collider.Rect.Right > bounds.Left && Collider.Rect.Left < bounds.Right) return platform;
        }
        return null;
    }

    private bool CanGrabWall(int direction)
    {
        int x = direction > 0 ? Collider.Rect.Right : Collider.Rect.Left - 1;
        // Include the torso so a single 8px tile is reachable by the 12px
        // hitbox, but exclude the lowest 3px to reject foot-only contact.
        for (int y = Collider.Rect.Top + 3; y < Collider.Rect.Bottom - 3; y++)
            if (map.IsSolidAt(x, y)) return true;
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
                bounceBoostWindow = 0;
                if (horizontal) { velocity.X = 0; remainder.X = 0; }
                else { velocity.Y = 0; remainder.Y = 0; jumpHoldTime = 0; }
                break;
            }
            Position += horizontal ? new Vector2(step, 0) : new Vector2(0, step);
            SyncCollider();
            pixels -= step;
        }
    }

    private void Respawn()
    {
        ignoredPlatformTops.Clear();
        platformCrouchTime = 0;
        recentJumpPress = bounceBoostWindow = 0;
        Position = Spawn;
        SetCrouching(false);
        velocity = remainder = Vector2.Zero;
        coyoteTime = wallJumpTime = 0;
        jumpBuffer = jumpHoldTime = 0;
        visualStretch = Vector2.One;
        isClimbHopping = false;
        isClimbJumping = false;
        airJumpAvailable = true;
        IsClimbing = false;
        IsWallSliding = false;
    }

    private static float Approach(float value, float target, float amount) =>
        value < target ? Math.Min(value + amount, target) : Math.Max(value - amount, target);

    private void SyncCollider() =>
        Collider.UpdateRect((int)Position.X, (int)Position.Y - Collider.Rect.Height / 2);

    private void SetCrouching(bool crouching)
    {
        IsCrouching = crouching;
        Collider.Resize(8, crouching ? 6 : 12);
        SyncCollider();
    }

    private bool TryStand()
    {
        if (!IsCrouching) return true;
        SetCrouching(false);
        if (!map.CheckCollision(Collider)) return true;
        SetCrouching(true);
        return false;
    }

    public void Draw()
    {
        animation.Position = Position;
        animation.Stretch = visualStretch;
        animation.SpriteEffect = facing < 0 ? SpriteEffects.FlipHorizontally : SpriteEffects.None;
        animation.Draw();
        //Collider.Draw(Color.Red * 0.5f);
    }
}
