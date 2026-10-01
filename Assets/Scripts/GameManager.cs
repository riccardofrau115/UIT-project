using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;

public class GameManager : MonoBehaviour
{
    public static GameManager Instance { get; private set; }

    [Header("Stato Attuale")]
    [SerializeField] private GameState startState = GameState.Calibration;
    [SerializeField] private GameState currentState;
    public GameState CurrentState => currentState;

    [Header("Riferimenti Moduli")]
    [SerializeField] private GameObject calibrationUI;
    [SerializeField] private GameObject modeSelectUI;
    [SerializeField] private GameObject sandboxUI;
    [SerializeField] private GameObject missionCompleteUI;
    [SerializeField] private Transform tableAnchor; // Ancoraggio sul tavolo calibrato

    [Header("Eventi")]
    public UnityEvent<GameState> OnStateChanged;

    // true dopo la prima transizione: evita che l'early return di Transition()
    // blocchi l'avvio quando startState coincide con il valore di default dell'enum
    private bool hasState;

    // Transizioni richieste mentre ne è in corso un'altra (es. da EnterState):
    // vengono eseguite in coda, così OnStateChanged scatta nell'ordine corretto
    private bool isTransitioning;
    private readonly Queue<GameState> pendingStates = new Queue<GameState>();

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
    }

    private void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }

    private void Start()
    {
        // Iniziamo sempre dalla calibrazione
        ChangeState(startState);
    }

    public void ChangeState(GameState newState)
    {
        if (isTransitioning)
        {
            pendingStates.Enqueue(newState);
            return;
        }

        isTransitioning = true;
        try
        {
            Transition(newState);
            while (pendingStates.Count > 0)
                Transition(pendingStates.Dequeue());
        }
        finally
        {
            isTransitioning = false;
            pendingStates.Clear();
        }
    }

    private void Transition(GameState newState)
    {
        if (hasState && currentState == newState) return;

        if (hasState) ExitState(currentState);
        currentState = newState;
        hasState = true;
        EnterState(newState);

        OnStateChanged?.Invoke(newState);
        Debug.Log($"[GameManager] Nuovo Stato: {newState}");
    }

    // Unico punto in cui si mostra/nasconde una UI: qui si può sostituire
    // SetActive con CanvasGroupFader.Show()/Hide()
    private static void SetUI(GameObject ui, bool visible)
    {
        if (ui) ui.SetActive(visible);
    }

    #region Gestione Ingressi negli Stati

    private void EnterState(GameState state)
    {
        switch (state)
        {
            case GameState.Calibration:
                SetUI(calibrationUI, true);
                // Avvia Meta Scene API o raycast plane detection per il tavolo
                break;

            case GameState.ModeSelect:
                SetUI(modeSelectUI, true);
                break;

            case GameState.Sandbox:
                SetUI(sandboxUI, true);
                // Attiva spawner libero di tutti gli atomi
                break;

            case GameState.MissionInitialize:
                SetupCurrentMission();
                // Messo in coda: parte dopo OnStateChanged(MissionInitialize)
                ChangeState(GameState.AssemblyInProgress);
                break;

            case GameState.AssemblyInProgress:
                // Abilita interazioni sui componenti del vassoio e abilita il Podio
                break;

            case GameState.Validation:
                CheckMoleculeSolution();
                break;

            case GameState.RewardAndFeedback:
                PlaySuccessEffects();
                break;

            case GameState.MissionComplete:
                SetUI(missionCompleteUI, true);
                break;
        }
    }

    #endregion

    #region Gestione Uscite dagli Stati

    private void ExitState(GameState state)
    {
        switch (state)
        {
            case GameState.Calibration:
                SetUI(calibrationUI, false);
                break;
            case GameState.ModeSelect:
                SetUI(modeSelectUI, false);
                break;
            case GameState.Sandbox:
                SetUI(sandboxUI, false);
                break;
            case GameState.RewardAndFeedback:
                // Se si esce prima dei 2 secondi, l'Invoke non deve scattare
                CancelInvoke(nameof(GoToMissionComplete));
                break;
            case GameState.MissionComplete:
                SetUI(missionCompleteUI, false);
                break;
        }
    }

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
        ChangeState(GameState.ModeSelect);
    }

    // Chiamato dai pulsanti della UI ModeSelect
    public void SelectSandboxMode() => ChangeState(GameState.Sandbox);
    public void SelectCampaignMode() => ChangeState(GameState.MissionInitialize);

    // Chiamato dal Podio quando l'utente adagia la molecola per la verifica
    public void OnMoleculeSubmitted(GameObject submittedMolecule)
    {
        if (currentState != GameState.AssemblyInProgress) return;
        ChangeState(GameState.Validation);
    }

    private void CheckMoleculeSolution()
    {
        // TODO: Inserire qui il controllo di formula / connettività nodi
        bool isFormulaCorrect = true; // Sostituire con validazione reale

        if (isFormulaCorrect)
        {
            ChangeState(GameState.RewardAndFeedback);
        }
        else
        {
            // Formula errata: feedback e ritorno all'assemblaggio per riprovare
            Debug.Log("[GameManager] Formula non corretta!");
            // TODO: feedback sonoro/visivo
            ChangeState(GameState.AssemblyInProgress);
        }
    }

    private void PlaySuccessEffects()
    {
        // TODO: Attiva Audio, Particellari, Sblocco Codex
        // Dopo un piccolo delay (es. 2 secondi), mostra la UI di completamento
        Invoke(nameof(GoToMissionComplete), 2.0f);
    }

    private void GoToMissionComplete()
    {
        if (currentState != GameState.RewardAndFeedback) return;
        ChangeState(GameState.MissionComplete);
    }

    // Chiamato dai pulsanti in MissionComplete UI
    public void NextLevel()
    {
        // Incrementa indice livello
        ChangeState(GameState.MissionInitialize);
    }

    public void RetryLevel()
    {
        ChangeState(GameState.MissionInitialize);
    }

    // Chiamato anche dalla UI Sandbox (unica uscita da quello stato)
    public void BackToMenu()
    {
        ChangeState(GameState.ModeSelect);
    }

    private void SetupCurrentMission()
    {
        // Istanzia ricetta, attiva il vassoio atomi necessario e posiziona il ghost
    }

    #endregion
}
