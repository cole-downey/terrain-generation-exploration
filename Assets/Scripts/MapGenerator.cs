using System;
using System.Threading;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class MapGenerator : MonoBehaviour {
    public enum DrawMode { NoiseMap, ColorMap, Mesh, NoiseMesh, FalloffMap };
    public enum ColorMode { Discrete, Gradient };
    public DrawMode drawMode;
    public ColorMode colorMode;

    public Noise.NormalizeMode noiseNormalizeMode;
    public bool useFalloff;

    public const int mapChunkSize = 241; // 240 is divisible by 2, 4, 8, 10, 12
    [Range(0, 6)]
    public int editorResolution;
    public float noiseScale;

    public int octaves;
    [Range(0, 1)]
    public float persistance;
    public float lacunarity;

    public int seed;
    public Vector2 offset;
    public int meshYScale = 10;
    public AnimationCurve meshHeightCurve;

    public bool autoUpdate;

    public TerrainType[] regions;
    float [,] falloffMap;

    Queue<MapThreadInfo<MapData>> mapDataThreadInfoQueue = new Queue<MapThreadInfo<MapData>>();
    Queue<MapThreadInfo<MeshData>> meshDataThreadInfoQueue = new Queue<MapThreadInfo<MeshData>>();

    void Awake() {
        falloffMap = FalloffGenerator.GenerateFalloffMap(mapChunkSize);
    }
    
    public void DrawMapInEditor() {
        MapData mapData = GenerateMapData(Vector2.zero);
        MapDisplay display = FindObjectOfType<MapDisplay>();
        if (drawMode == DrawMode.NoiseMap)
            display.DrawTexture(TextureGenerator.TextureFromHeightMap(mapData.heightMap), mapChunkSize, mapChunkSize);
        else if (drawMode == DrawMode.ColorMap)
            display.DrawTexture(TextureGenerator.TextureFromColorMap(mapData.colorMap, mapChunkSize, mapChunkSize), mapChunkSize, mapChunkSize);
        else if (drawMode == DrawMode.Mesh)
            display.DrawMesh(MeshGenerator.GenerateTerrainMesh(mapData.heightMap, meshYScale, meshHeightCurve, editorResolution), TextureGenerator.TextureFromColorMap(mapData.colorMap, mapChunkSize, mapChunkSize));
        else if (drawMode == DrawMode.NoiseMesh)
            display.DrawMesh(MeshGenerator.GenerateTerrainMesh(mapData.heightMap, meshYScale, meshHeightCurve, editorResolution), TextureGenerator.TextureFromHeightMap(mapData.heightMap));
        else if (drawMode == DrawMode.FalloffMap)
            display.DrawTexture(TextureGenerator.TextureFromHeightMap(FalloffGenerator.GenerateFalloffMap(mapChunkSize)), mapChunkSize, mapChunkSize);
    }

    public void RequestMapData(Action<MapData> callback, Vector2 center) {
        ThreadStart threadStart = delegate {
            MapDataThread(callback, center);
        };

        new Thread(threadStart).Start();
    }

    void MapDataThread(Action<MapData> callback, Vector2 center) {
        MapData mapData = GenerateMapData(center);
        lock (mapDataThreadInfoQueue) { // auto unlock
            mapDataThreadInfoQueue.Enqueue(new MapThreadInfo<MapData>(callback, mapData));
        }
    }

    public void RequestMeshData(Action<MeshData> callback, MapData mapData, int lod = 0) {
        ThreadStart threadStart = delegate {
            MeshDataThread(callback, mapData, lod);
        };
        new Thread(threadStart).Start();
    }

    void MeshDataThread(Action<MeshData> callback, MapData mapData, int lod) {
        MeshData meshData = MeshGenerator.GenerateTerrainMesh(mapData.heightMap, meshYScale, meshHeightCurve, lod);
        lock (meshDataThreadInfoQueue) {
            meshDataThreadInfoQueue.Enqueue(new MapThreadInfo<MeshData>(callback, meshData));
        }
    }

    void Update() {
        while (mapDataThreadInfoQueue.Count > 0) {
            MapThreadInfo<MapData> threadInfo;
            lock (mapDataThreadInfoQueue) {
                threadInfo = mapDataThreadInfoQueue.Dequeue();
            }
            threadInfo.callback(threadInfo.parameter);
        }
        while (meshDataThreadInfoQueue.Count > 0) {
            MapThreadInfo<MeshData> threadInfo;
            lock (meshDataThreadInfoQueue) {
                threadInfo = meshDataThreadInfoQueue.Dequeue();
            }
            threadInfo.callback(threadInfo.parameter);
        }
    }

    MapData GenerateMapData(Vector2 center) {
        float[,] noiseMap = Noise.GenerateNoiseMap(mapChunkSize, mapChunkSize, seed, noiseScale, octaves, persistance, lacunarity, center + offset, noiseNormalizeMode);

        Color[] colorMap = new Color[mapChunkSize * mapChunkSize];

        if(useFalloff) {
            for (int y = 0; y < mapChunkSize; y++) {
                for (int x = 0; x < mapChunkSize; x++) {
                    noiseMap[x, y] = Mathf.Clamp(noiseMap[x, y] - falloffMap[x, y], 0, int.MaxValue);
                }
            }
        }
        
        if (colorMode == ColorMode.Discrete) {
            for (int y = 0; y < mapChunkSize; y++) {
                for (int x = 0; x < mapChunkSize; x++) {
                    float currentHeight = noiseMap[x, y];
                    for (int r = 0; r < regions.Length; r++) {
                        if (currentHeight >= regions[r].height) {
                            colorMap[y * mapChunkSize + x] = regions[r].color;
                        } else {
                            break;
                        }
                    }
                }
            }
        } else if (colorMode == ColorMode.Gradient) {
            for (int y = 0; y < mapChunkSize; y++) {
                for (int x = 0; x < mapChunkSize; x++) {
                    float currentHeight = noiseMap[x, y];
                    for (int r = 0; r < regions.Length; r++) {
                        if (currentHeight <= regions[r].height) {
                            if (r == 0)
                                colorMap[y * mapChunkSize + x] = regions[r].color;
                            else
                                colorMap[y * mapChunkSize + x] = Color.Lerp(regions[r - 1].color, regions[r].color,
                                    (currentHeight - regions[r - 1].height) / (regions[r].height - regions[r - 1].height));
                            //colorMap[y * mapChunkSize + x] = Color.black;
                            break;
                        }
                    }
                }
            }
        }
        return new MapData(noiseMap, colorMap);
    }

    void OnValidate() {
        // called whenever variables changed in editor
        if (octaves < 1) octaves = 1;
        if (lacunarity < 1f) lacunarity = 1f;
        falloffMap = FalloffGenerator.GenerateFalloffMap(mapChunkSize);
    }

    struct MapThreadInfo<T> {
        public readonly Action<T> callback;
        public readonly T parameter;
        public MapThreadInfo(Action<T> _callback, T _parameter) {
            callback = _callback;
            parameter = _parameter;
        }
    }
}

// this tag makes it show up in editor
[System.Serializable]
public struct TerrainType {
    public string name;
    public float height;
    public Color color;
}

public struct MapData {
    public readonly float[,] heightMap;
    public readonly Color[] colorMap;

    public MapData(float[,] _heightMap, Color[] _colorMap) {
        heightMap = _heightMap;
        colorMap = _colorMap;
    }
}