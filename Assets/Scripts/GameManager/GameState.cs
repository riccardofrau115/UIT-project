public enum GameState
{
    Start,                // 0. Avvio Gioco
    Calibration,          // 1. Rilevamento tavolo reale / Setup altezza
    ModeSelect,           // 2. Scelta modalità: Campagna vs Sandbox
    Sandbox,              // 3A. Modalità libera / Codex
    Mission,    // 3B. Caricamento ricetta/ghost e vassoio atomi

}

public enum MissionPhase 
{ 
    Setup, 
    Assembly, 
    Validation, 
    Reward, 
    Complete 
    }