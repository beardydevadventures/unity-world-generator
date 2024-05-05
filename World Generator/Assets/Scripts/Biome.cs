using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(fileName = "New Biome", menuName = "Biome")]
public class Biome : ScriptableObject
{
    public BiomeType biomeType;  // This is the enum
    public TerrainLayer terrainLayer;
    public GameObject[] foliagePrefabs;  // Trees and large bushes
    [SerializeField] public DetailPrototype[] detailTypes;  // Grass and small plants
    public float foliageDensity;  // General multiplier for density
    public float detailDensity;   // Specific for smaller details like grass
    public List<int> treePrototypeIndices;  // Indices of tree prototypes in the global array

    public Biome()
    {
        treePrototypeIndices = new List<int>();
    }
    // Additional biome-specific settings can be added here
}