public enum GameState
{
    Calibration,          // 1. Rilevamento tavolo reale / Setup altezza
    ModeSelect,           // 2. Scelta modalità: Campagna vs Sandbox
    Sandbox,              // 3A. Modalità libera / Codex
    MissionInitialize,    // 3B. Caricamento ricetta/ghost e vassoio atomi
    AssemblyInProgress,   // 4. Manipolazione, snap, ascolto podio
    Validation,           // 5. Controllo chimico/geometrico
    RewardAndFeedback,    // 6. VFX, audio, sblocco Codex
    MissionComplete       // 7. UI vittoria / riprova / avanti
}