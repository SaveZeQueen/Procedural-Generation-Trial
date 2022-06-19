using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Tilemaps;

[System.Serializable]
public class CellTileData
{
    // Name for Editor
    public string name;
    // Spawn Chance for possible tile
    [Range(0, 100)]
    public int SpawnChance = 100;
    // Possible tiles to place
    public List<CellTile> cellTiles = new List<CellTile>();
    [Range(0.00f, 1.00f)]
    // depth start point
    public float depth;


    [System.Serializable]
    public struct CellTile
    {
        // Tile To be displayed at depth
        public TileBase tile;
    }

}
