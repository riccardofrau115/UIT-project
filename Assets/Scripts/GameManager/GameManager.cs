using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.Serialization;
using TMPro;

public class GameManager : MonoBehaviour
{
    public static GameManager Instance { get; private set; }

    [Header("Stato Attuale")]
    [SerializeField] private GameState startState = GameState.Start;
    [SerializeField] private GameState currentState;
    [SerializeField] private MissionPhase missionPhase; // valida solo in GameState.Mission
    public GameState CurrentState => currentState;
    public MissionPhase CurrentPhase => missionPhase;

    [Header("Riferimenti Moduli (menu con CanvasGroupFader)")]
    [SerializeField] private CanvasGroupFader startUI;
    [SerializeField] private CanvasGroupFader calibrationUI;
    [SerializeField] private CanvasGroupFader modeSelectUI;
    [SerializeField] private CanvasGroupFader periodicTableUI;
    [SerializeField] private CanvasGroupFader missionStart;
    [SerializeField] private CanvasGroupFader missionCompleted;
    [SerializeField] private CanvasGroupFader additionalInfoUI;

    [SerializeField] private Transform tableAnchor;              // Ancoraggio sul tavolo calibrato


    [Header("Livelli")]
    [SerializeField] private LevelList levelList;
    [SerializeField] private TMP_Text titoloMissioneText;
    [SerializeField] private TMP_Text richiestaGenericaText;
    [SerializeField] private TMP_Text richiestaSpecificaText;

    [Header("Eventi")]
    public UnityEvent<GameState> OnStateChanged;
    public UnityEvent<MissionPhase> OnMissionPhaseChanged;

    [Header("Missione")]
    [SerializeField] private float rewardDuration = 2f;

    private int levelIndex;
    private SingleLevel CurrentLevel => levelList ? levelList.CurrentLevel : null;

    private CanvasGroupFader[] allUI;

    // true dopo la prima transizione: evita che l'early return di Transition()
    // blocchi l'avvio quando startState coincide con il valore di default dell'enum
    private bool hasState;

    // Cambi di stato/fase richiesti mentre ne è in corso un altro (es. da EnterState):
    // vengono eseguiti in coda, nell'ordine di richiesta, così gli eventi scattano nell'ordine corretto
    private bool isProcessing;
    private readonly Queue<System.Action> pending = new Queue<System.Action>();

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;

