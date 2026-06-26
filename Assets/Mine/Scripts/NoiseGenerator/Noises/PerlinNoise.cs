using UnityEngine;

/// <summary>
/// Perlin noise implementation. Provides 3D Perlin noise with optional periodic tiling.
/// Call via NoiseGenerator — not intended for direct use.
/// </summary>
public static class PerlinNoise
{
    // Standard permutation table (256 values duplicated to 512 for easier index wrapping)
    private static readonly int[] perm = new int[512]
    {
        151,160,137,91,90,15,131,13,201,95,96,53,194,233,7,225,140,36,103,30,69,142,8,99,37,240,21,10,23,
        190, 6,148,247,120,234,75,0,26,197,62,94,252,219,203,117,35,11,32,57,177,33,88,237,149,56,87,174,
        20,125,136,171,168, 68,175,74,165,71,134,139,48,27,166,77,146,158,231,83,111,229,122,60,211,133,
        230,220,105,92,41,55,46,245,40,244,102,143,54, 65,25,63,161, 1,216,80,73,209,76,132,187,208, 89,
        18,169,200,196,135,130,116,188,159,86,164,100,109,198,173,186, 3,64,52,217,226,250,124,123,5,202,
        38,147,118,126,255,82,85,212,207,206,59,227,47,16,58,17,182,189,28,42,223,183,170,213,119,248,
        152, 2,44,154,163, 70,221,153,101,155,167, 43,172,9,129,22,39,253, 19,98,108,110,79,113,224,232,
        178,185,112,104,218,246,97,228,251,34,242,193,238,210,144,12,191,179,162,241, 81,51,145,235,249,
        14,239,107,49,192,214, 31,181,199,106,157,184, 84,204,176,115,121,50,45,127,  4,150,254,138,236,
        205, 93,222,114, 67,29,24,72,243,141,128,195,78,66,215,61,156,180,
        151,160,137,91,90,15,131,13,201,95,96,53,194,233,7,225,140,36,103,30,69,142,8,99,37,240,21,10,23,
        190, 6,148,247,120,234,75,0,26,197,62,94,252,219,203,117,35,11,32,57,177,33,88,237,149,56,87,174,
        20,125,136,171,168, 68,175,74,165,71,134,139,48,27,166,77,146,158,231,83,111,229,122,60,211,133,
        230,220,105,92,41,55,46,245,40,244,102,143,54, 65,25,63,161, 1,216,80,73,209,76,132,187,208, 89,
        18,169,200,196,135,130,116,188,159,86,164,100,109,198,173,186, 3,64,52,217,226,250,124,123,5,202,
        38,147,118,126,255,82,85,212,207,206,59,227,47,16,58,17,182,189,28,42,223,183,170,213,119,248,
        152, 2,44,154,163, 70,221,153,101,155,167, 43,172,9,129,22,39,253, 19,98,108,110,79,113,224,232,
        178,185,112,104,218,246,97,228,251,34,242,193,238,210,144,12,191,179,162,241, 81,51,145,235,249,
        14,239,107,49,192,214, 31,181,199,106,157,184, 84,204,176,115,121,50,45,127,  4,150,254,138,236,
        205, 93,222,114, 67,29,24,72,243,141,128,195,78,66,215,61,156,180
    };

    private static float Fade(float t) => t * t * t * (t * (t * 6f - 15f) + 10f);

    private static float Grad(int hash, float x, float y, float z)
    {
        int h = hash & 15;
        float u = h < 8 ? x : y;
        float v = h < 4 ? y : (h == 12 || h == 14 ? x : z);
        return ((h & 1) == 0 ? u : -u) + ((h & 2) == 0 ? v : -v);
    }

