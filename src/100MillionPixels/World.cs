using Raylib_cs;
using System.Numerics;
using System.Threading.Tasks;

namespace _100MillionPixels
{
    [Flags]
    public enum TileFlags : byte
    {
        None = 0,
        IsDestructible = 1 << 0,
        IsGoy = 1 << 1
    }

    public struct Tile
    {
        public byte Health;
        public byte Light;
        public TileFlags Flags;

        public bool IsDestructible
        {
            readonly get => (Flags & TileFlags.IsDestructible) != 0;
            set => Flags = value
                ? (Flags | TileFlags.IsDestructible)
                : (Flags & ~TileFlags.IsDestructible);
        }

        public bool IsGoy
        {
            readonly get => (Flags & TileFlags.IsGoy) != 0;
            set => Flags = value
                ? (Flags | TileFlags.IsGoy)
                : (Flags & ~TileFlags.IsGoy);
        }
    }

    public struct RaycastHit
    {
        public bool IsHit;
        public Vector2 TilePosition;
        public Vector2 Point;
        public Vector2 Normal;
        public float Distance;
    }

    public class LightningEffect
    {
        public const float Duration = 0.5f;

        public LightningEffect(Ball ball, Vector2 tilePosition, float lifetime)
        {
            Ball = ball;
            TilePosition = tilePosition;
            Lifetime = lifetime;
        }

        public Ball Ball;
        public Vector2 TilePosition;
        public float Lifetime;
    }

    public class World
    {
        public const int Width = 256;
        public const int Height = 256;

        public const int CenterX = Width / 2;
        public const int CenterY = Height / 2;

        public const int ArraySize = Width * Height;

        public const int GoyDistance = 3;

        public Tile[] Tiles = new Tile[ArraySize];

        public HashSet<Vector2> PoisonedTiles = new HashSet<Vector2>();

        public HashSet<Vector2> DirtyTiles = new HashSet<Vector2>();

        public HashSet<LightningEffect> LightningEffects = new HashSet<LightningEffect>();
        public HashSet<LightningEffect> LightningEffectsToDestroy = new HashSet<LightningEffect>();

        public const float PoisonTickInterval = 1.0f;
        public int PoisonDamagePerTick = 1;

        private float _poisonTimer = PoisonTickInterval;

        public event Action<int>? OnHit;

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

                    DirtyTiles.Add(position);
                }

