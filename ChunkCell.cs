using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Tilemaps;

[System.Serializable]
public class ChunkCell
{
    // Tile in this Cell (to speed up drawing and loading)
    public TileBase AttachedTile;
    // Cell position in the world
    public Vector2Int WorldPosition;
    // Cell position in the chunk
    public Vector2Int CellIndex;
    // If Cell is able to be moved through
    public bool Passable = true;
    // The Height Sample from Noise Map 
    public float CellSample = 0f;
    // If Cell is inside world Bounds
    public bool inWorldBounds = true;
    // Cell Attached Entity
    public GameObject Entity = null;
}
