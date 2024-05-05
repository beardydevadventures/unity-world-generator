using System.Collections.Generic;
using Unity.VisualScripting.Antlr3.Runtime.Tree;
using UnityEngine;
using UnityEngine.XR;

public enum BiomeType
{
    Desert,
    Plains,
    Forest,
    Swamp,
    Mountain,
    SnowyMountain
}

public class WorldGenerator : MonoBehaviour
{
    public int seed = 12345;
    public Terrain terrain;
    public Biome[] biomes;
    [SerializeField] private BiomeRule[] biomeRules;
    public int width = 256; // Width of the terrain
    public int depth = 256; // Depth of the terrain
    public int height = 20; // Max height of the terrain
    public float scale = 20.0f;
    public float cliffThreshold = 30;
    public int moistureMapOffset = 100;


    private float[,] heightsMap = new float[0, 0];
    private float[,] moistureMap = new float[0, 0];

    // Use this for initialization
    void Start()
    {
        InitializeTerrain();
        WipeTerrain();
        GenerateBiomeLayers();
        PopulateTerrain();
    }

    void InitializeTerrain()
    {
        Random.InitState(seed);
        terrain = GetComponent<Terrain>();
        terrain.terrainData.terrainLayers = GenerateTerrainLayers();
    }

    private void WipeTerrain()
    {
        ClearAllTrees();
    }

    void GenerateBiomeLayers()
    {
        terrain.terrainData = GenerateTerrain(terrain.terrainData);
        SetupTreePrototypes();
    }

    void PopulateTerrain()
    {
        ApplyBiomes(heightsMap, moistureMap);
        ApplyFoliage();
        PlaceRocksAndDetails();
    }
    void ClearAllTrees()
    {
        terrain.terrainData.treeInstances = new TreeInstance[0]; // Clear all tree instances
        terrain.terrainData.RefreshPrototypes(); // Refresh to apply changes immediately
    }

    TerrainLayer[] GenerateTerrainLayers()
    {
        // Update the terrain data with new terrain layers based on biomes
        TerrainLayer[] terrainLayers = new TerrainLayer[biomes.Length];

        for (int i = 0; i < biomes.Length; i++)
        {
            terrainLayers[i] = biomes[i].terrainLayer;
        }

        return terrainLayers;
    }

    TerrainData GenerateTerrain(TerrainData terrainData)
    {
        terrainData.heightmapResolution = width + 1;
        terrainData.size = new Vector3(width, height, depth);
        terrainData.SetHeights(0, 0, GenerateHeights());
        return terrainData;
    }

    void SetupTreePrototypes()
    {
        List<TreePrototype> combinedTreePrototypes = new List<TreePrototype>();
        Dictionary<GameObject, int> prototypeIndexMap = new Dictionary<GameObject, int>();

        // Iterate through all biomes to gather and assign tree prototypes
        foreach (var biome in biomes)
        {
            biome.treePrototypeIndices.Clear(); // Clear previous indices

            foreach (var prefab in biome.foliagePrefabs)
            {
                if (!prototypeIndexMap.ContainsKey(prefab))
                {
                    TreePrototype treePrototype = new TreePrototype { prefab = prefab };
                    combinedTreePrototypes.Add(treePrototype);
                    int index = combinedTreePrototypes.Count - 1;
                    prototypeIndexMap[prefab] = index;
                    biome.treePrototypeIndices.Add(index);
                }
                else
                {
                    // Add existing prototype index to the biome
                    biome.treePrototypeIndices.Add(prototypeIndexMap[prefab]);
                }
            }
        }

        // Assign the collected tree prototypes to the terrain data
        terrain.terrainData.treePrototypes = combinedTreePrototypes.ToArray();
        terrain.terrainData.RefreshPrototypes();  // Refresh to apply changes
    }

