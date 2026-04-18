using UnityEngine;

public static class NoiseGenerator
{
	public enum NoiseType
	{
		Perlin,
		Voronoi
	}

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
			// For seamless textures, sample [0, size) instead of [0, size]
			// to avoid discontinuity between the last and first slice
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

		texture.SetPixels(colors);
		texture.Apply();
		return texture;
	}

	public static float Sample3D(float x, float y, float z, float scale, int period, bool seamless, NoiseType noiseType)
	{
		switch (noiseType)
		{
			case NoiseType.Voronoi:
				return seamless ? Voronoi3DPeriodic(x, y, z, scale, period) : Voronoi3D(x, y, z, scale);
			default:
				return seamless ? Perlin3DPeriodic(x, y, z, scale, period) : Perlin3D(x, y, z, scale);
		}
	}

	private static float Repeat01(float value)
	{
		return value - Mathf.Floor(value);
	}

	private static int PositiveMod(int x, int m)
	{
		int r = x % m;
		return r < 0 ? r + m : r;
	}

	private static float Hash01(int x, int y, int z, int seed)
	{
		int h = x * 374761393 ^ y * 668265263 ^ z * 2147483647 ^ seed * 1274126177;
		h = (h ^ (h >> 13)) * 1274126177;
		h ^= (h >> 16);
		uint uh = (uint)h;
		return (uh & 0x00FFFFFF) / 16777215f;
	}

	private static Vector3 VoronoiFeaturePoint(int cx, int cy, int cz)
	{
		return new Vector3(
			Hash01(cx, cy, cz, 17),
			Hash01(cx, cy, cz, 31),
			Hash01(cx, cy, cz, 47)
		);
	}

	private static float Voronoi3D(float x, float y, float z, float s)
	{
		float px = x * s;
		float py = y * s;
		float pz = z * s;

		int ix = Mathf.FloorToInt(px);
		int iy = Mathf.FloorToInt(py);
		int iz = Mathf.FloorToInt(pz);

		float minDist = float.MaxValue;
		Vector3 p = new Vector3(px, py, pz);

		for (int dz = -1; dz <= 1; dz++)
		{
			for (int dy = -1; dy <= 1; dy++)
			{
				for (int dx = -1; dx <= 1; dx++)
				{
					int cx = ix + dx;
					int cy = iy + dy;
					int cz = iz + dz;
					Vector3 fp = VoronoiFeaturePoint(cx, cy, cz);
					Vector3 featurePos = new Vector3(cx + fp.x, cy + fp.y, cz + fp.z);
					float dist = Vector3.Distance(p, featurePos);
					if (dist < minDist) minDist = dist;
				}
			}
		}

		return 1f - Mathf.Clamp01(minDist);
	}

	private static float Voronoi3DPeriodic(float x, float y, float z, float s, int period)
	{
		int p = Mathf.Max(1, period);
		float px = x * s;
		float py = y * s;
		float pz = z * s;

		// Wrap coordinates to periodic domain [0, p)
		px = Repeat01(px / p) * p;
		py = Repeat01(py / p) * p;
		pz = Repeat01(pz / p) * p;

		int ix = Mathf.FloorToInt(px);
		int iy = Mathf.FloorToInt(py);
		int iz = Mathf.FloorToInt(pz);

		float minDist = float.MaxValue;
		Vector3 sample = new Vector3(px, py, pz);

		for (int dz = -1; dz <= 1; dz++)
		{
			for (int dy = -1; dy <= 1; dy++)
			{
				for (int dx = -1; dx <= 1; dx++)
				{
					int cx = PositiveMod(ix + dx, p);
					int cy = PositiveMod(iy + dy, p);
					int cz = PositiveMod(iz + dz, p);

					Vector3 fp = VoronoiFeaturePoint(cx, cy, cz);
					Vector3 featurePos = new Vector3(cx + fp.x, cy + fp.y, cz + fp.z);

					// Compute distance with periodic wrapping
					Vector3 delta = sample - featurePos;
					delta.x -= Mathf.Round(delta.x / p) * p;
					delta.y -= Mathf.Round(delta.y / p) * p;
					delta.z -= Mathf.Round(delta.z / p) * p;

					float dist = delta.magnitude;
					if (dist < minDist) minDist = dist;
				}
			}
		}

		return 1f - Mathf.Clamp01(minDist);
	}

	// Permutation table (standard 256 values duplicated)
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
	private static float Lerp(float a, float b, float t) => a + (b - a) * t;

	private static float Grad(int hash, float x, float y, float z)
	{
		int h = hash & 15;
		float u = h < 8 ? x : y;
		float v = h < 4 ? y : (h == 12 || h == 14 ? x : z);
		return ((h & 1) == 0 ? u : -u) + ((h & 2) == 0 ? v : -v);
	}

	private static float Perlin3D(float x, float y, float z, float s)
	{
		x *= s;
		y *= s;
		z *= s;

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

		float x1 = Lerp(Grad(perm[AA], xf, yf, zf), Grad(perm[BA], xf - 1, yf, zf), u);
		float x2 = Lerp(Grad(perm[AB], xf, yf - 1, zf), Grad(perm[BB], xf - 1, yf - 1, zf), u);
		float y1 = Lerp(x1, x2, v);
		float x3 = Lerp(Grad(perm[AA + 1], xf, yf, zf - 1), Grad(perm[BA + 1], xf - 1, yf, zf - 1), u);
		float x4 = Lerp(Grad(perm[AB + 1], xf, yf - 1, zf - 1), Grad(perm[BB + 1], xf - 1, yf - 1, zf - 1), u);
		float y2 = Lerp(x3, x4, v);

		return Lerp(y1, y2, w) * 0.5f + 0.5f;
	}

	private static float Perlin3DPeriodic(float x, float y, float z, float s, int period)
	{
		x *= s;
		y *= s;
		z *= s;

		int p = Mathf.Max(1, period);
		
		// Wrap to period while preserving fractional part
		float xw = Repeat01(x / p) * p;
		float yw = Repeat01(y / p) * p;
		float zw = Repeat01(z / p) * p;

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

		// Use modulo-based hash for periodic access
		int AA = perm[(perm[(perm[X0] + Y0) % 256] + Z0) % 256];
		int AB = perm[(perm[(perm[X0] + Y0) % 256] + Z1) % 256];
		int BA = perm[(perm[(perm[X1] + Y0) % 256] + Z0) % 256];
		int BB = perm[(perm[(perm[X1] + Y0) % 256] + Z1) % 256];
		int AA1 = perm[(perm[(perm[X0] + Y1) % 256] + Z0) % 256];
		int AB1 = perm[(perm[(perm[X0] + Y1) % 256] + Z1) % 256];
		int BA1 = perm[(perm[(perm[X1] + Y1) % 256] + Z0) % 256];
		int BB1 = perm[(perm[(perm[X1] + Y1) % 256] + Z1) % 256];

		float x1 = Lerp(Grad(AA, xf, yf, zf), Grad(BA, xf - 1, yf, zf), u);
		float x2 = Lerp(Grad(AB, xf, yf, zf - 1), Grad(BB, xf - 1, yf, zf - 1), u);
		float y1 = Lerp(x1, x2, w);
		float x3 = Lerp(Grad(AA1, xf, yf - 1, zf), Grad(BA1, xf - 1, yf - 1, zf), u);
		float x4 = Lerp(Grad(AB1, xf, yf - 1, zf - 1), Grad(BB1, xf - 1, yf - 1, zf - 1), u);
		float y2 = Lerp(x3, x4, w);

		return Lerp(y1, y2, v) * 0.5f + 0.5f;
	}
}
