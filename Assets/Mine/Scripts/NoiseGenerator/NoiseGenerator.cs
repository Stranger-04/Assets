using UnityEngine;

public static class NoiseGenerator
{
    public enum NoiseType
    {
        Perlin,
        Voronoi
    }

    // ── Shared utilities (used by noise implementations & sampling) ──────────

    public static float Repeat01(float value)
    {
        return value - Mathf.Floor(value);
    }

    public static float Lerp(float a, float b, float t) => a + (b - a) * t;

    private static float Seed01(float seed)
    {
        float value = Mathf.Sin(seed * 12.9898f + 78.233f) * 43758.5453f;
        return Repeat01(value);
    }

    // ── Texture generation ───────────────────────────────────────────────────

    public static Texture3D Generate3DTexture(int size, float scale, bool seamless, NoiseType noiseType)
    {
        var texture = new Texture3D(size, size, size, TextureFormat.RFloat, false)
        {
            wrapMode = TextureWrapMode.Repeat,
            filterMode = FilterMode.Bilinear
        };

        Color[] colors = new Color[size * size * size];
        for (int z = 0; z < size; z++)
        {
            float wz = seamless ? (float)z / size : Mathf.Min((float)z / size, 1f - 1f / size);
            for (int y = 0; y < size; y++)
            {
                float wy = seamless ? (float)y / size : Mathf.Min((float)y / size, 1f - 1f / size);
                for (int x = 0; x < size; x++)
                {
                    float wx = seamless ? (float)x / size : Mathf.Min((float)x / size, 1f - 1f / size);
                    float sample = Sample3D(wx, wy, wz, scale, size, seamless, noiseType);
                    colors[x + y * size + z * size * size] = new Color(sample, 0f, 0f, 1f);
                }
            }
        }

        if (seamless)
        {
            MakeSeamless3D(colors, size);
        }

        texture.SetPixels(colors);
        texture.Apply();
        return texture;
    }

    public static Texture2D GenerateChannelTexture(int resolution, float scale, bool seamless, NoiseType noiseType, float randomSeed)
    {
        int res = Mathf.Clamp(resolution, 8, 2048);
        float sliceY = Seed01(randomSeed);

        var texture = new Texture2D(res, res, TextureFormat.RGBA32, false)
        {
            wrapMode = TextureWrapMode.Repeat,
            filterMode = FilterMode.Bilinear
        };

        Color[] colors = new Color[res * res];
        for (int y = 0; y < res; y++)
        {
            float wz = (float)y / res;
            for (int x = 0; x < res; x++)
            {
                float wx = (float)x / res;
                float sample = Sample3D(wx, sliceY, wz, scale, res, seamless, noiseType);
                colors[x + y * res] = new Color(sample, sample, sample, 1f);
            }
        }

        if (seamless)
        {
            MakeSeamless2D(colors, res);
        }

        texture.SetPixels(colors);
        texture.Apply();
        return texture;
    }

    public static Texture2D Generate2DSliceTexture(
        int resolution,
        int channels,
        float sliceStart,
        float sliceDistance,
        float scale,
        int period,
        bool seamless,
        NoiseType noiseType)
    {
        int res = Mathf.Clamp(resolution, 8, 2048);
        int ch = Mathf.Clamp(channels, 1, 4);

        var texture = new Texture2D(res, res, TextureFormat.RGBA32, false)
        {
            wrapMode = TextureWrapMode.Repeat,
            filterMode = FilterMode.Bilinear
        };

        Color[] colors = new Color[res * res];
        for (int y = 0; y < res; y++)
        {
            float wz = (float)y / res;
            for (int x = 0; x < res; x++)
            {
                float wx = (float)x / res;
                Color c = new Color(0f, 0f, 0f, 1f);

                for (int channel = 0; channel < ch; channel++)
                {
                    float wy = sliceStart + channel * sliceDistance;
                    wy = seamless ? Repeat01(wy) : Mathf.Clamp01(wy);
                    c[channel] = Sample3D(wx, wy, wz, scale, period, seamless, noiseType);
                }

                colors[x + y * res] = c;
            }
        }

        if (seamless)
        {
            MakeSeamless2D(colors, res);
        }

        texture.SetPixels(colors);
        texture.Apply();
        return texture;
    }

    // ── Seamless helpers ─────────────────────────────────────────────────────

    private static void MakeSeamless3D(Color[] colors, int size)
    {
        if (size <= 1) return;

        Color[] source = (Color[])colors.Clone();
        for (int z = 0; z < size; z++)
        {
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    if (x == size - 1 || y == size - 1 || z == size - 1)
                    {
                        int sx = x == size - 1 ? 0 : x;
                        int sy = y == size - 1 ? 0 : y;
                        int sz = z == size - 1 ? 0 : z;
                        colors[x + y * size + z * size * size] = source[sx + sy * size + sz * size * size];
                    }
                }
            }
        }
    }

    private static void MakeSeamless2D(Color[] colors, int size)
    {
        if (size <= 1) return;

        Color[] source = (Color[])colors.Clone();
        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                if (x == size - 1 || y == size - 1)
                {
                    int sx = x == size - 1 ? 0 : x;
                    int sy = y == size - 1 ? 0 : y;
                    colors[x + y * size] = source[sx + sy * size];
                }
            }
        }
    }

    // ── Sampling entry points ────────────────────────────────────────────────

    public static float Sample3D(float x, float y, float z, float scale, int period, bool seamless, NoiseType noiseType)
    {
        if (seamless)
        {
            return SampleTileable3D(x, y, z, scale, noiseType);
        }

        switch (noiseType)
        {
            case NoiseType.Voronoi:
                return VoronoiNoise.Sample(x, y, z, scale);
            default:
                return PerlinNoise.Sample(x, y, z, scale);
        }
    }

    private static float SampleTileable3D(float x, float y, float z, float scale, NoiseType noiseType)
    {
        x = Repeat01(x);
        y = Repeat01(y);
        z = Repeat01(z);

        float n000 = SampleBaseNoise(x, y, z, scale, noiseType);
        float n100 = SampleBaseNoise(x - 1f, y, z, scale, noiseType);
        float n010 = SampleBaseNoise(x, y - 1f, z, scale, noiseType);
        float n110 = SampleBaseNoise(x - 1f, y - 1f, z, scale, noiseType);
        float n001 = SampleBaseNoise(x, y, z - 1f, scale, noiseType);
        float n101 = SampleBaseNoise(x - 1f, y, z - 1f, scale, noiseType);
        float n011 = SampleBaseNoise(x, y - 1f, z - 1f, scale, noiseType);
        float n111 = SampleBaseNoise(x - 1f, y - 1f, z - 1f, scale, noiseType);

        float nx00 = Lerp(n000, n100, x);
        float nx10 = Lerp(n010, n110, x);
        float nx01 = Lerp(n001, n101, x);
        float nx11 = Lerp(n011, n111, x);

        float nxy0 = Lerp(nx00, nx10, y);
        float nxy1 = Lerp(nx01, nx11, y);

        return Lerp(nxy0, nxy1, z);
    }

    private static float SampleBaseNoise(float x, float y, float z, float scale, NoiseType noiseType)
    {
        switch (noiseType)
        {
            case NoiseType.Voronoi:
                return VoronoiNoise.Sample(x, y, z, scale);
            default:
                return PerlinNoise.Sample(x, y, z, scale);
        }
    }
}
