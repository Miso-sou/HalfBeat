using System.Collections.Generic;
using UnityEngine;

using System.Collections;

// Add this line to the top of your scripts to easily separate logic
public enum SaberColor { Red, Blue }

public class BeatSaber : MonoBehaviour
{
    [Header("References")]
    public Transform hitTransform; // An empty object at the tip of your sword
    
    // Link this in inspector to the Haptic Impulse Player on your hand!
    public UnityEngine.XR.Interaction.Toolkit.Inputs.Haptics.HapticImpulsePlayer hapticPlayer; 

    [Header("Saber Settings")]
    public SaberColor saberColor; // "Red" or "Blue"
    public LayerMask sliceableLayer; // Set this to the layer your Cubes are on
    
    [Header("Physical Dimensions")]
    public Vector3 raycastDirection = Vector3.up; // Standard Unity Cylinder is "Up"
    public float saberLength = 1f;
    public float bladeRadius = 0.05f;

    [Header("Debugging")]
    public bool ignoreDirectionRequirement = false; // Makes it easier to test

    private const int posBufferFrames = 3;
    private Queue<Vector3> previousPositions = new Queue<Vector3>();
    private bool hasHitLastFrame = false; // Prevents multiple hits on the same frame

    private void TriggerHapticPulse(float amplitude, float duration)
    {
        if (hapticPlayer != null) 
        {
            hapticPlayer.SendHapticImpulse(amplitude, duration);
        }
    }

    void Start()
    {
        for (int i = 0; i < posBufferFrames; i++)
        {
            previousPositions.Enqueue(transform.position);
        }
    }

    // LateUpdate ensures VR headset tracking has completely applied its movement for this frame
    void LateUpdate()
    {
        RaycastHit hit;
        
        // The actual direction the saber blade is pointing in world space
        Vector3 worldDirection = transform.TransformDirection(raycastDirection.normalized);

        // Calculate how fast the saber is moving by looking back a few frames
        Vector3 oldestPos = previousPositions.Peek();
        Vector3 swingVelocity = transform.position - oldestPos;

        // Use a SphereCast to give the blade a thick hitbox (much harder to miss fast swings)
        if (Physics.SphereCast(transform.position, bladeRadius, worldDirection, out hit, saberLength, sliceableLayer))
        {
            // We hit something on the cube layer! Let's check if it's a BeatCube
            BeatCube cubeScript = hit.collider.GetComponentInParent<BeatCube>();
            if (cubeScript != null && !hasHitLastFrame)
            {
                hasHitLastFrame = true;
                // Trigger haptics instantly! Intensity 0.7, Duration 0.1s
                TriggerHapticPulse(0.7f, 0.1f);
                // Assuming moveDirection should be worldDirection based on original GetHitBySaber signature
                cubeScript.GetHitBySaber(this, swingVelocity, worldDirection, hit.point); 
                StartCoroutine(HitStopRoutine()); // Freeze for a split second for "Punch"
            }
        }
        else
        {
            hasHitLastFrame = false; // Reset hit flag if nothing is hit
        }

        // Store this frame's position for next frame's velocity calculation
        previousPositions.Enqueue(transform.position);
        if (previousPositions.Count > posBufferFrames)
        {
            previousPositions.Dequeue();
        }
    }

    void OnDrawGizmos()
    {
        // Draw the saber blade in the Scene view so you can visually verify its length and thickness
        Gizmos.color = saberColor == SaberColor.Red ? Color.red : Color.blue;
        Vector3 worldDirection = transform.TransformDirection(raycastDirection.normalized);
        Gizmos.DrawRay(transform.position, worldDirection * saberLength);
        Gizmos.DrawWireSphere(transform.position + worldDirection * saberLength, bladeRadius);
    }
    
    private IEnumerator HitStopRoutine()
    {
        // Hitstop is a classic game feel trick where time slows down for literally a 
        // fraction of a split second when a heavy blow connects.
        Time.timeScale = 0.1f;
        yield return new WaitForSecondsRealtime(0.05f); // Wait in REAL time so it doesn't take forever
        Time.timeScale = 1.0f;
    }
}
