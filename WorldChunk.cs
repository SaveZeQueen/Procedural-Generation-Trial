using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System;
using Unity.Mathematics;
using UnityEngine.Tilemaps;

[System.Serializable]
public class WorldChunk
{
    // Position Data
    public int2 Position;
    private int ChunkSize = 0;
    // Internal Data for Terrain
    public int[,] Data;
    public ChunkCell[,] CellData;
    // Enabled State and Child
    private bool Enabled = true;
    // Connected Child Object For Chunk
    public GameObject LinkedChild;
    // Tilemap (automatically linked from chunks)
    private Tilemap dirtBaseTilemap;
    // Tilemap (automatically linked from chunks) 
    private Tilemap chunkTilemap; 
    // Tilemap (automatically linked from chunks) 
    private Tilemap WaterTilemap; 
    // Set True if Drawn
    public bool ChunkDrawn = false;  

    // Initialize Chunk and Size
    public WorldChunk(int chunkSize = 16, GameObject _linkedObj = null){
        // Add one to make world Symetrical in coords
        Data = new int[chunkSize, chunkSize];
        CellData = new ChunkCell[chunkSize, chunkSize];
        ChunkSize = chunkSize;
        LinkedChild = _linkedObj;
        if (LinkedChild != null){
            Tilemap[] children = LinkedChild.GetComponentsInChildren<Tilemap>();
            for (int i = 0; i < children.Length; i++)
            {
                if (children[i].name.Contains("Ground")){
                    chunkTilemap = children[i];
                } else if (children[i].name.Contains("Dirt")){
                    dirtBaseTilemap = children[i];
                } else if (children[i].name.Contains("Water")){
                    WaterTilemap = children[i];
                }
            }
        }
    }

    //===============
    // Used to Check Position is valid
    //===============
    public bool PositionIs(int x, int y){
        return (Position.x == x && Position.y == y);
    }

    public bool PositionIs(int2 pos){
        return PositionIs(pos.x, pos.y);
    }


    //===============
    // Place Water Tile in Chunk (3 variations)
    //===============
    public void PlaceWater(Vector3Int position, TileBase tile){
        if (WaterTilemap != null){
            WaterTilemap.SetTile(position, tile);
        }
    }

    public void PlaceWater(int2 position, TileBase tile){
        PlaceWater(new Vector3Int(position.x, position.y, 0), tile);
    }

    public void PlaceWater(int x, int y, TileBase tile){
        PlaceWater(new Vector3Int(x, y, 0), tile);
    }

    //===============
    // Place Dirt Tile in Chunk (3 variations)
    //===============
    public void PlaceDirt(Vector3Int position, TileBase tile){
        if (dirtBaseTilemap != null){
            dirtBaseTilemap.SetTile(position, tile);
        }
    }

    public void PlaceDirt(int2 position, TileBase tile){
        PlaceDirt(new Vector3Int(position.x, position.y, 0), tile);
    }

    public void PlaceDirt(int x, int y, TileBase tile){
        PlaceDirt(new Vector3Int(x, y, 0), tile);
    }

    //===============
    // Place Tile in Chunk (3 variations)
    //===============
    public void Placetile(Vector3Int position, TileBase tile){
        if (chunkTilemap != null){
            chunkTilemap.SetTile(position, tile);
        }
    }

    public void Placetile(int2 position, TileBase tile){
        Placetile(new Vector3Int(position.x, position.y, 0), tile);
    }

    public void Placetile(int x, int y, TileBase tile){
        Placetile(new Vector3Int(x, y, 0), tile);
    }

    //===============
    // Get Cell Position in the world
    //===============
    public int2 CellPosition(int x, int y){
        return new int2((int)(ChunkWorldPosition.x - ChunkSize/2f) + x, (int)(ChunkWorldPosition.y - ChunkSize/2f) + y);
    }

    //===============
    // Get Vector Position in the world 
    //===============
    public Vector2 VectorCellPosition(int x, int y){
        return new Vector2((int)(ChunkWorldPosition.x - ChunkSize/2f) + x, (int)(ChunkWorldPosition.y - ChunkSize/2f) + y);
    }

    //===============
    // Returns the X,Y index from a vector3 position of this Chunk
    //===============
    public int2 CellIndexFromVector(Vector3 input){
        int2 CellPos = new int2(0, 0);
        CellPos.x = (int)(input.x - (ChunkSize * Position.x) + ChunkSize/2);
        CellPos.x %= ChunkSize;
        CellPos.y = (int)Mathf.Abs(input.y  - (ChunkSize * Position.y) + ChunkSize/2);
        CellPos.y %= ChunkSize;
        return CellPos;
    }

    //===============
    // Returns the cell at world Position
    //===============
    public ChunkCell GetCellFromVector(Vector3 input){
        int2 pos = CellIndexFromVector(input);
        if (pos.x >= 0 && pos.x < ChunkSize && pos.y >= 0 && pos.y < ChunkSize){
            return CellData[pos.x, pos.y];
        } else {
            return null;
        }
        
    }

    //===============
    // Used get Chunk World Position (instead of (-1, 0) would be (-chunkSize, 0)) used for Graphical Offsets
    //===============
    public int2 ChunkWorldPosition{
        get{
            return new int2(Position.x * ChunkSize, Position.y * ChunkSize);
        }
    }

    public bool enabled{
        get{
            return Enabled;
        }
        set{
            if (LinkedChild != null){
                LinkedChild.SetActive(value);
            }
            Enabled = value;
        }
    }
}
