using UnityEngine;

[CreateAssetMenu(menuName = "Molecole/Lista livelli", fileName = "ListaLivelli")]
public class LevelList : ScriptableObject
{
    [SerializeField] private SingleLevel[] livelli;

    private int indiceCorrente;

    public int Count => livelli.Length;
    public int IndiceCorrente => indiceCorrente;

    public SingleLevel CurrentLevel =>
        (livelli != null && livelli.Length > 0) ? livelli[indiceCorrente] : null;

    public void Reset() => indiceCorrente = 0;

    // Passa al livello successivo; a fine lista torna al primo
    public SingleLevel Next()
    {
        if (livelli == null || livelli.Length == 0) return null;
        indiceCorrente = (indiceCorrente + 1) % livelli.Length;
        return CurrentLevel;
    }

    private void OnEnable() => indiceCorrente = 0;
}