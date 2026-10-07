using System;
using System.Collections.Generic;
using UnityEngine;


public class ParticleListPopulator : MonoBehaviour
{
    [SerializeField] TextAsset json;            // trascina particles.json
    [SerializeField] ElementView buttonTemplate; // prefab buttonTemplate
    [SerializeField] Transform container;         // il container della lista
    [SerializeField] Transform spawnPoint;   // oggetto di scena, assegnato da inspector qui

    void Start()
    {
        var data = JsonUtility.FromJson<ParticleList>(json.text);

        foreach (var a in data.Particles)
        {
            var sprite = Resources.Load<Sprite>(a.icon);
            var prefab = Resources.Load<GameObject>(a.prefab);

            Debug.Log($"Caricata particella {a.name} con icona {a.icon} e prefab {a.prefab}");

            if (sprite == null || prefab == null)
            {
                Debug.LogWarning($"Risorse mancanti per {a.icon} o {a.prefab}");
                continue;
            }



            var view = Instantiate(buttonTemplate, container);
            Debug.Log($"Instanziato ElementView per {a.name} in {container.name}");
            view.Setup(sprite, $"{a.id} - {a.name}", prefab, spawnPoint);
            Debug.Log($"Setup completato per ElementView di {a.name}");
        }
    }
}