using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System;
using Unity.Mathematics;
using UnityEditor;
using UnityEngine.UI;
using UnityEngine.Tilemaps;
using Unity.Collections;
using Unity.Jobs;
using Unity.Burst;
using System.Linq;
using UnityEngine.Rendering;


public class WorldGenerator : MonoBehaviour
{
    // Define World Bounds for Limited Worlds (So worlds can Expand as you unlock more)
    [Header("Chunk Asset Data")]
    // Game Object that Defines the Visual Area for a chunk
    public GameObject ChunkParent;
    public GameObject ChunkPrefab;
    [Header("Chunk Gen Variables")]
    // Base World Data
    public int ChunkSize = 75;
    public string Seed = "";
    [Range(1f, 30000f)]
    public float PerlinScale = 100f;
    [Range(1, 100)]
    public int PerlinOctaves = 5;
    public float persistence = 2f;
    public float lacunarity = 2f;
    [Range(0.001f, 3.000f)]
    public float PerlinBaseAmplitude = 1f;
    // Random World Offset
    private float xOffset;
    private float yOffset;
    // Pseudo Random Number Generator
    private System.Random pseudoRandom;
    
    // Chunk Data Split into Sections (Each Chunk having Coords (x, y))
    public Dictionary<string, WorldChunk> chunks = new Dictionary<string, WorldChunk>();
    [Header("Active Chunk Data")]
    public List<WorldChunk> ActiveChunks = new List<WorldChunk>();
    // Tree and Resource Data
    [Header("Entity Spawn Information")]
    public GameObject TreeObj;
    public List<Sprite> TreeTops = new List<Sprite>();
    public List<Sprite> TreeTrunks = new List<Sprite>();
    [Range(0, 100)]
    public int TreeSpawnChance = 45;
    [Header("Chunk Default Tile List")]
    // Default Tile to be placed if no tile Exists for depth
    [Tooltip("This is used on all chunks as a base (mostly for clear water tiles)")]
    public Tile BaseDirtTile;
    public Tile WaterTile;
    public TileBase BankTile;
    public List<Tile> defaultTiles = new List<Tile>();
    [Header("Chunk Cell Tile Data")]
    // Tiles for Chunks (Testing)
    public List<CellTileData> tileGenData = new List<CellTileData>();
    public Tilemap mountainMap;
    public Tilemap WaterMap;
    public SpriteRenderer Clouds;

    // Get previous load position in world
    private Vector2Int prevChunkPos = new Vector2Int(-1,-1);
    [Header("DEBUG TEXT OBJECT")]
    // Debug Text
    public bool SHOWDEBUG = false;
    public Text DebugText;
     
    public FastNoise fastNoise = new FastNoise();

    // Load Chunks
    [Header("Loading Options")]
    public Vector2Int WorldSize = new Vector2Int(32, 32);
    int TotalChunks;
    BoxCollider2D boxCollider2D;
    public Text LoadingText;
    public Text LoadingPerc;
    public Image LoadingBar;
    public GameObject LoadScreen;
    public List<string> LoadingBlerbs = new List<string>();

    [HideInInspector]
    public static WorldGenerator Access;


    //============================================================
    // Set Warm-Up Data
    //============================================================
    private void Awake() {
        // Get/Create Seed
        if (Seed == ""){
            Seed = GenerateRandomSeed();
        }
        // Get Random Number Generator
        pseudoRandom = new System.Random(Seed.GetHashCode());
         // Set Offsets from seed (new world each time)
        xOffset = pseudoRandom.Next(-10000, 10000);
        yOffset = pseudoRandom.Next(-10000, 10000);
        // Set-up Fast noise for Rivers
        if (fastNoise == null){
            fastNoise = new FastNoise();
        }
        // Set Total Chunks to load
        TotalChunks = (WorldSize.x * WorldSize.y) + WorldSize.x*3 + WorldSize.y*2;
        // Set Collider and Size
        boxCollider2D = GetComponent<BoxCollider2D>();
        boxCollider2D.size = new Vector2((WorldSize.x+1) * ChunkSize, (WorldSize.y) * ChunkSize);
        // Set Noise Data For Rivers/Lakes
        fastNoise.SetNoiseType(FastNoise.NoiseType.Cellular);
        fastNoise.SetFrequency(0.02f);
        fastNoise.SetInterp(FastNoise.Interp.Quintic);
        fastNoise.SetFractalType(FastNoise.FractalType.FBM);
        fastNoise.SetFractalOctaves(5);
        fastNoise.SetFractalLacunarity(2.0f);
        fastNoise.SetFractalGain(0.5f);
        fastNoise.SetCellularDistanceFunction(FastNoise.CellularDistanceFunction.Euclidean);
        fastNoise.SetCellularReturnType(FastNoise.CellularReturnType.Distance2Div);
        fastNoise.SetGradientPerturbAmp(30.0f);
        // Using to Clear while Making Test Adjustments
        chunks.Clear();
        ActiveChunks.Clear();
        // Set Self for global Access to functions
        Access = this;
        LoadScreen.SetActive(true);
        InvokeRepeating("LoadChunks", 0f, 0.001f);
    }

