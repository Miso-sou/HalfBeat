using UnityEngine;
using TMPro; // Required for TextMeshPro

public class PointsPopup : MonoBehaviour
{
    public TextMeshPro textMesh; // Link your TextMeshPro component here
    public float floatSpeed = 2f;
    public float lifetime = 1f;

    public void Setup(int scoreAmount)
    {
        if (textMesh != null)
        {
            textMesh.text = scoreAmount.ToString();
            
            // Optional: Make 115 hits look cooler (pure white/yellow)
            if (scoreAmount >= 110)
            {
                textMesh.color = Color.white;
            }
        }

        // Destroy after a short time
        Destroy(gameObject, lifetime);
    }

    void Update()
    {
        // Float slowly upward
        transform.position += Vector3.up * floatSpeed * Time.deltaTime;
        
        // Face the player/camera so it's always readable
        if (Camera.main != null)
        {
            transform.rotation = Quaternion.LookRotation(transform.position - Camera.main.transform.position);
        }
    }
}