    float[,] GenerateHeights()
    {
        heightsMap = new float[width, depth];
        moistureMap = new float[width, depth];

        for (int x = 0; x < width; x++)
        {
            for (int y = 0; y < depth; y++)
            {
                float xCoord = (float)x / width * scale;
                float yCoord = (float)y / depth * scale;

                //this makes peaks
                /*float e0 = 1f * ridgenoise(1 * xCoord + seed, 1 * yCoord + seed);
                float e1 = 0.5f * ridgenoise(2 * xCoord + seed, 2 * yCoord + seed) * e0;
                float e2 = 0.25f * ridgenoise(4 * xCoord + seed, 4 * yCoord + seed) * (e0 + e1);
                heightsMap[x, y] = Mathf.Round(e * 32) / 32;*/

                float e0 = 1f * Mathf.PerlinNoise(1 * xCoord + seed, 1 * yCoord + seed);
                float e1 = 0.5f * Mathf.PerlinNoise(2 * xCoord + seed, 2 * yCoord + seed);
                float e2 = 0.25f * Mathf.PerlinNoise(4 * xCoord + seed, 4 * yCoord + seed);
                float e = (e0 + e1 + e2) / (1 + 0.5f + 0.25f);
                heightsMap[x, y] = Mathf.Pow(e * 1.4f, 2f);
                
                moistureMap[x, y] = Mathf.PerlinNoise(xCoord + moistureMapOffset + seed, yCoord + moistureMapOffset + seed);

                BiomeType biomeType = DetermineBiome(heightsMap[x, y], moistureMap[x, y]);
                AdjustHeightForBiome(ref heightsMap[x, y], biomeType);
            }
        }

        //GenerateLakes(heightsMap, width, depth, 0.0001f, 0.2f);
        ModifyEdgesForIsland(heightsMap, width, depth, 5.0f);

        return heightsMap;
    }

    float ridgenoise(float nx,float ny)
    {
        return 2 * (0.5f - Mathf.Abs(0.5f - Mathf.PerlinNoise(nx, ny)));
    }

    void AdjustHeightForBiome(ref float height, BiomeType biomeType)
    {
        switch (biomeType)
        {
            case BiomeType.Plains:
            case BiomeType.Forest:
                height = Mathf.Lerp(height, 0.3f, 0.6f);
                break;
            case BiomeType.Mountain:
                height = Mathf.Pow(height, 0.9f);
                break;
        }
    }

    void ApplyBiomes(float[,] heights, float[,] moisture)
    {
        int alphaMapWidth = terrain.terrainData.alphamapWidth;
        int alphaMapHeight = terrain.terrainData.alphamapHeight;

        // Create an array to hold the new alpha map data
        float[,,] alphaMaps = new float[alphaMapWidth, alphaMapHeight, terrain.terrainData.alphamapLayers];

        for (int x = 0; x < alphaMapWidth; x++)
        {
            for (int y = 0; y < alphaMapHeight; y++)
            {
                int mapX = (int)(x * (float)width / alphaMapWidth);
                int mapY = (int)(y * (float)depth / alphaMapHeight);
                BiomeType biomeType = DetermineBiome(heights[mapX, mapY], moisture[mapX, mapY]);
                Biome biome = FindBiomeByType(biomeType);

                float steepness = terrain.terrainData.GetSteepness(mapX, mapY);

                if (biome != null)
                {
                    int layerIndex = System.Array.IndexOf(terrain.terrainData.terrainLayers, biome.terrainLayer);

                    if (steepness > cliffThreshold)
                    {
                        int mountainLayerIndex = FindLayerIndexByBiomeType(BiomeType.Mountain);
                        if (mountainLayerIndex != -1)
                        {
                            layerIndex = mountainLayerIndex;
                        }
                    }

                    // Ensure the layer index is valid
                    if (layerIndex != -1)
                    {
                        for (int i = 0; i < terrain.terrainData.alphamapLayers; i++)
                        {
                            alphaMaps[x, y, i] = (i == layerIndex) ? 1.0f : 0.0f;
                        }
                    }
                    
                }
            }
        }

        // Apply all the changes to the alpha map in one go
        terrain.terrainData.SetAlphamaps(0, 0, alphaMaps);
    }

