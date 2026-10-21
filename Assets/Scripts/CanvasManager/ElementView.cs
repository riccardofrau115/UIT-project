using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class ElementView : MonoBehaviour
{
    [SerializeField] private Image icon;            // .../Elements/Icon
    [SerializeField] private TMP_Text label;        // .../Elements/Text
    [SerializeField] private ParticleSpawner spawner;

    public void Setup(Sprite sprite, string text, GameObject atomPrefab, Transform spawnPoint)
    {
        Debug.Log($"Impostata vista per {text}");
        icon.sprite = sprite;
        label.text = text;
        spawner.Init(atomPrefab, spawnPoint);
    }
}