    /// <summary>3D Perlin noise. Returns a value in [0, 1].</summary>
    public static float Sample(float x, float y, float z, float scale)
    {
        x *= scale;
        y *= scale;
        z *= scale;

        int X = Mathf.FloorToInt(x) & 255;
        int Y = Mathf.FloorToInt(y) & 255;
        int Z = Mathf.FloorToInt(z) & 255;
        float xf = x - Mathf.Floor(x);
        float yf = y - Mathf.Floor(y);
        float zf = z - Mathf.Floor(z);
        float u = Fade(xf);
        float v = Fade(yf);
        float w = Fade(zf);

        int A = perm[X] + Y;
        int AA = perm[A] + Z;
        int AB = perm[A + 1] + Z;
        int B = perm[X + 1] + Y;
        int BA = perm[B] + Z;
        int BB = perm[B + 1] + Z;

        float x1 = NoiseGenerator.Lerp(Grad(perm[AA], xf, yf, zf), Grad(perm[BA], xf - 1, yf, zf), u);
        float x2 = NoiseGenerator.Lerp(Grad(perm[AB], xf, yf - 1, zf), Grad(perm[BB], xf - 1, yf - 1, zf), u);
        float y1 = NoiseGenerator.Lerp(x1, x2, v);
        float x3 = NoiseGenerator.Lerp(Grad(perm[AA + 1], xf, yf, zf - 1), Grad(perm[BA + 1], xf - 1, yf, zf - 1), u);
        float x4 = NoiseGenerator.Lerp(Grad(perm[AB + 1], xf, yf - 1, zf - 1), Grad(perm[BB + 1], xf - 1, yf - 1, zf - 1), u);
        float y2 = NoiseGenerator.Lerp(x3, x4, v);

        return NoiseGenerator.Lerp(y1, y2, w) * 0.5f + 0.5f;
    }

    /// <summary>Periodic (tileable) 3D Perlin noise. Repeat period in each axis.</summary>
    public static float SamplePeriodic(float x, float y, float z, float scale, int period)
    {
        x *= scale;
        y *= scale;
        z *= scale;

        int p = Mathf.Max(1, period);

        float xw = NoiseGenerator.Repeat01(x / p) * p;
        float yw = NoiseGenerator.Repeat01(y / p) * p;
        float zw = NoiseGenerator.Repeat01(z / p) * p;

        int X0 = Mathf.FloorToInt(xw) % p;
        int Y0 = Mathf.FloorToInt(yw) % p;
        int Z0 = Mathf.FloorToInt(zw) % p;

        float xf = xw - Mathf.Floor(xw);
        float yf = yw - Mathf.Floor(yw);
        float zf = zw - Mathf.Floor(zw);
        float u = Fade(xf);
        float v = Fade(yf);
        float w = Fade(zf);

        int X1 = (X0 + 1) % p;
        int Y1 = (Y0 + 1) % p;
        int Z1 = (Z0 + 1) % p;

        int AA = perm[(perm[(perm[X0] + Y0) % 256] + Z0) % 256];
        int AB = perm[(perm[(perm[X0] + Y0) % 256] + Z1) % 256];
        int BA = perm[(perm[(perm[X1] + Y0) % 256] + Z0) % 256];
        int BB = perm[(perm[(perm[X1] + Y0) % 256] + Z1) % 256];
        int AA1 = perm[(perm[(perm[X0] + Y1) % 256] + Z0) % 256];
        int AB1 = perm[(perm[(perm[X0] + Y1) % 256] + Z1) % 256];
        int BA1 = perm[(perm[(perm[X1] + Y1) % 256] + Z0) % 256];
        int BB1 = perm[(perm[(perm[X1] + Y1) % 256] + Z1) % 256];

        float x1 = NoiseGenerator.Lerp(Grad(AA, xf, yf, zf), Grad(BA, xf - 1, yf, zf), u);
        float x2 = NoiseGenerator.Lerp(Grad(AB, xf, yf, zf - 1), Grad(BB, xf - 1, yf, zf - 1), u);
        float y1 = NoiseGenerator.Lerp(x1, x2, w);
        float x3 = NoiseGenerator.Lerp(Grad(AA1, xf, yf - 1, zf), Grad(BA1, xf - 1, yf - 1, zf), u);
        float x4 = NoiseGenerator.Lerp(Grad(AB1, xf, yf - 1, zf - 1), Grad(BB1, xf - 1, yf - 1, zf - 1), u);
        float y2 = NoiseGenerator.Lerp(x3, x4, w);

        return NoiseGenerator.Lerp(y1, y2, v) * 0.5f + 0.5f;
    }
}