    private void Update() {
        // If world is empty load Chunks
        if (chunks.Count < TotalChunks){
            return;            
        } else {
            if (LoadScreen.activeSelf == true){
                CancelInvoke("LoadChunks");
                EnableChunks(new int2(0, 0));
                LoadScreen.SetActive(false);
            }
            // Set Clouds
           // Clouds.size = new Vector2(mountainMap.size.x, mountainMap.size.y);
            // Generate new Chunks if they don't exist & Enable Active Chunks 
            EnableChunks(ChunkAtVector(Camera.main.transform.position));
            if (Input.GetKeyDown(KeyCode.R)){

            }
            // Do Debug
            // =======================
                if (chunks != null && ActiveChunks != null && SHOWDEBUG == true && DebugText != null){
                    Vector3 mousePos = Camera.main.ScreenToWorldPoint(Input.mousePosition);
                    int2 hoverChunkPos = ChunkAtVector(mousePos);
                    WorldChunk c = GetHoverChunk(hoverChunkPos.x, hoverChunkPos.y);

                    if (c != null){
                    ChunkCell cc = c.GetCellFromVector(mousePos);
                    float CellSample = cc.CellSample;
                    
                    if (cc != null){
                    DebugText.text = $"TOTAL_LOADED_CHUNKS:{chunks.Count}\n" + 
                    $"ACTIVE_CHUNKS:{ActiveChunks.Count}\n" +
                    $"CHUNK: {hoverChunkPos.x},{hoverChunkPos.y} | {c.LinkedChild}\n" + 
                    $"CELL: {cc.CellIndex.x},{cc.CellIndex.y} | {CellSample} | {cc.WorldPosition.x},{cc.WorldPosition.y}\n" +
                    $"TILE: {((cc.AttachedTile != null) ? cc.AttachedTile.name : "NULL")} | {cc.Entity}\n";
                    }
                    }
                }

            // =======================
        }
    }

    // ===
    //  Load Chunks into Memory
    // ===
    int ncx = 0;
    int ncy = 0;
    private void LoadChunks(){
        
        // Get Next Chunk Location
        int x = -1+-(WorldSize.x/2) + ncx;
        int y = -1+-(WorldSize.y/2) + ncy;
       
        // Generate Chunk
        WorldChunk c = GetChunk(x, y);
        c.enabled = false;

        if (ncx == WorldSize.x+2){
            ncx = 0;
            ncy += 1;
            System.GC.Collect();
            return;
        } else {
            ncx ++;
        }
        // Increase Chunk Index
      UpdateLoadScreen();
    }

    // ===
    //  Update Loading Screen Display
    // ===
    private void UpdateLoadScreen(){
        float targetFillAmount = ((float)chunks.Count / (float)TotalChunks);
        LoadingBar.fillAmount = Mathf.Lerp(LoadingBar.fillAmount, targetFillAmount, Time.deltaTime * 10f);
        
        LoadingPerc.text = $"{Mathf.RoundToInt(targetFillAmount * 100)}/100%";
        LoadingText.text = (chunks.Count % (TotalChunks/10) == 0) ? LoadingBlerbs[UnityEngine.Random.Range(0, LoadingBlerbs.Count)] : LoadingText.text;
    }


    // ===
    //  Get The Chunk at a Vector position
    // ===
    public int2 ChunkAtVector(Vector3 input){
        int2 chunkPos = new int2(0, 0);
        // Get X/Y Offsets for camera to correctly load new chunks (this will be updated to work with camera bounds later instead this is just temporary)
        if (input.x >= 0){
            chunkPos.x = (int)((input.x+ChunkSize/2) / ChunkSize);
        } else {
            chunkPos.x = (int)((input.x-ChunkSize/2) / ChunkSize);
        }

        if (input.y >= 0){
            chunkPos.y = (int)((input.y+ChunkSize/2) / ChunkSize);
        } else {
            chunkPos.y = (int)((input.y-ChunkSize/2) / ChunkSize);
        }

        return chunkPos;
    }


