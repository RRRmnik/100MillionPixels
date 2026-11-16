using Raylib_cs;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Text;
using System.Threading.Tasks;

namespace _100MillionPixels
{
    public struct Tile
    {
        public byte Health;
        public bool IsDestructible;
        public byte Light;
    }

    public struct RaycastHit
    {
        public bool IsHit;
        public Vector2 Position;
        public Vector2 Normal;
        public float Distance;
    }

    public class LightningEffect
    {
        public const float Duration = 0.5f;

        public LightningEffect(Ball ball, Vector2 startPosition, Vector2 endPosition, float lifetime)
        {
            Ball = ball;
            StartPosition = startPosition;
            EndPosition = endPosition;
            Lifetime = lifetime;
        }

        public Ball Ball;
        public Vector2 StartPosition;
        public Vector2 EndPosition;
        public float Lifetime;
    }

    public class World
    {
        public const int Width = 1024;
        public const int Height = 1024;

        public const int CenterX = Width / 2;
        public const int CenterY = Height / 2;

        public const int ArraySize = Width * Height;

        public Tile[] Tiles = new Tile[ArraySize];

        public List<Vector2> PoisonedTiles = new List<Vector2>();

        public List<Vector2> TilesToLightUp = new List<Vector2>();

        public List<LightningEffect> LightningEffects = new List<LightningEffect>();
        public List<LightningEffect> LightningEffectsToDestroy = new List<LightningEffect>();

        public const float PoisonTickInterval = 1.0f;
        public int PoisonDamagePerTick = 1;

        private float _poisonTimer = PoisonTickInterval;

        public void Generate()
        {
            for (int i = 0; i < ArraySize; i++)
            {
                Vector2 position = GetPositionFromIndex(i);
                int x = (int)position.X;
                int y = (int)position.Y;

                float distance = Vector2.Distance(position, new Vector2(CenterX, CenterY));
                if (distance > 6.5f)
                {
                    //HealthPoints[i] = Raylib.GetRandomValue(1, 3);
                    float value = PerlinNoise.FBM01(position * 0.05f, 4);
                    value = MathF.Pow(value, 2.0f);
                    Tiles[i].Health = (byte)Math.Clamp((int)(value * 8.0f), 1, 5);
                    Tiles[i].IsDestructible = true;

                    if (x == 0 || y == 0 || x == Width - 1 || y == Height - 1)
                    {
                        Tiles[i].IsDestructible = false;
                    }
                }
                else
                {
                    Tiles[i].Health = 0;

                    TilesToLightUp.Add(position);
                }

                /*
                if (x % 32 == 0 && (y < 7 || y > 9))
                {
                    Tiles[i].Health = 1;
                    Tiles[i].IsDestructible = false;
                }
                */

                /*
                int modX = x % 9;
                int modY = y % 9;

                bool[] pattern = {
                    false, false, false, false, false, false, false, false, false,
                    false, false, false, false, false, false, false, false, false,
                    false, false, true , false, false, true , true , true , true ,
                    false, false, true , false, false, true , false, false, false,
                    false, false, true , false, false, true , false, false, false,
                    false, false, true , true , true , true , true , true , true ,
                    false, false, false, false, false, true , false, false, false,
                    false, false, false, false, false, true , false, false, false,
                    false, false, true , true , true , true , false, false, false,
                };

                bool result = pattern[modY * 9 + modX];

                if (result) Tiles[i].IsDestructible = false;
                */
            }
        }

        public void UpdateLight()
        {
            foreach (Vector2 position in TilesToLightUp)
            {
                const int MaxOffset = 5;
                const int MaxManhattanDistance = MaxOffset * 2;
                for (int ox = -MaxOffset; ox <= MaxOffset; ox++)
                {
                    for (int oy = -MaxOffset; oy <= MaxOffset; oy++)
                    {
                        Vector2 otherPosition = position + Vector2.UnitX * ox + Vector2.UnitY * oy;
                        if (!IsInBounds(otherPosition)) continue;

                        int manhattanDistance = Math.Abs(ox) + Math.Abs(oy) - 1;

                        float light01 = 0.5f - (float)manhattanDistance / MaxManhattanDistance;
                        light01 = Raymath.Clamp01(light01 * 2.0f);

                        byte light = (byte)(light01 * 255);

                        int index = GetIndexFromPosition(otherPosition);
                        Tiles[index].Light = Math.Max(light, Tiles[index].Light);
                    }
                }
            }

            TilesToLightUp.Clear();
        }

