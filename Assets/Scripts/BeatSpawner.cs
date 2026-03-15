using System.Collections.Generic;
using System.IO; // Required for file reading and writing
using UnityEngine;

public class BeatSpawner : MonoBehaviour
{
    [Header("Player Tracking")]
    [Tooltip("Enter the player's name to record their misses in the Excel sheet.")]
    public string playerName = "Player 1";

    // public GameObject EndLevelUI;

    [Header("Level Asset")]
    [Tooltip("Drag the Beatmap Asset you created here!")]
    public BeatmapAsset currentLevel;

    [Header("Audio Player")]
    public AudioSource beatMusic; // Drag your spawner's audio source here!

    [Header("Spawner Settings")]
    public Vector3 customSpawnOrigin = new Vector3(0, 1.5f, 20f); 
    public float cubeMoveSpeed = 5f; // Used to calculate when to SPAWN so it ARRIVES at the exact spawnTime
    
    private int nextBlockIndex = 0; // Tracks which block is next

    [Header("Cube Prefab")]
    public GameObject beatCubePrefab; 
    
    
    [Header("Visuals (Materials)")]
    public Material redCubeMaterial;
    public Material blueCubeMaterial;

    private int totalBlocks;
    /// <summary>Cubes spawned this run that are still in play (not hit/missed yet).</summary>
    private int outstandingCubes;
    private Dictionary<string, int> missCounters = new Dictionary<string, int>();
    private Dictionary<string, int> hitCounters  = new Dictionary<string, int>();
    private bool levelCompleteShown;

    void Start()
    {
        nextBlockIndex = 0;
        levelCompleteShown = false;
        outstandingCubes = 0;

        if (currentLevel != null)
        {
            totalBlocks = (currentLevel.blocks != null) ? currentLevel.blocks.Count : 0;
            missCounters.Clear();
            hitCounters.Clear();
        }

        if (currentLevel != null && beatMusic != null && currentLevel.songInfo != null)
        {
            beatMusic.clip = currentLevel.songInfo;
            beatMusic.spatialBlend = 0f;
            beatMusic.Play();
        }
    }

    void Update()
    {
        if (currentLevel == null) return;
        
        // If we ran out of blocks in the list, stop checking!
        if (nextBlockIndex >= currentLevel.blocks.Count) return;

        float currentTime = (beatMusic != null && beatMusic.isPlaying) ? beatMusic.time : Time.time;
        float adjustedTime = currentTime - currentLevel.songOffsetSeconds;

        // Spawn the cube exactly at the time authored in the beatmap window.
        // Here, BeatData.spawnTime is treated as the SPAWN moment, not the hit moment.
        float triggerTime = currentLevel.blocks[nextBlockIndex].spawnTime;

        if (adjustedTime >= triggerTime)
        {
            SpawnCube(currentLevel.blocks[nextBlockIndex]);
            nextBlockIndex++; // Move to the next block in the list
        }
    }

    /// <summary>
    /// Called by BeatCube when it is successfully hit by the saber.
    /// </summary>
    public void OnBlockHit(string name)
    {
        if (outstandingCubes <= 0)
            return;

        string key = string.IsNullOrEmpty(name) ? "(unnamed)" : name;
        if (hitCounters.ContainsKey(key))
            hitCounters[key]++;
        else
            hitCounters[key] = 1;

        outstandingCubes--;
        TryCompleteLevel();
    }

    public void OnBlockMissed(string name)
    {
        if (outstandingCubes <= 0)
            return;

        string key = string.IsNullOrEmpty(name) ? "(unnamed)" : name;
        if (missCounters.ContainsKey(key))
            missCounters[key]++;
        else
            missCounters[key] = 1;

        Debug.Log($"[Miss] \"{key}\" missed! (total misses for this name: {missCounters[key]})");

        outstandingCubes--;
        TryCompleteLevel();
    }