    // ===
    //  Enable Chunk at position and surrounding chunks, disable all active chunks outsize area
    // ===
    private void EnableChunks(int2 chunkPosition){
        // Check if needs to update
        Vector2Int cpos = new Vector2Int(chunkPosition.x, chunkPosition.y);
        if (!PositionIsNew(cpos)){
            return;
        }
        // Get offsets
        List<int2> offsets = new List<int2>();
        // Add 1, 1 & -1, -1  and so on for full square
        offsets.Add(new int2(0, 1));
        offsets.Add(new int2(0, -1));
        offsets.Add(new int2(2, 0));
        offsets.Add(new int2(1, 0));
        offsets.Add(new int2(-2, 0));
        offsets.Add(new int2(-1, 0));
        offsets.Add(new int2(0, 0));
        offsets.Add(new int2(-2, -1));
        offsets.Add(new int2(-1, -1));
        offsets.Add(new int2(-1, 1));
        offsets.Add(new int2(-2, 1));
        offsets.Add(new int2(2, -1));
        offsets.Add(new int2(1, -1));
        offsets.Add(new int2(1, 1));
        offsets.Add(new int2(2, 1));
        // Set all Active Chunks Inactive
        foreach (int2 offset in offsets)
        {
            for (int i = 0; i < ActiveChunks.Count; i++)
            {
                // Check if the Chunk exists in active List (this is to reduce lag when loading and unloading chunks)
                // So you don't cycle through all the chunks to see what is enabled
                if (ActiveChunks[i] != null){
                    // If it does disable it and remove it from the list
                    ActiveChunks[i].enabled = false;
                    ActiveChunks.RemoveAt(i);
                }
            }
        }
        // Cycle offsets and Enable All Chunks within
        foreach (int2 offset in offsets)
        {
            WorldChunk offsetChunk = GetChunk(chunkPosition.x + offset.x, chunkPosition.y + offset.y);
            offsetChunk.enabled = true;
            ActiveChunks.Add(offsetChunk);
        }
        prevChunkPos = cpos;
    }

    // ===
    //  BOOL - Check if current pos is the same as old 
    // ===
    public bool PositionIsNew(Vector2Int position){
        return (prevChunkPos != position);
    }

    //============================================================
    // Generation Code
    //============================================================

    // ===
    //  Create New Chunks
    // ===
    public void GenerateChunk(int x, int y){
        // Set Key to use
        string key = $"{x},{y}";
        // Check if key exists if not Generate New Chunk
        if (!chunks.ContainsKey(key)){
            // Define GameObject in Chunks
            //if (EditorApplication.isPlaying){
            GameObject gChunk = Instantiate(ChunkPrefab, ChunkParent.transform);
            gChunk.name = $"CHUNK_[{key}]";
            gChunk.transform.position = new Vector3(((x * ChunkSize) - ChunkSize/2), ((y * ChunkSize) - ChunkSize/2));
            // Add Chunk, Set Position in chunk grid (for calling and block data later), Then Generate data
                chunks.Add(key, new WorldChunk(ChunkSize, gChunk));
            } else {
                chunks.Add(key, new WorldChunk(ChunkSize));
            }
            chunks[key].Position = new int2(x, y);



            GenerateChunkData(chunks[key]);
        //}
    }
    // ===
    //  Get Current Chunk From X/Y or INt2
    // ===
    public WorldChunk GetChunk(int x, int y){
        // Set Key to use
        string key = $"{x},{y}";
        // Check if key exists if not Generate New Chunk
        if (chunks.ContainsKey(key)){
            return chunks[key];
        } else {
            GenerateChunk(x,y);
            return chunks[key];
        }
    }

    public WorldChunk GetChunk(int2 intpos){
        return GetChunk(intpos.x, intpos.y);
    }

    // ===
    //  Get Hover Chunk From X/Y
    // ===
    public WorldChunk GetHoverChunk(int x, int y){
        // Set Key to use
        string key = $"{x},{y}";
        // Check if key exists if not Generate New Chunk
        if (chunks.ContainsKey(key)){
            return chunks[key];
        } else {
            return null;
        }
    }

