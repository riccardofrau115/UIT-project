using UnityEngine;
using TMPro;

public class UIOrdineLegame : MonoBehaviour
{
    [SerializeField] private TMP_Text testo;
    
    private void Update()
    {
        int ordine = GestoreOrdineLegame.Instance?.OrdineRichiesto ?? 1;
        testo.text = $"Ordine legame: {ordine}";
    }

}
