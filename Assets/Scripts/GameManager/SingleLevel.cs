using System.Collections.Generic;
using System.Linq;
using UnityEngine;

[System.Serializable]
public struct ElementoRichiesto
{
    [Tooltip("Stesso id del JSON delle particelle: H, O, C, N...")]
    public string id;
    [Min(1)] public int quantita;
}

[CreateAssetMenu(menuName = "Molecole/Livello", fileName = "Livello_")]
public class SingleLevel : ScriptableObject
{   
    [Header("Livello")]
    [Min(1)] public int idLivello;              // es. 1
    [Header("Molecola richiesta")]
    public string nomeMolecola;                 // es. "Acqua"
    public string formula;                      // es. "H2O" (solo per UI)
    public ElementoRichiesto[] composizione;    // es. O x1, H x2

    [Header("Richieste mostrate nella MissionUI")]
    [TextArea] public string richiestaGenerica;   // es. "Crea una molecola d'acqua"
    [TextArea] public string richiestaSpecifica;  // es. "Unisci 1 atomo di ossigeno e 2 di idrogeno"

    /// <summary>True se la molecola ha esattamente gli atomi richiesti e nessuna valenza libera.</summary>
    public bool Verifica(IEnumerable<Atomo> molecola)
    {
        if (molecola == null) return false;
        var atomi = molecola.ToList();

        // 1) composizione: stesso numero di atomi per ogni id
        var presenti = atomi.GroupBy(a => a.simbolo)
                            .ToDictionary(g => g.Key, g => g.Count());

        if (presenti.Count != composizione.Length) return false;

        foreach (var richiesto in composizione)
        {
            if (!presenti.TryGetValue(richiesto.id, out int n) || n != richiesto.quantita)
                return false;
        }

        // 2) molecola chiusa: nessuna valenza libera
        return atomi.All(a => a.ValenzeLibere == 0);
    }
}