    // ===
    //  Get Tile at current Vector
    // ===
    public ChunkCell GetCellAt(Vector2 position){
        // Get Chunk at position
        int2 chunkAtPos = ChunkAtVector(position);
        WorldChunk chunk = GetHoverChunk(chunkAtPos.x, chunkAtPos.y);
        // Check if Chunk Exists
        if (chunk != null){
            // Get Cell
            ChunkCell chunkCell = chunk.GetCellFromVector(position);
            // Return Cell
            return chunkCell;
        }
        // Return Null if Nothing Found
        return null;
    }

    // ===
    //  Fill Chunks with Perlin Data
    // ===
    private void GenerateChunkData(WorldChunk chunk, bool noDraw = false){
        
        // Do Job for Chunk
        NativeArray<float> output = new NativeArray<float>(ChunkSize*ChunkSize, Allocator.Persistent);
        // Initialize new job
        NoiseJob job = new NoiseJob{
            Output = output,
            PerlinScale = PerlinScale,
            xOffset = xOffset,
            yOffset = yOffset,
            PerlinBaseAmplitude = PerlinBaseAmplitude,
            PerlinOctaves = PerlinOctaves,
            persistence = persistence,
            ChunkSize = ChunkSize,
            Position = chunk.Position,
            lacunarity = lacunarity
        };
        // Run Job
        JobHandle jh = job.Schedule();
        jh.Complete();
       
        // Cycle Array
        if (job.Output.Length > 0){
            // Set index
            int index = 0;
            // Loop index
            for (int x = 0; x < ChunkSize; x++)
            {
                for (int y = 0; y < ChunkSize; y++)
                {
                    // Get Cell position
                    int2 cellPos = chunk.CellPosition(x,y);
                    //Start new Cell
                    chunk.CellData[x,y] = new ChunkCell();
                    //Load cell with current Cell index in chunk
                    chunk.CellData[x,y].CellIndex = new Vector2Int(x, y);
                    //Load Cell with current world index
                    chunk.CellData[x,y].WorldPosition = new Vector2Int(cellPos.x, cellPos.y);
                    // Set Sample from Job
                    chunk.CellData[x,y].CellSample = job.Output[index];
                    index ++;
                }
            }
        }
        // Dispose of array
        output.Dispose();

        // Draws the Tile onto the map
       // if (EditorApplication.isPlaying){
           if (noDraw == false){
            DrawTileMapForChunk(chunk);
           }
       // }
    }

