using UnityEngine;

public class Spawner : MonoBehaviour
{
    [SerializeField] private GameObject prefab;
    [SerializeField] private Transform spawnPoint;

    public void Spawn()
    {
        if (prefab == null)
        {
            UnityEngine.Debug.LogWarning($"Prefab non assegnato a {gameObject.name}");
            return;
        }

        if (spawnPoint == null)
        {
            UnityEngine.Debug.LogWarning($"Spawn Point non assegnato a {gameObject.name}");
            return;
        }

        Instantiate(
            prefab,
            spawnPoint.position,
            spawnPoint.rotation
        );
    }
}