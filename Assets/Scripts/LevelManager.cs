using UnityEngine;
using System.Collections.Generic;

public class LevelManager : MonoBehaviour
{
    public LevelData level; // Assign your notes here in the Inspector
    public GameObject cubePrefab;
    public AudioSource songSource;

    public Transform spawnPoint; // Where cubes appear
    public float spawnOffsetZ = 20f; // How far away they spawn
    public float beatsAhead = 4f; // How many beats early they spawn

    private int nextNoteIndex = 0;

    void Update()
    {
        if (!songSource.isPlaying) return;

        // 1. Calculate the current beat of the song
        float currentBeat = (songSource.time * level.bpm) / 60f;

        // 2. Check if it's time to spawn the next note
        // We spawn it 'beatsAhead' early so it can travel to the player
        if (nextNoteIndex < level.notes.Count &&
            currentBeat >= level.notes[nextNoteIndex].beat - beatsAhead)
        {
            SpawnCube(level.notes[nextNoteIndex]);
            nextNoteIndex++;
        }
    }

    void SpawnCube(NoteData data)
    {
        // Calculate the exact spawn position based on lane and layer
        Vector3 pos = spawnPoint.position;
        pos.x += data.lane * 1.0f; // 1 meter spacing
        pos.y += data.layer * 0.8f;

        GameObject cube = Instantiate(cubePrefab, pos, Quaternion.identity);

        // Pass info to the cube so it knows how to move
        CubeMovement mover = cube.GetComponent<CubeMovement>();
        mover.beatOfThisNote = data.beat;
        mover.songBPM = level.bpm;
        mover.spawnPos = pos;
        mover.removePos = new Vector3(pos.x, pos.y, 0); // Where the player stands
    }
}