    // ===
    //  Draws The Tile Data for that chunk
    // ===
    private void DrawTileMapForChunk(WorldChunk chunk){
        // Cycle Depths for Tiles at each x, y
        for (int x = 0; x < ChunkSize; x++)
        {
            for (int y = 0; y < ChunkSize; y++)
            {
                // Set New Tile
                int2 tcpos = chunk.CellPosition(x, y);
                chunk.PlaceDirt(x, y, BaseDirtTile);
                // Check if River at position
                if (GetRiverAt(tcpos.x, tcpos.y) == 0 && 
                GetRiverAt(tcpos.x, tcpos.y - 1) == 0 &&
                GetRiverAt(tcpos.x, tcpos.y + 1) == 0 &&
                GetRiverAt(tcpos.x - 1, tcpos.y) == 0 &&
                GetRiverAt(tcpos.x + 1, tcpos.y) == 0 &&
                GetRiverAt(tcpos.x + 1, tcpos.y + 1) == 0 &&
                GetRiverAt(tcpos.x - 1, tcpos.y + 1) == 0 &&
                GetRiverAt(tcpos.x + 1, tcpos.y - 1) == 0 &&
                GetRiverAt(tcpos.x - 1, tcpos.y - 1) == 0){
                // Set Default Tile
                int dti = (pseudoRandom.Next(0, defaultTiles.Count * 100) / 100);
                if (dti > defaultTiles.Count){
                    dti = 0;
                }
                TileBase dt = defaultTiles[dti];
                TileBase t = defaultTiles[dti];
                string cellName = "";
                // Check if Cell Data has a tile
                if (chunk.CellData[x,y].AttachedTile == null){
                    // If not Set Sample and Check for tile
                    float s = Mathf.Clamp01(chunk.CellData[x,y].CellSample);
                    
                    // For each x,y cycle the cell data to see if depth is at or below point
                    foreach (CellTileData celldata in tileGenData)
                    {
                        if (celldata.depth <= s){
                            // Draw Trees
                            cellName = celldata.name;
                            if (celldata.name == "Woods"){
                                // Random Spawn Roll
                                int tspRoll = pseudoRandom.Next(0, 100);
                                // Do Roll Check
                                if (tspRoll <= TreeSpawnChance){
                                    //Spawn Tree in chunk at location
                                   GameObject tree = Instantiate(TreeObj, chunk.LinkedChild.transform);
                                    Vector2Int wpos = chunk.CellData[x,y].WorldPosition;
                                    tree.transform.position = new Vector3(wpos.x, wpos.y, 0);
                                    // Get Renderers
                                    SpriteRenderer trunk = tree.GetComponent<SpriteRenderer>();
                                    SpriteRenderer[] tsprch = tree.GetComponentsInChildren<SpriteRenderer>();
                                    SpriteRenderer bush = tree.GetComponentInChildren<SpriteRenderer>();
                                    // Set Sorting Layer
                                    tree.GetComponent<SortingGroup>().sortingOrder = -tcpos.y;
                                    // Set Bush
                                    for (int i = 0; i < tsprch.Length; i++)
                                    {
                                        if (tsprch[i].name.Contains("Bush")){
                                            bush = tsprch[i];
                                        }
                                    }
                                    // Set Tree Top Graphics
                                    if (TreeTops.Count > 0){
                                        // Roll Trees
                                        int topindRoll = (pseudoRandom.Next(0, TreeTops.Count * 100) / 100);
                                        if (topindRoll > TreeTops.Count){
                                            topindRoll = 0;
                                        }
                                        // Change Graphic
                                        bush.flipX = (pseudoRandom.Next(0, 1) == 0) ? false : true;
                                        bush.sprite = TreeTops[topindRoll];
                                    }

                                    // Set Tree Top Graphics
                                    if (TreeTrunks.Count > 0){
                                        // Roll Trees
                                        int trunkindRoll = (pseudoRandom.Next(0, TreeTrunks.Count * 100) / 100);
                                        if (trunkindRoll > TreeTrunks.Count){
                                            trunkindRoll = 0;
                                        }
                                        // Change Graphic
                                        trunk.sprite = TreeTrunks[trunkindRoll];
                                    }

                                   chunk.CellData[x,y].Entity = tree;
                                }
                            }
                            // Contiue Cell Draw
                            if (celldata.cellTiles != null && celldata.cellTiles.Count >= 0){
                                // If more than 1 tile in data block roll for tile chance
                                int spawRoll = pseudoRandom.Next(0, 100);
                                if (celldata.cellTiles.Count > 1){
                                    // Do random roll
                                    int indexRoll = pseudoRandom.Next(0, (celldata.cellTiles.Count-1) * 100) / 100;
                                    // Loop through available tiles and check if can spawn
                                    if (spawRoll <= celldata.SpawnChance){
                                        t = celldata.cellTiles[indexRoll].tile;
                                    }
                                // if count == 1 get first tile
                                } else {
                                    if (spawRoll <= celldata.SpawnChance){
                                        t = celldata.cellTiles[0].tile;
                                    }
                                }
                                // if tiles in data keep default
                            }
                            break;
                        }
                    }
                    // Set New tile to cell
                    chunk.CellData[x,y].AttachedTile = t;
                } else {
                    t = chunk.CellData[x,y].AttachedTile;
                }
                
                
                    if (cellName != "Mountain"){
                        chunk.Placetile(x, y, t);
                    } else {
                        chunk.Placetile(x, y, dt);
                       mountainMap.SetTile(new Vector3Int(tcpos.x, tcpos.y, 0), t);
                    }
                
            } else {
                // Draw Water Tile
                chunk.CellData[x,y].AttachedTile = WaterTile;
                WaterMap.SetTile(new Vector3Int(tcpos.x, tcpos.y, 0), BankTile);
            }
            }
        }
        
    }

