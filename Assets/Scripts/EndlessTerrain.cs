using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class EndlessTerrain : MonoBehaviour {
    const float scale = 20f;

    const float moveThresholdForChunkUpdate = 25;
    const float sqrMoveThresholdForChunkUpdate = moveThresholdForChunkUpdate * moveThresholdForChunkUpdate; // getting square dist is faster than actual dist
    Vector2 viewerLastPos;

    public LODInfo[] detailLevels;
    public static float maxViewDistance;

    public Transform viewer;
    public Material mapMaterial;

    public static Vector2 viewerPosition;
    static MapGenerator mapGenerator;
    int chunkSize;
    int chunksVisibleInViewDistance;

    Dictionary<Vector2, TerrainChunk> terrainChunkDictionary = new Dictionary<Vector2, TerrainChunk>();
    static List<TerrainChunk> terrainChunksVisibleLastUpdate = new List<TerrainChunk>();

    void Start() {
        mapGenerator = FindObjectOfType<MapGenerator>();

        maxViewDistance = detailLevels[detailLevels.Length - 1].visibleDstThreshold;
        chunkSize = MapGenerator.mapChunkSize - 1;
        chunksVisibleInViewDistance = Mathf.RoundToInt(maxViewDistance / chunkSize);

        viewerPosition = new Vector2(viewer.position.x, viewer.position.z) / scale;
        viewerLastPos = viewerPosition;
        UpdateVisibleChunks();
    }

    void Update() {
        viewerPosition = new Vector2(viewer.position.x, viewer.position.z) / scale;
        if ((viewerLastPos - viewerPosition).sqrMagnitude > sqrMoveThresholdForChunkUpdate) {
            viewerLastPos = viewerPosition;
            UpdateVisibleChunks();
        }
    }

    void UpdateVisibleChunks() {
        foreach (var chunk in terrainChunksVisibleLastUpdate) {
            chunk.SetVisible(false);
        }
        terrainChunksVisibleLastUpdate.Clear();

        // chunk coord starting at (0, 0), +1 per chunk
        int currentChunkCoordX = Mathf.RoundToInt(viewerPosition.x / chunkSize);
        int currentChunkCoordY = Mathf.RoundToInt(viewerPosition.y / chunkSize);

        for (int yOffset = -chunksVisibleInViewDistance; yOffset <= chunksVisibleInViewDistance; yOffset++) {
            for (int xOffset = -chunksVisibleInViewDistance; xOffset <= chunksVisibleInViewDistance; xOffset++) {
                Vector2 currentChunkCoord = new Vector2(currentChunkCoordX + xOffset, currentChunkCoordY + yOffset);

                if (terrainChunkDictionary.ContainsKey(currentChunkCoord)) {
                    terrainChunkDictionary[currentChunkCoord].UpdateChunk();
                } else {
                    terrainChunkDictionary.Add(currentChunkCoord, new TerrainChunk(currentChunkCoord, chunkSize, detailLevels, transform, mapMaterial));
                }

            }
        }
    }

    public class TerrainChunk {

        GameObject meshObject;
        Vector2 position;
        Bounds bounds;

        MapData mapData;
        bool mapDataReceived;

        MeshRenderer meshRenderer;
        MeshFilter meshFilter;

        LODInfo[] detailLevels;
        LODMesh[] lodMeshes;
        int previousLODIndex = -1;

        public TerrainChunk(Vector2 coord, int size, LODInfo[] detailLevels, Transform parent, Material material) {
            position = coord * size;
            bounds = new Bounds(position, Vector2.one * size);
            Vector3 posV3 = new Vector3(position.x, 0, position.y);
            this.detailLevels = detailLevels;

            meshObject = new GameObject("Terrain Chunk");
            meshRenderer = meshObject.AddComponent<MeshRenderer>(); // add component returns component it adds
            meshFilter = meshObject.AddComponent<MeshFilter>();
            meshRenderer.material = material;

            meshObject.transform.position = posV3 * scale;
            meshObject.transform.parent = parent;
            meshObject.transform.localScale = Vector3.one * scale;
            SetVisible(false);

            lodMeshes = new LODMesh[detailLevels.Length];
            for (int i = 0; i < detailLevels.Length; i++) {
                lodMeshes[i] = new LODMesh(detailLevels[i].lod, UpdateChunk);
            }

            mapGenerator.RequestMapData(OnMapDataReceived, position);
        }

        void OnMapDataReceived(MapData mapData) {
            this.mapData = mapData;
            mapDataReceived = true;

            Texture2D texture;
            /* disabled for performance
            if (mapGenerator.drawMode == MapGenerator.DrawMode.NoiseMesh) {
                texture = TextureGenerator.TextureFromHeightMap(mapData.heightMap);
            } else {
            }
            */
            texture = TextureGenerator.TextureFromColorMap(mapData.colorMap, MapGenerator.mapChunkSize, MapGenerator.mapChunkSize);
            meshRenderer.material.mainTexture = texture;

            UpdateChunk();
        }

        public void UpdateChunk() {
            if (mapDataReceived) {
                float viewerDistanceFromNearestEdge = Mathf.Sqrt(bounds.SqrDistance(viewerPosition));
                bool visible = viewerDistanceFromNearestEdge <= maxViewDistance;

                if (visible) {
                    int lodIndex = 0;

                    for (int i = 0; i < detailLevels.Length - 1; i++) {
                        if (viewerDistanceFromNearestEdge > detailLevels[i].visibleDstThreshold) {
                            lodIndex++;
                        } else {
                            break;
                        }
                    }
                    if (lodIndex != previousLODIndex) {
                        LODMesh lodMesh = lodMeshes[lodIndex];
                        if (lodMesh.hasMesh) {
                            meshFilter.mesh = lodMesh.mesh;
                            previousLODIndex = lodIndex;
                        } else if (!lodMesh.hasRequestedMesh) {
                            lodMesh.RequestMesh(mapData);
                        }
                    }

                    terrainChunksVisibleLastUpdate.Add(this);
                }

                SetVisible(visible);
            }
        }

        public void SetVisible(bool visible) {
            meshObject.SetActive(visible);
        }

        public bool isVisible() {
            return meshObject.activeSelf;
        }

    }

    class LODMesh {
        public Mesh mesh;
        public bool hasRequestedMesh;
        public bool hasMesh;
        int lod;
        System.Action updateCallback;

        public LODMesh(int lod, System.Action updateCallback) {
            this.lod = lod;
            this.updateCallback = updateCallback;
        }

        public void RequestMesh(MapData mapData) {
            hasRequestedMesh = true;
            mapGenerator.RequestMeshData(OnMeshDataReceived, mapData, lod);
        }

        public void OnMeshDataReceived(MeshData meshData) {
            mesh = meshData.CreateMesh();
            hasMesh = true;
            updateCallback();
        }
    }

    [System.Serializable]
    public struct LODInfo {
        [Range(0, 6)]
        public int lod;
        public float visibleDstThreshold; // when a new lod becomes active
    }
}
