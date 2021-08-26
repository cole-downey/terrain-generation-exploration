using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public static class MeshGenerator {
    public static MeshData GenerateTerrainMesh(float[,] heightMap, float yScale, AnimationCurve _heightCurve, int resolution) {
        AnimationCurve heightCurve = new AnimationCurve(_heightCurve.keys); // to make thread-safe
        int width = heightMap.GetLength(0);
        int height = heightMap.GetLength(1);

        resolution = (resolution == 0) ? 1 : resolution * 2;
        int verticesPerLine = (width - 1) / resolution + 1;

        MeshData meshData = new MeshData(verticesPerLine, verticesPerLine);
        int vertexIndex = 0;

        for (int y = 0; y < height; y += resolution) {
            for (int x = 0; x < width; x += resolution) {
                meshData.AddVertice((width - 1) / 2f - x, heightCurve.Evaluate(heightMap[x, y]) * yScale, (height - 1) / 2f - y);
                meshData.AddUV(x / (float)width, y / (float)height);

                if (x < width - 1 && y < height - 1) {
                    meshData.AddTriangle(vertexIndex, vertexIndex + verticesPerLine, vertexIndex + verticesPerLine + 1);
                    meshData.AddTriangle(vertexIndex + verticesPerLine + 1, vertexIndex + 1, vertexIndex);
                }
                vertexIndex++;
            }
        }
        for (int r = 0; r < height - 1; r++) {
            for (int c = 0; c < width - 1; c++) {
                // a = r * width + c
                // b = r * width + c + 1
                // d = (r + 1) * width + c
                // c = (r + 1) * width + c + 1
                //meshData.AddTriangle(r * width + c, (r + 1) * width + c + 1, r * width + c + 1);
                //meshData.AddTriangle(r * width + c, (r + 1) * width + c, (r + 1) * width + c + 1);
            }
        }
        // returns MeshData instead of Mesh to implement threading
        return meshData;
    }
}

public class MeshData {
    public Vector3[] vertices;
    public int[] triangles;
    public Vector2[] uvs;

    int verticeIndex;
    int triangleIndex;
    int uvIndex;

    public MeshData(int meshWidth, int meshHeight) {
        vertices = new Vector3[meshWidth * meshHeight];
        uvs = new Vector2[meshWidth * meshHeight];
        triangles = new int[(meshWidth - 1) * (meshHeight - 1) * 6];
        verticeIndex = 0;
        triangleIndex = 0;
        uvIndex = 0;
    }

    public void AddTriangle(int a, int b, int c) {
        triangles[triangleIndex] = a;
        triangles[triangleIndex + 1] = b;
        triangles[triangleIndex + 2] = c;
        triangleIndex += 3;
    }

    public void AddVertice(float x, float y, float z) {
        vertices[verticeIndex] = new Vector3(x, y, z);
        verticeIndex++;
    }

    public void AddUV(float u, float v) {
        uvs[uvIndex] = new Vector2(u, v);
        uvIndex++;
    }

    Vector3[] CalculateNormals() {
        Vector3[] vertexNormals = new Vector3[vertices.Length];
        int triangleCount = triangles.Length / 3;
        for (int i = 0; i < triangleCount; i++) {
            int normalTriangleIndex = i * 3;
            int vertexIndexA = triangles[normalTriangleIndex];
            int vertexIndexB = triangles[normalTriangleIndex + 1];
            int vertexIndexC = triangles[normalTriangleIndex + 2];
            Vector3 triangleNormal = SurfaceNormalFromIndices(vertexIndexA, vertexIndexB, vertexIndexC);
            vertexNormals[vertexIndexA] += triangleNormal;
            vertexNormals[vertexIndexB] += triangleNormal;
            vertexNormals[vertexIndexC] += triangleNormal;
        }
        for(int i = 0; i < vertexNormals.Length; i++) {
            vertexNormals[i].Normalize();
        }
        return vertexNormals;
    }

    Vector3 SurfaceNormalFromIndices(int indexA, int indexB, int indexC) {
        Vector3 pointA = vertices[indexA];
        Vector3 pointB = vertices[indexB];
        Vector3 pointC = vertices[indexC];

        return Vector3.Cross(pointB - pointA, pointC - pointA).normalized;
    }

    public Mesh CreateMesh() {
        Mesh mesh = new Mesh();
        mesh.vertices = vertices;
        mesh.triangles = triangles;
        mesh.uv = uvs;
        mesh.RecalculateNormals();
        //mesh.normals = CalculateNormals();
        return mesh;
    }
}