    // ===
    //  Generate Random Seed of Length
    // ===
    public string GenerateRandomSeed(int maxCharAmount = 10, int minCharAmount = 10){
        //Set Characters To Pick from
        const string glyphs= "abcdefghijklmnopqrstuvwxyz0123456789";
        //Set Length from min to max
        int charAmount = UnityEngine.Random.Range(minCharAmount, maxCharAmount);
        // Set output Variable
        string output = "";
        // Do Random Addition
        for(int i=0; i<charAmount; i++)
        {
            output += glyphs[UnityEngine.Random.Range(0, glyphs.Length)];
        }
        // Output New Random String
        return output;
    }

    
    //============================================================
    // Draw Example
    //============================================================
    private void OnDrawGizmos() {
                   
         
    }
    //===============
    // BURST NOISE JOB
    //===============
    public int GetRiverAt(int ix, int iy){
        int x = ix + (int)xOffset;
        int y = iy + (int)yOffset;
        float pcx = (float)x;
        float pcy = (float)y;
        float px = (float)x / 5;
        float py = (float)y / 5;
        fastNoise.GradientPerturbFractal(ref px, ref py);
        float p = Mathf.Sin(fastNoise.GetCellular(px, py) - fastNoise.GetPerlinFractal(pcx, pcy));
        if (p <= 0.82){
            p = 0;
        } else {
            p = 1;
        }

        return (int)p;
    }

    //===============
    // BURST NOISE JOB
    //===============
    [BurstCompile(CompileSynchronously = true)]
    public struct NoiseJob : IJob
    {

        public NativeArray<float> Output;
        public float PerlinScale;
        public float xOffset;
        public float yOffset;
        public float PerlinBaseAmplitude;
        public int PerlinOctaves;
        public float persistence;
        public int ChunkSize;
        public int2 Position;
        public float lacunarity;

        public void Execute()
        {
            // Set min / max
            float maxNoiseHeight = float.MaxValue;
            float minNoiseHeight = float.MinValue;
        // Set Max Height for World
            float maxPossibleHeight = 0;
            float amplitude = PerlinBaseAmplitude;
            for (int i = 0; i < PerlinOctaves; i++)
            {
                maxPossibleHeight += amplitude;
                amplitude *= persistence;
            }
            // Set Data to Chunk
            for (int x = 0; x < ChunkSize; x++)
            {
                for (int y = 0; y < ChunkSize; y++)
                {   int2 ChunkWorldPosition = new int2(Position.x * ChunkSize, Position.y * ChunkSize);
                    // Get World Cell Position
                    int2 cellPos = new int2((int)(ChunkWorldPosition.x - ChunkSize/2f) + x, (int)(ChunkWorldPosition.y - ChunkSize/2f) + y);
                    
                    // Set base Perlin Variables
                    amplitude = PerlinBaseAmplitude;
                    float freq = 1;
                    float noiseHeight = 0;
                    // Get Perlin Map
                    for (int i = 0; i < PerlinOctaves; i++)
                    {
                        // Set X & Y with chunk offsets
                        float px = (float)(cellPos.x + xOffset) / PerlinScale * freq + xOffset;
                        float py = (float)(cellPos.y + yOffset) / PerlinScale * freq + yOffset;

                        // Set Temp Sample For Testing (This will change for Map Data (Hills and Water) later)
                        float2 pos = new float2(px, py);
                        float PerlinValue = Unity.Mathematics.noise.snoise(pos) * Unity.Mathematics.noise.cnoise(pos);//Mathf.PerlinNoise(px, py) * 2 - 1;
                        PerlinValue /= Unity.Mathematics.noise.cellular(pos).x;
                        noiseHeight += PerlinValue * amplitude;

                        // Increase amp and freq
                        amplitude *= persistence;
                        freq *= lacunarity;
                    }

                    // Adjust Min and Max
                    if (noiseHeight > maxNoiseHeight){
                        maxNoiseHeight = noiseHeight;
                    } else if (noiseHeight < minNoiseHeight){
                        minNoiseHeight = noiseHeight;
                    }
                    
                    // Set Sample for Chunk
                    Output[x * ChunkSize + y] = noiseHeight;// + (1.0f - Math.Abs(noiseHeight));
                }
            }

            // Normalize Sample to fit world Sample Height
            for (int x = 0; x < ChunkSize; x++)
            {
                for (int y = 0; y < ChunkSize; y++)
                {
                    // Normalize cell and force down
                    float normalizedHeight = (Output[x * ChunkSize + y] + 1) / (2f * maxPossibleHeight / 2.75f);
                    // Clamp to Height
                    Output[x * ChunkSize + y] = Mathf.Clamp(normalizedHeight, 0, int.MaxValue);
                }
            }
        }
    }
}
