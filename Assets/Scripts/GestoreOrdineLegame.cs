using UnityEngine;

public class GestoreOrdineLegame : MonoBehaviour
{
    public static GestoreOrdineLegame Instance { get; private set; }

    [SerializeField] private int ordineRichiesto = 1; // 1 singolo, 2 doppio, 3 triplo
    public int OrdineRichiesto => ordineRichiesto;
    public event System.Action<int> OnOrdineCambiato;

    private void Awake() => Instance = this;


    public void AvanzaOrdineLegame() { 
        ordineRichiesto = ordineRichiesto % 3 + 1;
        OnOrdineCambiato?.Invoke(ordineRichiesto);
    }
    
    public void ResetOrdine() { 
        ordineRichiesto = 1;
        OnOrdineCambiato?.Invoke(ordineRichiesto);
        }
}