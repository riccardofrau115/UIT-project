using UnityEngine;

public class StateTrigger : MonoBehaviour
{
    [SerializeField] private GameState targetState;

    // Collegalo all'evento del bottone (OnClick o On Value Changed)
    public void Go()
    {
        if (GameManager.Instance != null)
            GameManager.Instance.ChangeState(targetState);
    }
}