        public void Tick(float dt)
        {
            UpdateLight();

            _poisonTimer += dt;

            if (_poisonTimer >= PoisonTickInterval)
            {
                _poisonTimer -= PoisonTickInterval;
                TickPoison();
            }

            foreach (LightningEffect effect in LightningEffects)
            {
                effect.Lifetime += dt;
                if (effect.Lifetime > LightningEffect.Duration)
                {
                    LightningEffectsToDestroy.Add(effect);
                }
            }

            foreach (LightningEffect effectToDestroy in LightningEffectsToDestroy)
            {
                LightningEffects.Remove(effectToDestroy);
            }
        }

        public void TickPoison()
        {
            for (int i = PoisonedTiles.Count - 1; i >= 0; i--)
            {
                TryHitTileAt(PoisonedTiles[i], PoisonDamagePerTick);
            }
        }

        public void Draw(Rectangle viewRect)
        {
            for (int i = 0; i < ArraySize; i++)
            {
                Vector2 position = GetPositionFromIndex(i);

                if (position.X + 1 < viewRect.X || position.X > viewRect.X + viewRect.Width ||
                    position.Y + 1 < viewRect.Y || position.Y > viewRect.Y + viewRect.Height)
                    continue;

                if (Tiles[i].Health != 0 && Tiles[i].Light != 0)
                {
                    Color[] healthColors = {
                        Color.LightGray,
                        Color.Green,
                        Color.Red,
                        Color.Blue,
                        Color.Brown
                    };
                    Color color = Tiles[i].IsDestructible ? healthColors[Tiles[i].Health - 1] : Color.White;
                    color.A = Tiles[i].Light;

                    Raylib.DrawRectangleV(position, Vector2.One, color);

                    if (PoisonedTiles.Contains(position))
                    {
                        Vector2 center = position + Vector2.One * 0.5f;

                        float time = (float)Raylib.GetTime();
                        float repeat = Raymath.Repeat(time, 1.0f);
                        Vector2 size = Vector2.One * repeat;

                        Vector2 topLeft = center - size * Vector2.One * 0.5f;

                        Color poisonColor = new Color(0.0f, repeat, 0.0f, 1.0f - repeat);
                        Raylib.DrawRectangleV(topLeft, size, poisonColor);
                    }
                }
            }

            foreach (LightningEffect effect in LightningEffects)
            {
                Vector2 tilePosition = new Vector2((int)effect.EndPosition.X, (int)effect.EndPosition.Y);
                Vector2 tileCenterPosition = tilePosition + Vector2.One * 0.5f;
                Vector2 tilePositionRandomized = tilePosition + new Vector2(
                    Random.Shared.NextSingle(),
                    Random.Shared.NextSingle()
                );

                Raylib.DrawLineV(effect.Ball.Position, tilePositionRandomized, Color.Yellow);
            }
        }

