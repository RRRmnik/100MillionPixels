using Raylib_cs;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Text;
using System.Threading.Tasks;

namespace _100MillionPixels
{
    public enum BallType
    {
        PIERCING,
        POISON,
        SPEED,
        LIGHTNING
    }

    public class Ball
    {
        public const float Radius = 0.1f;

        public const float LightningInterval = 0.05f;

        public BallType Type;

        public Vector2 Position = new Vector2(0.0f, 0.0f);
        public Vector2 Direction = new Vector2(0.0f, 0.0f);
        public Vector2 Velocity => Direction * Speed;

        public float Speed => GetSpeed(Type);
        public Color Color => GetColor(Type);

        private float _lightningTimer = 0.0f;

        public Ball(BallType type, Vector2 position, Vector2 direction)
        {
            Type = type;
            Position = position;
            Direction = Vector2.Normalize(direction);
        }

        public void Tick(float dt, World world)
        {
            int Steps = (int)Math.Ceiling(120.0f * dt * Velocity.Length());
            Steps = Math.Clamp(Steps, 1, 3000);
            float stepDt = dt / Steps;

            for (int i = 0; i < Steps; i++) Step(stepDt, world);

            if (Type == BallType.LIGHTNING)
            {
                _lightningTimer -= dt;
                if (_lightningTimer <= 0.0f)
                {
                    _lightningTimer += LightningInterval;
                    Lightning(world);
                }
            }

            //Console.WriteLine(Steps);
        }

        public void Lightning(World world)
        {
            /*
            const int MaxOffset = 10;
            for (int ox = -MaxOffset; ox <= MaxOffset; ox++)
            {
                for (int oy = -MaxOffset; oy <= MaxOffset; oy++)
                {
                    if (Raylib.GetRandomValue(0, 3) != 0) break;

                    Vector2 otherPosition = Position + Vector2.UnitX * ox + Vector2.UnitY * oy;
                    if (!world.IsInBounds(otherPosition)) continue;

                    world.TryHitTileAt(otherPosition);
                }
            }
            */

            float x = Random.Shared.NextSingle() - 0.5f;
            float y = Random.Shared.NextSingle() - 0.5f;
            Vector2 direction = Vector2.Normalize(new Vector2(x, y));

            RaycastHit hit = world.Raycast(Position, direction, 4);
            if (hit.IsHit)
            {
                world.TryHitTileAt(hit.Position);
                world.LightningEffects.Add(new LightningEffect(this, Position, hit.Position, 0.0f));
            }
        }

        public void Step(float dt, World world)
        {
            Vector2 currentDelta = Velocity * dt;

            Position.X = Raymath.Clamp(Position.X, 0.5f, World.Width - 0.5f);
            Position.Y = Raymath.Clamp(Position.Y, 0.5f, World.Height - 0.5f);

            Position.X += currentDelta.X;

            Position.X = Raymath.Clamp(Position.X, 0.5f, World.Width - 0.5f);
            if (world.HasTileAt(Position))
            {
                world.TryHitTileAt(Position);
                if (Type == BallType.POISON) world.PoisonTileAt(Position);

                if (!(Type == BallType.PIERCING && !world.HasTileAt(Position)))
                {
                    Direction.X *= -1.0f;
                    Position.X -= currentDelta.X;
                }
            }
            Position.X = Raymath.Clamp(Position.X, 0.5f, World.Width - 0.5f);

            Position.Y += currentDelta.Y;

            Position.Y = Raymath.Clamp(Position.Y, 0.5f, World.Height - 0.5f);
            if (world.HasTileAt(Position))
            {
                world.TryHitTileAt(Position);
                if (Type == BallType.POISON) world.PoisonTileAt(Position);

                if (!(Type == BallType.PIERCING && !world.HasTileAt(Position)))
                {
                    Direction.Y *= -1.0f;
                    Position.Y -= currentDelta.Y;
                }
            }
            Position.Y = Raymath.Clamp(Position.Y, 0.5f, World.Height - 0.5f);
        }

        public void Draw()
        {
            Color color = Color;
            Raylib.DrawCircleV(Position, 0.25f, color);
        }

        public static Color GetColor(BallType type)
        {
            switch (type)
            {
                case BallType.PIERCING:
                    return Color.Orange;
                case BallType.POISON:
                    return Color.Lime;
                case BallType.SPEED:
                    return Color.Magenta;
                case BallType.LIGHTNING:
                    return Color.Yellow;
                default:
                    return Color.Black;
            }
        }

        public static float GetSpeed(BallType type)
        {
            switch (type)
            {
                case BallType.SPEED:
                    return 50.0f;
                default:
                    return 10.0f;
            }
        }
    }
}
