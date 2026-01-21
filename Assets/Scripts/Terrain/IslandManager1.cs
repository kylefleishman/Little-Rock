using UnityEngine;

public class IslandManager : MonoBehaviour
{
    public GameObject chunkPrefab; 
    public MapGenerator noiseGen;

    [Header("Settings")]
    public int chunksPerSide = 16;
    public int chunkSize = 16;
    public int chunkHeight = 32;
    public float threshold = 0.5f;

    void Start()
    {
        // sync mapgenerator.cs map dimensions
        int totalIslandSize = chunksPerSide * chunkSize;
        noiseGen.mapWidth = totalIslandSize;
        noiseGen.mapHeight = totalIslandSize;

        // calculate offset to center island to world origin
        float centerOffset = (totalIslandSize / 2f);

        for (int x = 0; x < chunksPerSide; x++)
        {
            for (int z = 0; z < chunksPerSide; z++)
            {
                // calculate position offset for current chunk
                Vector3Int chunkOffset = new Vector3Int(x * chunkSize, 0, z * chunkSize);

                // position in world space, centered at origin
                Vector3 worldPosition = new Vector3( chunkOffset.x - centerOffset, 0, chunkOffset.z - centerOffset );

                // instantiate chunk at centered position
                GameObject chunkObj = Instantiate(chunkPrefab, worldPosition, Quaternion.identity, transform);
                
                // grab data from mapgenerator.cs
                float[,,] data = noiseGen.GenerateChunkData(chunkOffset, chunkSize, chunkHeight);
                
                // pass it to marchingcubesmesh.cs
                chunkObj.GetComponent<MarchingCubesMesh>().BuildChunk(data, chunkSize, chunkHeight, threshold, noiseGen.regions);
            }

        }
    }
}