    BiomeType DetermineBiome(float height, float moisture)
    {
        foreach (var rule in biomeRules)
        {
            if (height >= rule.minHeight && height <= rule.maxHeight &&
                moisture >= rule.minMoisture && moisture <= rule.maxMoisture)
            {
                return rule.biomeType;
            }
        }
        return BiomeType.Plains; // Fallback biome
    }

    Biome FindBiomeByType(BiomeType type)
    {
        foreach (var biome in biomes)
        {
            if (biome.biomeType == type)
            {
                return biome;
            }
        }
        Debug.LogWarning($"Could not find biome {type}");
        return null;
    }

    int FindLayerIndexByBiomeType(BiomeType type)
    {
        for (int i = 0; i < biomes.Length; i++)
        {
            if (biomes[i].biomeType == type)
            {
                return i;
            }
        }
        Debug.LogWarning($"Could not find index of biome {type}");
        return -1; // Not found
    }

    void ApplyFoliage()
    {
        List<TreeInstance> treeInstances = new List<TreeInstance>();
        int gridSize = 5;
        int stepSize = gridSize;

        for (int x = gridSize; x < width - gridSize; x += stepSize)
        {
            for (int y = gridSize; y < depth - gridSize; y += stepSize)
            {
                BiomeType biomeType = DetermineBiome(heightsMap[x, y], moistureMap[x, y]);
                Biome biome = FindBiomeByType(biomeType);

                if (biome != null)
                {
                    /*if (biome.detailPrefabs.Length > 0)
                    {
                        ApplyDetailObjects(x, y, biome);
                    }*/

                    if (biome.foliagePrefabs.Length > 0) 
                    { 
                        if (Random.value < biome.foliageDensity) // Apply density check here to reduce the number of tree additions
                        {
                            TreeInstance tree = AddTree(biome, new Vector2(x, y));
                            treeInstances.Add(tree);
                        }
                    }
                }
            }
        }

        AddTrees(treeInstances);
    }

    void ApplyDetailObjects(int x, int y, Biome biome)
    {
        if (biome.detailPrefabs.Length <= 0) return;

        int[,] detailMap = new int[terrain.terrainData.detailWidth, terrain.terrainData.detailHeight];
        for (int i = 0; i < biome.detailPrefabs.Length; i++)
        {
            terrain.terrainData.SetDetailResolution(terrain.terrainData.detailWidth, terrain.terrainData.detailResolutionPerPatch);
            terrain.terrainData.SetDetailLayer(x, y, i, detailMap);
        }
    }

    TreeInstance AddTree(Biome biome, Vector2 gridPosition)
    {

        float worldX = (gridPosition.y + Random.Range(0,3)) / depth * terrain.terrainData.size.z;
        float worldZ = (gridPosition.x + Random.Range(0,3)) / width * terrain.terrainData.size.x;

        // Normalize these coordinates to range [0, 1]
        Vector3 normalizedPosition = new Vector3(
            worldX / terrain.terrainData.size.x,
            0,
            worldZ / terrain.terrainData.size.z);

        // Get height at this normalized position
        float terrainHeight = terrain.terrainData.GetHeight(
            Mathf.FloorToInt(normalizedPosition.x * terrain.terrainData.heightmapResolution),
            Mathf.FloorToInt(normalizedPosition.z * terrain.terrainData.heightmapResolution));
        normalizedPosition.y = terrainHeight / terrain.terrainData.size.y;

        TreeInstance tree = new TreeInstance
        {
            prototypeIndex = biome.treePrototypeIndices[Random.Range(0, biome.treePrototypeIndices.Count)],
            position = normalizedPosition,
            widthScale = 1.0f,
            heightScale = 1.0f,
            color = Color.white,
            lightmapColor = Color.white
        };

        return tree;
    }

    private void AddTrees(List<TreeInstance> treeInstances)
    {
        // Applying only a limited number of trees per frame/call to manage performance
        foreach (var tree in treeInstances)
        {
            terrain.AddTreeInstance(tree);
        }

        terrain.Flush(); // Ensure changes take immediate effect
    }

