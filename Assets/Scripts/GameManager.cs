using System;
using UnityEngine;
using UnityEngine.Events;

public class GameManager : MonoBehaviour
{
    public static GameManager Instance { get; private set; }

    [Header("Stato Attuale")]
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

    private void Awake()
    {
        if (Instance != null && Instance != deathInstanceCheck(this))
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
    }

    private static GameManager deathInstanceCheck(GameManager current) => Instance;

    private void Start()
    {
        // 1. Iniziamo sempre dalla calibrazione
        ChangeState(GameState.Calibration);
    }

    public void ChangeState(GameState newState)
    {
        if (currentState == newState) return;

        ExitState(currentState);
        currentState = newState;
        EnterState(newState);

        OnStateChanged?.Invoke(newState);
        Debug.Log($"[GameManager] Nuovo Stato: {newState}");
    }

    #region Gestione Ingressi negli Stati

    private void EnterState(GameState state)
    {
        switch (state)
        {
            case GameState.Calibration:
                if (calibrationUI) calibrationUI.SetActive(true);
                // Avvia Meta Scene API o raycast plane detection per il tavolo
                break;

            case GameState.ModeSelect:
                if (modeSelectUI) modeSelectUI.SetActive(true);
                break;

            case GameState.Sandbox:
                if (sandboxUI) sandboxUI.SetActive(true);
                // Attiva spawner libero di tutti gli atomi
                break;

            case GameState.MissionInitialize:
                SetupCurrentMission();
                // Una volta caricati asset e vassoio, passa automaticamente all'assemblaggio
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
                if (missionCompleteUI) missionCompleteUI.SetActive(true);
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
                if (calibrationUI) calibrationUI.SetActive(false);
                break;
            case GameState.ModeSelect:
                if (modeSelectUI) modeSelectUI.SetActive(false);
                break;
            case GameState.Sandbox:
                if (sandboxUI) sandboxUI.SetActive(false);
                break;
            case GameState.MissionComplete:
                if (missionCompleteUI) missionCompleteUI.SetActive(false);
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
            // Formula errata: puoi dare feedback sonoro/visivo e mostrare riprova
            Debug.Log("[GameManager] Formula non corretta!");
            ChangeState(GameState.MissionComplete);
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