    /// <summary>
    /// Level ends only after every beatmap block has spawned AND every spawned cube has been hit or missed.
    /// </summary>
    private void TryCompleteLevel()
    {
        if (levelCompleteShown || currentLevel == null || currentLevel.blocks == null)
            return;

        bool allSpawned = nextBlockIndex >= currentLevel.blocks.Count;
        if (!allSpawned || outstandingCubes > 0)
            return;

        levelCompleteShown = true;

        // Collect every cube name that appeared in either counter
        var allKeys = new System.Collections.Generic.HashSet<string>(missCounters.Keys);
        foreach (var k in hitCounters.Keys) allKeys.Add(k);

        Debug.Log("========== LEVEL FINISHED ==========");
        if (missCounters.Count == 0)
        {
            Debug.Log("Perfect run! No misses.");
        }
        else
        {
            foreach (var key in allKeys)
            {
                int hits   = hitCounters.ContainsKey(key)  ? hitCounters[key]  : 0;
                int misses = missCounters.ContainsKey(key) ? missCounters[key] : 0;
                int total  = hits + misses;
                Debug.Log($"  \"{key}\" — total: {total}  |  hits: {hits}  |  misses: {misses}");
            }
        }
        Debug.Log("=====================================");

        // --- Export data to CSV Matrix ---
        SaveMissLogsToExcel();

        // If you have an EndLevelUI script, it gets called here.
        // If not, it just prints the warning below.
        if (EndLevelUI.Instance != null)
            EndLevelUI.Instance.ShowLevelFinished();
        else
            Debug.LogWarning("[BeatSpawner] EndLevelUI.Instance is null.");
    }

    /// <summary>
    /// Reads existing CSV, updates the player's row with new miss data, 
    /// dynamically creates new columns for new cube names, and rewrites the file.
    /// </summary>
    private void SaveMissLogsToExcel()
    {
        if (missCounters.Count == 0) return; // Skip if perfect run (optional)

        string filePath = Application.persistentDataPath + "/PlayerMissMatrix.csv";
        string safePlayerName = string.IsNullOrEmpty(playerName) ? "Unknown" : playerName.Replace(",", "");

        // 1. Setup dictionaries to hold our grid data in memory
        Dictionary<string, Dictionary<string, int>> allData = new Dictionary<string, Dictionary<string, int>>();
        List<string> allCubeNames = new List<string>();

        // 2. Read existing file to remember previous players and columns
        if (File.Exists(filePath))
        {
            string[] lines = File.ReadAllLines(filePath);
            if (lines.Length > 0)
            {
                // Parse the top row (Headers)
                string[] headers = lines[0].Split(',');
                for (int i = 1; i < headers.Length; i++) 
                {
                    if (!allCubeNames.Contains(headers[i]))
                        allCubeNames.Add(headers[i]);
                }

                // Parse the player rows
                for (int i = 1; i < lines.Length; i++)
                {
                    string[] cols = lines[i].Split(',');
                    if (cols.Length > 0)
                    {
                        string pName = cols[0];
                        allData[pName] = new Dictionary<string, int>();
                        
                        // Fill in their previous misses
                        for (int j = 1; j < cols.Length && j < headers.Length; j++)
                        {
                            if (int.TryParse(cols[j], out int count))
                            {
                                allData[pName][headers[j]] = count;
                            }
                        }
                    }
                }
            }
        }

        // 3. Add the current run's data for the active player
        if (!allData.ContainsKey(safePlayerName))
        {
            allData[safePlayerName] = new Dictionary<string, int>();
        }

        foreach (var kvp in missCounters)
        {
            string safeBlockName = kvp.Key.Replace(",", "");
            
            // If this is a cube no one has missed before, add it as a new column
            if (!allCubeNames.Contains(safeBlockName))
            {
                allCubeNames.Add(safeBlockName);
            }

            // Accumulate misses (if they play multiple times, this adds to their total)
            if (allData[safePlayerName].ContainsKey(safeBlockName))
            {
                allData[safePlayerName][safeBlockName] += kvp.Value; 
            }
            else
            {
                allData[safePlayerName][safeBlockName] = kvp.Value;
            }
        }

        // 4. Overwrite the file with the brand new grid
        using (StreamWriter writer = new StreamWriter(filePath, false)) // false = overwrite
        {
            // Write Header Row (Player Name, Cube1, Cube2, etc.)
            string headerLine = "Player Name";
            foreach (string cube in allCubeNames)
            {
                headerLine += "," + cube;
            }
            writer.WriteLine(headerLine);

            // Write Data Rows for every player
            foreach (var player in allData.Keys)
            {
                string rowLine = player;
                foreach (string cube in allCubeNames)
                {
                    int count = 0;
                    if (allData[player].ContainsKey(cube))
                    {
                        count = allData[player][cube];
                    }
                    rowLine += "," + count; // Fills 0 if they never missed this specific cube
                }
                writer.WriteLine(rowLine);
            }
        }

        Debug.Log($"<color=green>[Excel Log]</color> Matrix successfully saved to: {filePath}");
    }

