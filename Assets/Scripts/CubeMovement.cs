using UnityEngine;

public class CubeMovement : MonoBehaviour
{
    public float beatOfThisNote;
    public float songBPM;
    public Vector3 spawnPos;
    public Vector3 removePos;

    // We need to find the audio source automatically if it's not set
    private AudioSource audioSource;

    void Start()
    {
        audioSource = GameObject.FindObjectOfType<AudioSource>();
    }

    void Update()
    {
        if (audioSource == null || !audioSource.isPlaying) return;

        float currentBeat = (audioSource.time * songBPM) / 60f;

        // This 't' goes from 0 to 1 over exactly 4 beats
        float beatsAhead = 4f;
        float t = (currentBeat - (beatOfThisNote - beatsAhead)) / beatsAhead;

        // Apply the movement
        transform.position = Vector3.Lerp(spawnPos, removePos, t);

        // Debug: If t is 0, the cube won't move. 
        // If this prints 0 in your console, the math above is wrong.
        // Debug.Log("T value: " + t);

        if (t >= 1.1f) Destroy(gameObject);
    }
}