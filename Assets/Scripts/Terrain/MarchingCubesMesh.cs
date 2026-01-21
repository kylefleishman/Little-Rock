using System.Collections.Generic;
using UnityEngine;

[RequireComponent(typeof(MeshFilter), typeof(MeshRenderer))]
public class MarchingCubesMesh : MonoBehaviour
{
     // Float Array that contains all of the height values
    // Can input x, y, z coordinates and recieve a value based on it
    private float[,,] heights;
    
    // Two lists to determine vertices and triangle indexes
    private List<Vector3> vertices = new List<Vector3>();
    private List<int> triangles = new List<int>();
    private List<Color> colors = new List<Color>();

    // pasted
    private MapGenerator.BiomeType[] biomeRegions;
    private int worldMaxHeight;

    // Used to intialize mesh
    private MeshFilter meshFilter;

    // added biometype and sealevel
    public void BuildChunk(float[,,] data, int width, int height, float threshold, MapGenerator.BiomeType[] regions)
    {
        this.heights = data;

        // pasted
        this.biomeRegions = regions;
        this.worldMaxHeight = height;

        meshFilter = GetComponent<MeshFilter>();
        // we moved set heights to ... MapGenerator under new name ChunkGenerator... how is this passed here?
        // added biometype and sealevel
        MarchCubes(width, height, threshold);
        SetMesh();
    }

    // Clear lists to make sure everything is okay
    // go through x,y,z coordinates to find the value's for each corner of the cube
    // create new vector 3 int, where it would be the new position of where we are trying to march the cube 
    // then add the corner from the marching table at the respective index
    // On the the new float array for the cube corners, on the index of i
    // assign the value from the heights array, and taking it at the correct position of the vertex of the cube
    private void MarchCubes(int noiseWidth, int noiseHeight, float threshold)
    {
        vertices.Clear();
        triangles.Clear();
        colors.Clear();

         for (int x = 0; x < noiseWidth; x++)
        { 
            for (int y = 0; y < noiseHeight; y++)
            {
                for (int z = 0; z < noiseWidth; z++)
                {
                    float[] cubeCorners = new float[8];

                    for (int i = 0; i < 8; i++)
                    {
                        Vector3Int corner = new Vector3Int(x,y,z) + MarchingTable.Corners[i];
                        cubeCorners[i] = heights[corner.x, corner.y, corner.z];
                    }
                    // added biometype, noiseHeight, and seaLevel
                    MarchCube(new Vector3(x,y,z), GetConfigurationIndex(cubeCorners, threshold));

                }
            }
        }
    }

    // Take in vector 3 were we should march cube and integer for configeIndex
    // Check if configuration index is equal to 0 or 255, if so exit due to nonrender case (air or below ground)
    // Build the entire cube by iterating the entire triangles, then the iterate the vertices of a triangle
    // Find the edge, start, and end point, then calculate the mid point were the vertex will be added 
    // Connect triangle to make sure it looks alright :D
    private void MarchCube (Vector3 position, int configIndex)
    {
        if (configIndex == 0 || configIndex == 255)
        {
            return;
        }

        int edgeIndex = 0;

        // never more than 5 triangles
        for (int t = 0; t < 5; t++)
        {
            // never more than 3 vertices in each triangle
            for (int v = 0; v < 3; v++)
            {
                // getting value from triangulation table
                int triTableValue = MarchingTable.Triangles[configIndex, edgeIndex];

                // if we hit -1, no need to go further
                if (triTableValue == -1) 
                {
                    return;
                }
                // find start and beginning using the edges table
                Vector3 edgeStart = position + MarchingTable.Edges[triTableValue, 0];
                Vector3 edgeEnd = position + MarchingTable.Edges[triTableValue, 1];

                // find the midpoint between them and add the vertex
                Vector3 vertex = (edgeStart + edgeEnd) / 2;
                vertices.Add(vertex);
                triangles.Add(vertices.Count - 1);

                // Calculate Color using the state variables set in BuildChunk
                colors.Add(GetBiomeColorAtVertex(vertex));

                edgeIndex++;
            }
        }
    }

    // pasted
    private Color GetBiomeColorAtVertex(Vector3 vertex)
    {
        float yPercent = vertex.y / (float)worldMaxHeight;
        float biomeSampleValue = yPercent;

        for (int i = 0; i < biomeRegions.Length; i++)
        {
            if (biomeSampleValue <= biomeRegions[i].height)
            {
                return biomeRegions[i].color;
            }
        }

        return Color.white;
    }
   
    // Input the float array of the cube corners, then go through all them
    // Then decide if the corner should be active of de-active based on value
    // Based on heightThreshold determine if it should be 1 or 0

    // 0000 1000 - current byte
    // 0000 0010 - added byte
    // 0000 1010 - new byte after adding

    // If on index 5 (fifth vertex of the cube should be on) 
    // Need to put 1 on the fifth bit, i.e. 0001 0000
    private int GetConfigurationIndex(float[] cubeCorners, float threshold)
    {
        int configIndex = 0;

        for (int i = 0; i < 8; i++)
        {
            if (cubeCorners[i] > threshold) 
            {
                configIndex |= 1 << i;
            }
        }

        return configIndex;
    }

    // Create a new mesh, and assign all vertices and traingles to this new Mesh
    // Because vertices and traingles are list, need to add ToArray 
    // Recaculate normals to make sure everything looks okay
      private void SetMesh()
    {
        Mesh mesh = new Mesh();

        mesh.vertices = vertices.ToArray();
        mesh.triangles = triangles.ToArray();
        mesh.colors = colors.ToArray();

        mesh.RecalculateNormals();
   
        meshFilter.mesh = mesh;

        // Assign to the collider for physics/contact / Possible Revision
        MeshCollider meshCollider = GetComponent<MeshCollider>();
        if (meshCollider != null)
        {
            meshCollider.sharedMesh = mesh;
        }
    }

}