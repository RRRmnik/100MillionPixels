using Raylib_cs;
using System.Diagnostics;
using System.Numerics;

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

        public const float LightningInterval = 0.075f;
        public const float LightningRadius = 6.0f;

        public BallType Type;

        public Vector2 Position = new Vector2(0.0f, 0.0f);
        public Vector2 Direction = new Vector2(0.0f, 0.0f);
        public Vector2 Velocity => Direction * Speed;

        public float Speed => GetSpeed(Type);
        public Color Color => GetColor(Type);

        private float _lightningTimer = 0.0f;

        private float _accumulated = 0.0f;

        public Ball(BallType type, Vector2 position, Vector2 direction)
        {
            Type = type;
            Position = position;
            Direction = Vector2.Normalize(direction);
        }

        public void Tick(float dt, float timeScale, World world)
        {
            if (!world.IsInBounds(Position)) { return; }

            RaycastHit hit = world.Raycast(Position, Direction, Speed * dt * 1.0005f + _accumulated);
            if (hit.IsHit)
            {
                world.TryHitTileAt(hit.TilePosition, 1);
                if (Type == BallType.POISON) world.PoisonTileAt(hit.TilePosition);

                if (!(Type == BallType.PIERCING && !world.HasTileAt(hit.TilePosition)))
                {
                    Position = hit.Point + hit.Normal * 0.01f;
                    Direction = Vector2.Reflect(Direction, hit.Normal);

                    _accumulated = MathF.Max(0.0f, Speed * dt - hit.Distance);
                }
                else
                {
                    Position = hit.Point + Direction * 0.01f;
                    _accumulated = MathF.Max(0.0f, _accumulated + Speed * dt - hit.Distance - 0.01f);
                }
            }
            else
            {
                Position += Velocity * dt + Direction * _accumulated;
                _accumulated = 0.0f;
            }

            int index = world.GetIndexFromPosition(Position);
            if (!world.Tiles[index].IsGoy)
            {
                int Steps = (int)Math.Ceiling(60.0f * timeScale * Velocity.Length());
                Steps = Math.Clamp(Steps, 1, 3000);
                float stepDt = dt / Steps;

                for (int i = 0; i < Steps; i++) Step(stepDt, world);
            }
        }

        public void Step(float dt, World world)
        {
            if (LightningTimerStep(dt))
            {
                Lightning(world);
            }
        }

        public bool LightningTimerStep(float dt)
        {
            if (Type == BallType.LIGHTNING)
            {
                _lightningTimer -= dt;
                if (_lightningTimer <= 0.0f)
                {
                    _lightningTimer += LightningInterval;
                    return true;
                }
            }
            return false;
        }

        public void Lightning(World world)
        {
            float x = Random.Shared.NextSingle() - 0.5f;
            float y = Random.Shared.NextSingle() - 0.5f;
            Vector2 direction = Vector2.Normalize(new Vector2(x, y));

            RaycastHit hit = world.Raycast(Position, direction, LightningRadius);
            if (hit.IsHit && world.IsDestructibleAt(hit.TilePosition))
            {
                world.TryHitTileAt(hit.TilePosition);
                world.LightningEffects.Add(new LightningEffect(this, hit.TilePosition, 0.0f));
            }
        }

        /*
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
        */

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
