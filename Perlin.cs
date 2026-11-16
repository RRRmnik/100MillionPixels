using Raylib_cs;
using System;
using System.Numerics;

public static class PerlinNoise
{
    private static readonly int[] Perm = new int[512];

    static PerlinNoise()
    {
        Reseed(1337);
    }

    public static void Reseed(int seed)
    {
        var p = new int[256];
        for (int i = 0; i < 256; i++) p[i] = i;

        var rng = new Random(seed);
        for (int i = 255; i > 0; i--)
        {
            int j = rng.Next(i + 1);
            (p[i], p[j]) = (p[j], p[i]);
        }

        for (int i = 0; i < 512; i++)
            Perm[i] = p[i & 255];
    }

    private static float Fade(float t) => t * t * t * (t * (t * 6f - 15f) + 10f);

    private static float Lerp(float a, float b, float t) => a + t * (b - a);

    private static float Grad(int hash, float x, float y)
    {
        return (hash & 7) switch
        {
            0 => x + y,
            1 => x - y,
            2 => -x + y,
            3 => -x - y,
            4 => x,
            5 => -x,
            6 => y,
            _ => -y,
        };
    }

    public static float Noise(Vector2 point)
    {
        float x = point.X;
        float y = point.Y;

        int xi = (int)MathF.Floor(x) & 255;
        int yi = (int)MathF.Floor(y) & 255;
        float xf = x - MathF.Floor(x);
        float yf = y - MathF.Floor(y);

        float u = Fade(xf);
        float v = Fade(yf);

        int aa = Perm[Perm[xi] + yi];
        int ab = Perm[Perm[xi] + yi + 1];
        int ba = Perm[Perm[xi + 1] + yi];
        int bb = Perm[Perm[xi + 1] + yi + 1];

        float x1 = Lerp(Grad(aa, xf, yf),
                        Grad(ba, xf - 1, yf), u);
        float x2 = Lerp(Grad(ab, xf, yf - 1),
                        Grad(bb, xf - 1, yf - 1), u);

        return Lerp(x1, x2, v);
    }

    public static float FBM(
        Vector2 point,
        int octaves = 5,
        float lacunarity = 2f,
        float gain = 0.5f,
        float frequency = 1f,
        float amplitude = 1f)
    {
        float sum = 0f;
        float norm = 0f;

        for (int i = 0; i < octaves; i++)
        {
            sum += amplitude * Noise(point * frequency);
            norm += amplitude;
            frequency *= lacunarity;
            amplitude *= gain;
        }

        return norm > 0f ? sum / norm : 0f;
    }

    public static float FBM01(Vector2 point, int octaves = 4, float lacunarity = 2f, float gain = 0.5f)
    {
        return FBM(point, octaves, lacunarity, gain) * 0.5f + 0.5f;
    }
}