using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using EzySlice; // Make sure to include the EzySlice namespace

public class saber : MonoBehaviour
{
    public LayerMask layer;
    public Vector3 raycastDirection = Vector3.up; // Standard Cylinder uses Up (Y-axis)
    public float saberLength = 1f;
    public float bladeRadius = 0.05f;

    // A toggle to ignore the directional cut requirement (useful for debugging if you are just poking the block)
    public bool requireSpecificCutDirection = true;

    private Vector3 previousPos;

    // Use this for initialization
    void Start()
    {
        previousPos = transform.position;
    }

    // LateUpdate is called AFTER all Update functions and internal physics/tracking updates are finished
    void LateUpdate()
    {
        RaycastHit hit;
        
        // Calculate the direction in world space
        Vector3 worldDirection = transform.TransformDirection(raycastDirection.normalized);

        // Calculate velocity based on true movement since last frame
        Vector3 swingVelocity = transform.position - previousPos;

        // Using SphereCast instead of Raycast to give the "blade" some thickness
        if (Physics.SphereCast(transform.position, bladeRadius, worldDirection, out hit, saberLength, layer))
        {
            // We hit *something* on the specified layer
            float angle = Vector3.Angle(swingVelocity, hit.transform.up);
            
            // Debugging output
            Debug.Log($"[Saber] Hit object: {hit.transform.name}. Swing velocity: {swingVelocity.magnitude:F4}. Angle vs Cube Up: {angle:F1}");

            // Compare the swing direction to the hit object's UP vector (how the cube is rotated)
            if (!requireSpecificCutDirection || angle > 130)
            {
                Debug.Log($"[Saber] Hit condition passed! Attempting to slice {hit.transform.name}...");
                
                // 1) Find the material of the cube to fill the inside of the cut
                Material insideMaterial = null;
                MeshRenderer mr = hit.transform.GetComponent<MeshRenderer>();
                if (mr != null)
                {
                    insideMaterial = mr.material;
                }

                // 2) Calculate the cutting plane normal.
                Vector3 cutNormal = Vector3.Cross(worldDirection, swingVelocity).normalized;
                
                // If the player holds the saber still, swingVelocity could be near zero. 
                // Default to a downward cut.
                if (cutNormal == Vector3.zero) 
                {
                    cutNormal = Vector3.up; 
                }

                // 3) Use EzySlice to cut the object into two pieces
                GameObject[] parts = hit.transform.gameObject.SliceInstantiate(hit.point, cutNormal, insideMaterial);

                // Check if the slice was successful
                if (parts != null)
                {
                    Debug.Log("[Saber] EzySlice success! Applying physics to halves.");
                    // 4) Add physics to the two new parts so they fall away
                    for (int n = 0; n < parts.Length; n++)
                    {
                        // Add a mesh collider so they don't fall through the floor endlessly
                        MeshCollider mc = parts[n].AddComponent<MeshCollider>();
                        mc.convex = true;

                        Rigidbody rb = parts[n].AddComponent<Rigidbody>();
                        
                        // Push them slightly apart based on the cut normal
                        float separationDirection = (n == 0) ? 1.0f : -1.0f;
                        rb.AddForce(cutNormal * separationDirection * 150f);
                        
                        // Push them slightly backward in the direction the cube was moving
                        rb.AddForce(-hit.transform.forward * 200f);

                        // Clean up the parts after a few seconds so they don't pile up
                        Destroy(parts[n], 3f);
                    }
                }
                else
                {
                    Debug.LogWarning("[Saber] EzySlice returned null! The mesh might not have 'Read/Write Enable' checked in its Import Settings, or it's not a closed mesh. Destroying entirely as fallback.");
                }

                // 5) Destroy the original whole cube
                Destroy(hit.transform.gameObject);
            }
            else
            {
                // Un-comment the line below if you want the console spammed with angle failure logs on every hit frame, but since it's working let's keep it relatively clean.
                // Debug.Log($"[Saber] Angle ({angle:F1}) was insufficient. Must be > 130 if requireSpecificCutDirection is true.");
            }
        }

        // Update Position for next LateUpdate frame comparison
        previousPos = transform.position;
    }

    // Draw the saber blade ray in the editor for easy visualization
    void OnDrawGizmos()
    {
        Gizmos.color = Color.red;
        Vector3 worldDirection = transform.TransformDirection(raycastDirection.normalized);
        Gizmos.DrawRay(transform.position, worldDirection * saberLength);
        Gizmos.DrawWireSphere(transform.position + worldDirection * saberLength, bladeRadius);
    }
}