        allUI = new[] { startUI, calibrationUI, modeSelectUI, periodicTableUI, missionStart, missionCompleted, additionalInfoUI };
    }

    private void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }

    private void Start()
    {
        // Parte da tutti i menu nascosti, indipendentemente da come sono in scena
        foreach (var ui in allUI) SetUI(ui, false, true);
        ChangeState(startState);
    }

    #region Coda transizioni

    public void ChangeState(GameState newState) => Enqueue(() => Transition(newState));

    // Valido solo mentre si è in GameState.Mission (controllato al momento dell'esecuzione)
    public void ChangeMissionPhase(MissionPhase newPhase) => Enqueue(() => PhaseTransition(newPhase));

    private void Enqueue(System.Action transition)
    {
        pending.Enqueue(transition);
        if (isProcessing) return;

        isProcessing = true;
        try
        {
            while (pending.Count > 0)
                pending.Dequeue().Invoke();
        }
        finally
        {
            isProcessing = false;
            pending.Clear();
        }
    }

    private void Transition(GameState newState)
    {
        if (hasState && currentState == newState) return;

        if (hasState) ExitState(currentState);
        currentState = newState;
        hasState = true;

        // Si entra sempre in Mission dalla fase Setup (evita UI residua della missione precedente)
        if (newState == GameState.Mission) missionPhase = MissionPhase.Setup;

        RefreshUI();
        EnterState(newState);

        OnStateChanged?.Invoke(newState);
        Debug.Log($"[GameManager] Nuovo Stato: {newState}");
    }

    private void PhaseTransition(MissionPhase newPhase)
    {
        if (currentState != GameState.Mission || missionPhase == newPhase) return;

        missionPhase = newPhase;

        RefreshUI();
        EnterMissionPhase(newPhase);

        OnMissionPhaseChanged?.Invoke(newPhase);
        Debug.Log($"[GameManager] Nuova Fase Missione: {newPhase}");
    }

    #endregion

    #region Gestione UI

    // Unico punto in cui si mostra/nasconde un menu: usa CanvasGroupFader, non SetActive
    private static void SetUI(CanvasGroupFader ui, bool visible, bool instant = false)
    {
        if (ui) ui.SetVisible(visible, instant);
    }

    // Applica a tutti i menu la visibilità dello stato/fase corrente.
    // I menu condivisi (tavola periodica) restano visibili tra Sandbox e Mission senza essere nascosti e riaccesi.
    private void RefreshUI()
    {
        foreach (var ui in allUI)
            SetUI(ui, IsVisible(ui));
    }

    // Mappa stato/fase -> menu visibili
    private bool IsVisible(CanvasGroupFader ui)
    {
        switch (currentState)
        {
            case GameState.Start: return ui == startUI;
            case GameState.Calibration: return ui == calibrationUI;
            case GameState.ModeSelect: return ui == modeSelectUI;
            case GameState.Sandbox: return ui == periodicTableUI;

            case GameState.Mission:
                switch (missionPhase)
                {
                    case MissionPhase.Setup:
                        return ui == missionStart;
                    case MissionPhase.Assembly:
                        return ui == periodicTableUI || ui == additionalInfoUI;
                    case MissionPhase.Complete:
                        return ui == missionCompleted;
                }
                return false;
        }
        return false;
    }

    #endregion

    #region Gestione Ingressi / Uscite Stati

    private void EnterState(GameState state)
    {
        switch (state)
        {
            case GameState.Calibration:
                // Avvia Meta Scene API o raycast plane detection per il tavolo
                break;

            case GameState.Sandbox:
                // Attiva spawner libero di tutti gli atomi
                break;

            case GameState.Mission:
                // Fase Setup già impostata da Transition(); la UI è già aggiornata
                EnterMissionPhase(missionPhase);
                OnMissionPhaseChanged?.Invoke(missionPhase);
                break;
        }
    }

    private void ExitState(GameState state)
    {
        switch (state)
        {
            case GameState.Mission:
                // Uscendo dalla missione l'Invoke di fine Reward non deve scattare
                CancelInvoke(nameof(GoToMissionComplete));
                break;
        }
    }

    #endregion

    #region Gestione Fasi Missione

    public void StartAssembly() => ChangeMissionPhase(MissionPhase.Assembly);

    private void EnterMissionPhase(MissionPhase phase)
    {
        switch (phase)
        {
            case MissionPhase.Setup:
                SetupCurrentMission();
                break;

            case MissionPhase.Assembly:
                // Abilita interazioni sui componenti del vassoio e abilita il Podio
                break;


            case MissionPhase.Complete:
                // UI di completamento mostrata da RefreshUI()
                break;
        }
    }



    private void OnEnable() => Atomo.OnLegameCreato += CheckMoleculeSolution;
    private void OnDisable() => Atomo.OnLegameCreato -= CheckMoleculeSolution;


    private void CheckMoleculeSolution(Atomo origine = null)
    {
        // Controlla solo durante l'assemblaggio di una missione
        Debug.Log("[GameManager] Controllo Molecola");
        if (currentState != GameState.Mission || missionPhase != MissionPhase.Assembly) return;
        if (CurrentLevel == null || origine == null) return;
        Debug.Log("Livello corrente: " + CurrentLevel.nomeMolecola + " - Origine: " + origine.nomeElemento);
        var molecola = origine.OttieniMolecola();

        if (CurrentLevel.Verifica(molecola))
        {
            PlaySuccessEffects();
        }
        else if (molecola.TrueForAll(a => a.ValenzeLibere == 0))
        {
            // Molecola chiusa ma sbagliata: errore definitivo
            Debug.Log("[GameManager] Molecola completa ma non corretta!");
            // TODO: feedback sonoro/visivo
        }
        // Altrimenti è solo incompleta: nessun errore, si continua ad assemblare
    }

    private void PlaySuccessEffects()
    {
        // TODO: Attiva Audio, Particellari, Sblocco Codex
        Invoke(nameof(GoToMissionComplete), rewardDuration);
    }

    private void GoToMissionComplete() => ChangeMissionPhase(MissionPhase.Complete);

    #endregion

    #region Trigger ed Eventi Pubblici

    // Chiamato quando il tavolo è stato agganciato o confermato dall'utente
    public void ConfirmCalibration(Vector3 tablePos, Quaternion tableRot)
    {
        if (tableAnchor)
        {
            tableAnchor.position = tablePos;
            tableAnchor.rotation = tableRot;
        }
        if (currentState == GameState.Calibration)
            ChangeState(GameState.ModeSelect);
    }

    public void SelectSandboxMode() => ChangeState(GameState.Sandbox);
    public void SelectCampaignMode()
    {
        levelList.Reset();
        ChangeState(GameState.Mission);
    }

    // Chiamato dai pulsanti in MissionComplete UI
    public void NextLevel()
    {
        if (levelList.Next() == null)
        {
            Debug.Log("[GameManager] Nessun livello successivo: torna a scelta modalità");
            ChangeState(GameState.ModeSelect);
            return;
        }
        else
        {
            
        }
        ChangeMissionPhase(MissionPhase.Setup);
    }

    public void RetryLevel()
    {
        ChangeMissionPhase(MissionPhase.Setup);
    }

    // Uscita da Sandbox o Mission verso la scelta modalità
    public void GoToModeSelect()
    {
        ChangeState(GameState.ModeSelect);
    }

    public void GoToCalibration()
    {
        ChangeState(GameState.Calibration);
    }

    private void SetupCurrentMission()
    {
        PulisciScena();

        var level = CurrentLevel;
        if (level == null) { Debug.LogWarning("[GameManager] Nessun livello valido"); return; }

        if (titoloMissioneText) titoloMissioneText.text = $"Livello {levelList.IndiceCorrente + 1}: {level.nomeMolecola}";
        if (richiestaGenericaText) richiestaGenericaText.text = level.richiestaGenerica;
        if (richiestaSpecificaText) richiestaSpecificaText.text = level.richiestaSpecifica;
    }

    // Rimuove le molecole del livello precedente (adatta a come gestisci gli atomi sciolti)
    private void PulisciScena()
    {
        foreach (var c in FindObjectsByType<ContenitoreMolecola>(FindObjectsSortMode.None))
            c.DistruggiMolecola();
    }

    #endregion
}