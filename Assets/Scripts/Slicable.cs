using UnityEngine;

public class Slicable : MonoBehaviour
{
    public GameObject smashedVersion; // Assign your 2-half-cube prefab here

    private void OnTriggerEnter(Collider other)
    {
        if (other.CompareTag("Saber"))
        {
            // 1. Spawn the broken halves at current position
            GameObject broken = Instantiate(smashedVersion, transform.position, transform.rotation);

            // 2. Add an "explosion" force to make them fly apart
            foreach (Rigidbody rb in broken.GetComponentsInChildren<Rigidbody>())
            {
                rb.AddExplosionForce(500f, transform.position, 2f);
            }

            // 3. Delete the original cube
            Destroy(gameObject);
        }
    }
}