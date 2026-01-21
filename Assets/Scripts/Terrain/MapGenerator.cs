using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class MapGenerator : MonoBehaviour
{
    public enum DrawMode {NoiseMap, colorMap, Terrain3D};
    public DrawMode drawMode;

    public int mapWidth;
    public int mapHeight;
    public float noiseScale;
    public bool autoUpdate;

    public int octaves;
    [Range(0,1)]
    public float persistance;
    public float lacunarity;

    public int seed;
    public Vector2 offset;

    public BiomeType[] regions;

    [Range(0, 1)]
    public float mapMix = 0.5f; // 0 - pure noise,  1 pure falloff multiplied by noise ( i think )

    public IslandManager islandManager; // grab chunk settings from islandmanager for 3d preview
    private GameObject preview3DTerrain;

    public void GenerateMap() {
        float[,] noiseMap = Noise.GenerateNoiseMap(mapWidth, mapHeight, seed, noiseScale, mapMix, octaves, persistance, lacunarity, offset);

        Color[] colorMap = new Color[mapWidth * mapHeight];
        for (int y = 0; y < mapHeight; y++) {
            for (int x = 0; x < mapWidth; x++) {
                float currentHeight = noiseMap [x,y];
                // loop through regions to find what regions find within what height
                for (int i = 0; i < regions.Length; i++) {
                    // found region within height
                    if ( currentHeight <= regions [i].height ) {
                        colorMap[y * mapWidth + x] = regions[i].color;
                        break;
                    }
                }
            }
        }

        // set drawmode to perlin noise / colormap based on editor value
        // draw texture from texture generator
        MapDisplay display = FindObjectOfType<MapDisplay>();
       
        if (drawMode == DrawMode.Terrain3D ) {
            Generate3DPreview();
        }

        if (drawMode == DrawMode.NoiseMap) {
            display.DrawTexture(TextureGenerator.TextureFromHeightMap(noiseMap));
            DestroyImmediate(preview3DTerrain);
        }

        else if (drawMode == DrawMode.colorMap) { 
            display.DrawTexture(TextureGenerator.TextureFromColorMap(colorMap, mapWidth, mapHeight));
            DestroyImmediate(preview3DTerrain);
        }
    }

    void OnValidate() {
        if (mapWidth < 1) { mapWidth = 1; }
        if (mapHeight < 1) { mapHeight = 1; }
        if (lacunarity < 1) { lacunarity = 1; }
        if (octaves < 0) { octaves = 0; }
    }

    [System.Serializable]
    public struct BiomeType {
        public string name;
        public float height;
        public Color color;
    }

   private void Generate3DPreview() {
       // realistically most of this could have og values passed in better but nobody is paying me for this so this works for now
        preview3DTerrain = new GameObject("3D Map Preview");
        preview3DTerrain.transform.SetParent(transform);

        int chunksPerSide = islandManager.chunksPerSide;
        int chunkSize = islandManager.chunkSize;
        int chunkHeight = islandManager.chunkHeight;
        float threshold = islandManager.threshold;
        GameObject chunkPrefab = islandManager.chunkPrefab;

        // sync mapgenerator.cs map dimensions
        // seems to be not needed? as long as we know chunksperside and chunksize
        int totalIslandSize = chunksPerSide * chunkSize;
        // noiseGen.mapWidth = totalIslandSize;
        // noiseGen.mapHeight = totalIslandSize;

        // calculate offset to center island to world origin
        float centerOffset = (totalIslandSize / 2f);

        // generate those chunks
        for (int x = 0; x < chunksPerSide; x++)
        {
            for (int z = 0; z < chunksPerSide; z++)
            {
                // calculate position offset for current chunk
                Vector3Int chunkOffset = new Vector3Int(x * chunkSize, 0, z * chunkSize);
                
                // position in world space, centered at origin
                Vector3 worldPosition = new Vector3(chunkOffset.x - centerOffset,  0, chunkOffset.z - centerOffset);

                // instantiate chunk at centered position
                GameObject chunkObj = Instantiate(chunkPrefab, worldPosition, Quaternion.identity, preview3DTerrain.transform);
                
                // grab data from mapgenerator.cs
                float[,,] data = GenerateChunkData(chunkOffset, chunkSize, chunkHeight);
                
                // pass it to marchingcubesmesh.cs
                MarchingCubesMesh meshBuilder = chunkObj.GetComponent<MarchingCubesMesh>();
                if (meshBuilder != null)
                {
                    meshBuilder.BuildChunk(data, chunkSize, chunkHeight, threshold, regions);
                }
            }
        }
   }

   public float[,,] GenerateChunkData(Vector3Int chunkOffset, int size, int height)
{
    // use n + 1 to create seamless transitions between chunks
    float[,,] heights = new float[size + 1, height + 1, size + 1];

    for (int x = 0; x <= size; x++) {
        for (int z = 0; z <= size; z++) {
            // calculate world coordinates
            float globalX = x + chunkOffset.x;
            float globalZ = z + chunkOffset.z;

            // get our normalized noise from noise.cs GetNoiseAtPoint(); ( 0 to 1 )
            // tie perlin noise and falloff map to "world bounds"
            // removed totalIsland size and use mapWidth and mapHeight instead now
            // passed in so falloffMap knows bounds of the world
            float noiseVal = Noise.GetNoiseAtPoint(globalX, globalZ, mapWidth, mapHeight, seed, noiseScale, mapMix, octaves, persistance, lacunarity, offset);

            // noise val is a stable 0 to 1, and now acts a percentage
            // 0% of max height
            // 0.5 = 50% of max height
            // 1 = 100% of max height
            // may want to change name of height variable to reflect
            // if world size is 100 units, 0.5 is 50 units
            // subtract biomeNoise before multiplying
            // shift seaNoiseLevel to be the new floor
            float currentHeight = noiseVal  * height; 

            for (int y = 0; y <= height; y++) {
                heights[x, y, z] = (y - currentHeight);
            }
        }
    }
    return heights;
}
}