                Tiles[i].IsGoy = false;

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
            Parallel.ForEach(DirtyTiles, position => {
                const int MaxOffset = 4;
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
            });
        }

        public void UpdateGoys()
        {
            Parallel.ForEach(DirtyTiles, position =>
            {
                const int MaxStartCheckOffset = GoyDistance;
                for (int ox = -MaxStartCheckOffset; ox <= MaxStartCheckOffset; ox++)
                {
                    for (int oy = -MaxStartCheckOffset; oy <= MaxStartCheckOffset; oy++)
                    {
                        Vector2 otherPosition = position + Vector2.UnitX * ox + Vector2.UnitY * oy;
                        if (!IsInBounds(otherPosition) || HasTileAt(otherPosition)) continue;

                        CalculateGoy(otherPosition);
                    }
                }
                int index = GetIndexFromPosition(position);
            });
        }

        public void CalculateGoy(Vector2 position)
        {
            const int MaxDistanceCheckOffset = GoyDistance;
            int minReached = int.MaxValue;
            Vector2[] offsets = {
                new Vector2( 0,  0),
                new Vector2(-1,  0), new Vector2( 0,  1), new Vector2( 0, -1), new Vector2( 1,  0),
                new Vector2(-2,  0), new Vector2(-1,  1), new Vector2(-1, -1), new Vector2( 0,  2),
                new Vector2( 0, -2), new Vector2( 1,  1), new Vector2( 1, -1), new Vector2( 2,  0),
            };

            foreach (Vector2 offset in offsets)
            {
                Vector2 otherPosition = position + Vector2.UnitX * offset.X + Vector2.UnitY * offset.Y;
                if (!IsInBounds(otherPosition) || !HasTileAt(otherPosition)) continue;

                int manhattanDistance = Math.Abs((int)offset.X) + Math.Abs((int)offset.Y) - 1;

                minReached = Math.Min(manhattanDistance, minReached);
            }
            int index = GetIndexFromPosition(position);

            Tiles[index].IsGoy = minReached >= MaxDistanceCheckOffset;
        }

        public void Tick(float dt)
        {
            UpdateLight();
            UpdateGoys();
            DirtyTiles.Clear();

            _poisonTimer += dt;

            while (_poisonTimer >= PoisonTickInterval)
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
            Vector2? positionToAdd = null;
            foreach (Vector2 position in PoisonedTiles)
            {
                TryHitTileAt(position, PoisonDamagePerTick);

                Vector2[] offsets = {
                    new Vector2( 0.0f, 1.0f),
                    new Vector2( 1.0f, 0.0f),
                    new Vector2( 0.0f,-1.0f),
                    new Vector2(-1.0f, 0.0f),
                };

                List<Vector2> availablePositions = new List<Vector2>();
                foreach (Vector2 offset in offsets)
                {
                    Vector2 offsetted = position + offset;
                    if (IsInBounds(offsetted) && HasTileAt(offsetted) && IsDestructibleAt(offsetted) && !PoisonedTiles.Contains(offsetted))
                    {
                        availablePositions.Add(offsetted);
                    }
                }

                if (availablePositions.Count != 0)
                {
                    int last = availablePositions.Count - 1;

                    int random = Random.Shared.Next(availablePositions.Count);

                    positionToAdd = availablePositions[random];
                }
            }

            if (positionToAdd.HasValue) PoisonedTiles.Add(positionToAdd.Value);
        }

        public void Draw(Rectangle viewRect, float fps)
        {
            Color[] healthColors = {
                Color.LightGray,
                Color.Green,
                Color.Red,
                Color.Blue,
                Color.Brown
            };

            for (int i = 0; i < ArraySize; i++)
            {
                Vector2 position = GetPositionFromIndex(i);

                if (position.X + 1 < viewRect.X || position.X > viewRect.X + viewRect.Width ||
                    position.Y + 1 < viewRect.Y || position.Y > viewRect.Y + viewRect.Height)
                    continue;

                if (Tiles[i].Health != 0 && Tiles[i].Light != 0)
                {
                    Color color = Tiles[i].IsDestructible ? healthColors[Tiles[i].Health - 1] : Color.White;
                    color.A = Tiles[i].Light;

                    Raylib.DrawRectangleV(position, Vector2.One, color);
                }

                /*
                if (Tiles[i].Health == 0 && Tiles[i].IsGoy)
                {
                    Raylib.DrawRectangleV(position, Vector2.One, new Color(0.0f, 1.0f, 1.0f, 0.25f));
                }
                */
            }

            if (fps < 30) return;

            Vector2 size = Vector2.One * _poisonTimer;
            Color poisonColor = new Color(0.0f, _poisonTimer * 0.5f, 0.0f, _poisonTimer);
            foreach (Vector2 position in PoisonedTiles)
            {
                if (PoisonedTiles.Contains(position))
                {
                    Vector2 center = position + Vector2.One * 0.5f;

                    Vector2 topLeft = center - size * Vector2.One * 0.5f;

                    Raylib.DrawRectangleV(topLeft, size, poisonColor);
                }
            }

            Vector2 tileRandomization = new Vector2(
                Random.Shared.NextSingle(),
                Random.Shared.NextSingle()
            );

            foreach (LightningEffect effect in LightningEffects)
            {
                Vector2 tilePosition = new Vector2((int)effect.TilePosition.X, (int)effect.TilePosition.Y);
                float distanceSquared = Vector2.DistanceSquared(tilePosition, effect.Ball.Position);

                float t = 1.0f - effect.Lifetime / LightningEffect.Duration;

                Vector2 tileCenterPosition = tilePosition + Vector2.One * 0.5f;
                Vector2 tileSize = Vector2.One * t;
                Vector2 tileTopLeft = tileCenterPosition - tileSize * Vector2.One * 0.5f;

                Color lightningColor = Color.Yellow;
                Color tileColor = new Color(lightningColor.R, lightningColor.G, lightningColor.B, t);

                Raylib.DrawRectangleV(tileTopLeft, tileSize, tileColor);

                Vector2 tilePositionRandomized = tilePosition + tileRandomization;

                Raylib.DrawLineV(effect.Ball.Position, tilePositionRandomized, lightningColor);
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

            // старт внутри тайла — сразу считаем попадание в точке запуска
            if (IsInBounds(mapX, mapY) && HasTileAt(mapX, mapY))
            {
                hit.IsHit = true;
                hit.TilePosition = new Vector2(mapX, mapY);
                hit.Point = origin;          // точка запуска
                hit.Distance = 0f;
                hit.Normal = Vector2.Zero;      // грани нет — нормаль не определена
                return hit;
            }

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

                    hit.TilePosition = new Vector2(mapX, mapY);
                    // позиция попадания луча, а не координаты тайла
                    hit.Point = origin + direction * distance;

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
                DirtyTiles.Add(position);
            }

            int healthDamaged = Tiles[index].Health - newHealth;
            OnHit?.Invoke(healthDamaged);

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