    void PlaceRocksAndDetails()
    {
        for (int x = 0; x < width; x += 1)  // Loop through the terrain width with a step size
        {
            for (int y = 0; y < depth; y += 1)  // Loop through the terrain depth with a step size
            {
                // Calculate the normalized terrain coordinates (0.0 to 1.0 range)
                float normX = (float)y + Random.Range(-1, 1) / width * terrain.terrainData.size.x;
                float normZ = (float)x + Random.Range(-1, 1) / depth * terrain.terrainData.size.z;

                // Calculate the actual world position based on the terrain size and position
                Vector3 worldPosition = new Vector3(normX, 0, normZ) + terrain.GetPosition();

                // Sample the height at this world position
                worldPosition.y = terrain.SampleHeight(worldPosition);

                // Get the biome and check if it meets conditions to place an object
                float height = heightsMap[x, y];
                BiomeType biomeType = DetermineBiome(height, moistureMap[x, y]);
                Biome biome = FindBiomeByType(biomeType);

                if (biome != null && biome.detailPrefabs.Length > 0 && height > 0.2 && Random.value < biome.detailDensity)
                {
                    // Choose a random prefab from the biome-specific prefabs
                    GameObject prefab = biome.detailPrefabs[Random.Range(0, biome.detailPrefabs.Length)];
                    float yAngle = Random.Range(0f, 360f); // Generate a random angle for rotation around the Z-axis
                    Quaternion randomRotation = Quaternion.Euler(0, yAngle, 0); // Create rotation quaternion
                    Instantiate(prefab, worldPosition, randomRotation);
                }
            }
        }
    }

    void ModifyEdgesForIsland(float[,] heights, int width, int depth, float edgeHeight)
    {
        float edgeWidth = Mathf.Min(width, depth) * 0.1f; // Define how wide the edge effect should be, e.g., 10% of map width

        for (int x = 0; x < width; x++)
        {
            for (int y = 0; y < depth; y++)
            {
                float distanceToEdgeX = Mathf.Min(x, width - x - 1); // Distance to nearest horizontal edge
                float distanceToEdgeY = Mathf.Min(y, depth - y - 1); // Distance to nearest vertical edge

                float edgeFactorX = Mathf.Clamp01(distanceToEdgeX / edgeWidth);
                float edgeFactorY = Mathf.Clamp01(distanceToEdgeY / edgeWidth);
                float edgeFactor = Mathf.Min(edgeFactorX, edgeFactorY);

                // Increase height towards the edges more sharply
                if (edgeFactor < 1)
                {
                    heights[x, y] += edgeHeight * (1 - edgeFactor);
                }
            }
        }
    }

    void GenerateLakes(float[,] heights, int width, int depth, float lakeProbability, float lakeDepth)
    {
        for (int x = 0; x < width; x++)
        {
            for (int y = 0; y < depth; y++)
            {
                // Randomly decide if this point will be the center of a lake
                if (Random.value < lakeProbability)
                {
                    CreateLake(heights, x, y, width, depth, lakeDepth);
                }
            }
        }
    }

    void CreateLake(float[,] heights, int centerX, int centerY, int width, int depth, float depthAmount)
    {
        int radius = 10; // Radius of the lake, adjust based on your needs
        for (int x = centerX - radius; x <= centerX + radius; x++)
        {
            for (int y = centerY - radius; y <= centerY + radius; y++)
            {
                if (x >= 0 && x < width && y >= 0 && y < depth)
                {
                    float distance = Mathf.Sqrt((x - centerX) * (x - centerX) + (y - centerY) * (y - centerY));
                    if (distance < radius)
                    {
                        // Reduce the height based on the distance from the center
                        float reduceAmount = depthAmount * (1 - (distance / radius));
                        heights[x, y] = Mathf.Max(0, heights[x, y] - reduceAmount);
                    }
                }
            }
        }
    }
}