    void SpawnCube(BeatData data)
    {
        if (beatCubePrefab == null) return;

        // 1. Convert GridPos enum to actual X/Y coordinates 
        Vector3 spawnOffset = Vector3.zero;
        string posName = data.position.ToString();
        
        // X Position (Columns 1 to 4)
        if (posName.Contains("Col1")) spawnOffset.x = -0.9f;
        else if (posName.Contains("Col2")) spawnOffset.x = -0.3f;
        else if (posName.Contains("Col3")) spawnOffset.x = 0.3f;
        else if (posName.Contains("Col4")) spawnOffset.x = 0.9f;
        
        // Y Position (Rows 0 to 2)
        if (posName.Contains("Row2")) spawnOffset.y = 1.3f; // Top
        else if (posName.Contains("Row1")) spawnOffset.y = 0.7f; // Middle
        else if (posName.Contains("Row0")) spawnOffset.y = 0.1f; // Bottom

        Vector3 finalSpawnPos = customSpawnOrigin + spawnOffset;

        // 2. Convert CutDirection enum to an exact Z-axis rotation angle
        float zRot = 0;
        switch (data.direction)
        {
            case CutDirection.Down: zRot = 180f; break;
            case CutDirection.Up: zRot = 0f; break;
            case CutDirection.Right: zRot = -90f; break;
            case CutDirection.Left: zRot = 90f; break;
            case CutDirection.DownRight: zRot = -135f; break;
            case CutDirection.UpRight: zRot = -45f; break;
            case CutDirection.DownLeft: zRot = 135f; break;
            case CutDirection.UpLeft: zRot = 45f; break;
            case CutDirection.Any: zRot = 0f; break;
        }

        Quaternion rotation = Quaternion.Euler(0, 0, zRot);

        // 3. Instantiate the cube prefab!
        GameObject newCubeObj = Instantiate(beatCubePrefab, finalSpawnPos, rotation);

        // 4. Assign Color and Speed
        BeatCube cubeScript = newCubeObj.GetComponent<BeatCube>();
        MeshRenderer mr = newCubeObj.GetComponent<MeshRenderer>();

        if (cubeScript != null)
        {
            cubeScript.requiredColor = data.color;
            cubeScript.moveSpeed = cubeMoveSpeed;
            cubeScript.cubeName = data.cubeName;
            
            if (data.direction == CutDirection.Any) cubeScript.ignoreDirectionRequirement = true;
        }

        if (mr != null)
        {
            mr.material = (data.color == SaberColor.Red) ? redCubeMaterial : blueCubeMaterial;
        }

        // Parent it to the spawner for a clean hierarchy
        newCubeObj.transform.SetParent(this.transform);
        outstandingCubes++;
    }
}