        // завайбкожено
        public RaycastHit Raycast(Vector2 origin, Vector2 direction, float maxDistance = 2048f)
        {
            RaycastHit hit = new RaycastHit();

            // нулевое направление — сразу выход
            if (direction.X == 0f && direction.Y == 0f) return hit;

            direction = Vector2.Normalize(direction);

            // тайл, в котором стартуем
            int mapX = (int)MathF.Floor(origin.X);
            int mapY = (int)MathF.Floor(origin.Y);

            // расстояние по параметру t между соседними линиями сетки по каждой оси
            // (direction нормализован => t == реальная дистанция)
            float deltaDistX = direction.X == 0f ? float.MaxValue : MathF.Abs(1f / direction.X);
            float deltaDistY = direction.Y == 0f ? float.MaxValue : MathF.Abs(1f / direction.Y);

            int stepX, stepY;
            float sideDistX, sideDistY;

            if (direction.X < 0f)
            {
                stepX = -1;
                sideDistX = (origin.X - mapX) * deltaDistX;
            }
            else
            {
                stepX = 1;
                sideDistX = (mapX + 1f - origin.X) * deltaDistX;
            }

            if (direction.Y < 0f)
            {
                stepY = -1;
                sideDistY = (origin.Y - mapY) * deltaDistY;
            }
            else
            {
                stepY = 1;
                sideDistY = (mapY + 1f - origin.Y) * deltaDistY;
            }

            int side = 0;      // 0 = пересекли вертикальную грань (ось X), 1 = горизонтальную (ось Y)
            float distance = 0f;
            int steps = 0;
            int maxSteps = Width + Height; // защита от бесконечного цикла

            while (steps++ < maxSteps * 2)
            {
                // шагаем к ближайшей границе сетки
                if (sideDistX < sideDistY)
                {
                    distance = sideDistX;
                    sideDistX += deltaDistX;
                    mapX += stepX;
                    side = 0;
                }
                else
                {
                    distance = sideDistY;
                    sideDistY += deltaDistY;
                    mapY += stepY;
                    side = 1;
                }

                if (distance > maxDistance) break;
                if (!IsInBounds(mapX, mapY)) break;

                if (HasTileAt(mapX, mapY))
                {
                    hit.IsHit = true;
                    hit.Position = new Vector2(mapX, mapY);
                    hit.Distance = distance;

                    hit.Normal = side == 0
                        ? new Vector2(stepX > 0 ? -1f : 1f, 0f)
                        : new Vector2(0f, stepY > 0 ? -1f : 1f);

                    return hit;
                }
            }

            return hit;
        }

        public void PoisonTileAt(int x, int y)
        {
            if (!HasTileAt(x, y) || !IsDestructibleAt(x, y)) return;

            Vector2 position = new Vector2(x, y);

            if (!PoisonedTiles.Contains(position))
            {
                PoisonedTiles.Add(position);
            }
        }

        public static async Task DelayedAction(Action action, int delayMilliseconds)
        {
            await Task.Delay(delayMilliseconds);
            action?.Invoke();
        }

        public void PoisonTileAt(Vector2 position) => PoisonTileAt((int)position.X, (int)position.Y);

        public void TryHitTileAt(int x, int y, int damage = 1)
        {
            if (!IsDestructibleAt(x, y)) return;

            HitTileAt(x, y, damage);
        }
        public void TryHitTileAt(Vector2 position, int damage = 1) => TryHitTileAt((int)position.X, (int)position.Y, damage);

        public void HitTileAt(int x, int y, int damage = 1)
        {
            int index = GetIndexFromPosition(x, y);
            int newHealth = Tiles[index].Health - damage;
            if (newHealth < 0) newHealth = 0;

            if (newHealth == 0)
            {
                Vector2 position = new Vector2(x, y);
                PoisonedTiles.Remove(position);
                TilesToLightUp.Add(position);
            }

            Tiles[index].Health = (byte)newHealth;
        }
        public void HitTileAt(Vector2 position) => HitTileAt((int)position.X, (int)position.Y);

        public bool HasTileAt(int x, int y)
        {
            int i = GetIndexFromPosition(x, y);
            return Tiles[i].Health != 0;
        }
        public bool HasTileAt(Vector2 position) => HasTileAt((int)position.X, (int)position.Y);

        public bool IsDestructibleAt(int x, int y)
        {
            int i = GetIndexFromPosition(x, y);
            return Tiles[i].IsDestructible;
        }
        public bool IsDestructibleAt(Vector2 position) => IsDestructibleAt((int)position.X, (int)position.Y);

        public Vector2 GetPositionFromIndex(int index)
        {
            return new Vector2(
                index % Width,
                index / Width
            );
        }

        public int GetIndexFromPosition(int x, int y)
        {
            return y * Width + x;
        }
        public int GetIndexFromPosition(Vector2 position) => GetIndexFromPosition((int)position.X, (int)position.Y);

        public bool IsInBounds(int x, int y)
        {
            return x >= 0 && y >= 0 && x < Width && y < Height;
        }
        public bool IsInBounds(Vector2 position) => IsInBounds((int)position.X, (int)position.Y);
    }
}
