using UnityEngine;

public class ParticleSpawner : MonoBehaviour
{
    [SerializeField] private GameObject prefab;
    [SerializeField] private Transform spawnPoint;

    // Chiamato dal popolatore dopo l'Instantiate dell'Element
    public void Init(GameObject particlePrefab, Transform point)
    {
        prefab = particlePrefab;
        spawnPoint = point;
    }

    public void Spawn()
    {
        if (prefab == null)
        {
            Debug.LogWarning($"Prefab non assegnato a {gameObject.name}");
            return;
        }

        if (spawnPoint == null)
        {
            Debug.LogWarning($"Spawn Point non assegnato a {gameObject.name}");
            return;
        }

        Instantiate(prefab, spawnPoint.position, spawnPoint.rotation);
    }
}