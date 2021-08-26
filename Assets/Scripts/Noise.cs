using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public static class Noise {
    public enum NormalizeMode { Local, Global };

    public static float[,] GenerateNoiseMap(int mapWidth, int mapHeight, int seed, float scale, int octaves, float persistance, float lacunarity, Vector2 offset, NormalizeMode normalizeMode = Noise.NormalizeMode.Local) {
        float[,] noiseMap = new float[mapWidth, mapHeight];

        System.Random prng = new System.Random(seed);
        Vector2[] octaveOffsets = new Vector2[octaves];

        float maxPossibleHeight = 0;
        float amplitude = 1;
        float frequency = 1;

        for (int o = 0; o < octaves; o++) {
            float offsetX = prng.Next(-100000, 100000) - offset.x;
            float offsetY = prng.Next(-100000, 100000) - offset.y;
            octaveOffsets[o] = new Vector2(offsetX, offsetY);

            maxPossibleHeight += amplitude;
            amplitude *= persistance;
        }

        if (scale <= 0) scale = 0.0001f;

        float localMaxNoiseHeight = float.MinValue;
        float localMinNoiseHeight = float.MaxValue;

        float halfWidth = mapWidth / 2f;
        float halfHeight = mapHeight / 2f;

        for (int y = 0; y < mapHeight; y++) {
            for (int x = 0; x < mapWidth; x++) {
                amplitude = 1;
                frequency = 1;
                float noiseHeight = 0;

                for (int o = 0; o < octaves; o++) {
                    float sampleX = (x - halfWidth + octaveOffsets[o].x) / scale * frequency;
                    float sampleY = (y - halfHeight + octaveOffsets[o].y) / scale * frequency;
                    // move perlinvalue to range [-1, 1]
                    float perlinValue = Mathf.PerlinNoise(sampleX, sampleY) * 2 - 1;
                    noiseHeight += perlinValue * amplitude;

                    amplitude *= persistance;
                    frequency *= lacunarity;
                }
                if (noiseHeight > localMaxNoiseHeight) localMaxNoiseHeight = noiseHeight;
                if (noiseHeight < localMinNoiseHeight) localMinNoiseHeight = noiseHeight;
                noiseMap[x, y] = noiseHeight;
            }
        }
        for (int y = 0; y < mapHeight; y++) {
            for (int x = 0; x < mapWidth; x++) {
                // normalize
                switch (normalizeMode) {
                    case NormalizeMode.Local:
                        noiseMap[x, y] = Mathf.InverseLerp(localMinNoiseHeight, localMaxNoiseHeight, noiseMap[x, y]);
                        break;
                    case NormalizeMode.Global:
                        float normalizedHeight = (noiseMap[x, y] + 1) / (2f * maxPossibleHeight / 2f); // have to estimate normalized value
                        noiseMap[x, y] = Mathf.Clamp(normalizedHeight, 0, int.MaxValue);
                        break;
                }
            }
        }
        return noiseMap;
    }

}
