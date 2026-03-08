using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class BeatSpawner : MonoBehaviour
{
    [Header("Spawner Settings")]
    public float beatsPerMinute = 130f;
    public Vector3 customSpawnOrigin = new Vector3(0, 1.5f, 20f); // Make the exact spawn coordinate completely editable
    
    [Header("Audio Synchronization")]
    public AudioSource beatMusic; // Drag your audio source here!
    public float songOffsetSeconds = 0f; // Shift if the song has an intro before the beats start

    [Header("Cube Prefab")]
    public GameObject beatCubePrefab; // Drag your single generic BeatCube prefab here
    
    [Header("Visuals (Materials)")]
    public Material redCubeMaterial;
    public Material blueCubeMaterial;

    // A 2x2 grid is standard in Beat Saber (Top Left, Top Right, Bottom Left, Bottom Right)
    // These offsets are relative to the customSpawnOrigin
    private Vector3[] gridPositions = new Vector3[]
    {
        new Vector3(-0.5f, -0.3f, 0), // Bottom Left
        new Vector3( 0.5f, -0.3f, 0), // Bottom Right
        new Vector3(-0.5f,  0.3f, 0), // Top Left
        new Vector3( 0.5f,  0.3f, 0)  // Top Right
    };

    private float beatInterval;
    private float lastBeatTime = 0f;

    void Start()
    {
        // Calculate how many seconds pass between each beat
        // Example: 130 BPM = 60 / 130 = ~0.46 seconds per beat
        beatInterval = 60f / beatsPerMinute; 
        
        if (beatMusic != null)
        {
            beatMusic.spatialBlend = 0f; // Force 2D audio so it can be heard loud and clear from anywhere
            beatMusic.Play(); // Make sure the music starts!
        }
    }

    void Update()
    {
        // If we don't have music attached, fallback to a simple timer
        float currentTime = (beatMusic != null && beatMusic.isPlaying) ? beatMusic.time : Time.time;
        
        // Subtract song offset to align the first beat
        float adjustedTime = currentTime - songOffsetSeconds;

        // Check if we have crossed the threshold for the next beat
        if (adjustedTime >= lastBeatTime + beatInterval)
        {
            SpawnNextCube();
            
            // Advance the beat tracker. 
            // We += instead of just setting to exact time so it never drifts out of sync!
            lastBeatTime += beatInterval; 
        }
    }

    void SpawnNextCube()
    {
        if (beatCubePrefab == null) return;

        // 1. Pick a random position from our 2x2 grid
        Vector3 spawnOffset = gridPositions[Random.Range(0, gridPositions.Length)];
        
        // Spawn them exactly at the custom origin coordinate + the grid slot
        Vector3 finalSpawnPos = customSpawnOrigin + spawnOffset;

        // 2. Pick a random rotation (Up, Down, Left, Right arrow directions)
        int randRotation = Random.Range(0, 4);
        Quaternion rotation = Quaternion.Euler(0, 0, randRotation * 90f);

        // 3. Instantiate the cube prefab!
        GameObject newCubeObj = Instantiate(beatCubePrefab, finalSpawnPos, rotation);

        // 4. Randomly assign it Red or Blue
        BeatCube cubeScript = newCubeObj.GetComponent<BeatCube>();
        MeshRenderer mr = newCubeObj.GetComponent<MeshRenderer>();

        if (Random.value > 0.5f)
        {
            // Make it Red
            if (cubeScript != null) cubeScript.requiredColor = SaberColor.Red;
            if (mr != null && redCubeMaterial != null) mr.material = redCubeMaterial;
        }
        else
        {
            // Make it Blue
            if (cubeScript != null) cubeScript.requiredColor = SaberColor.Blue;
            if (mr != null && blueCubeMaterial != null) mr.material = blueCubeMaterial;
        }

        // Parent it to the spawner for a clean hierarchy
        newCubeObj.transform.SetParent(this.transform);
    }
}
