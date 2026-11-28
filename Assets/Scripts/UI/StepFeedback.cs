// =====================================================================
// StepFeedback.cs
// ---------------------------------------------------------------------
// "Step done" cue: every accepted brewing click (free mode) shows a short
// toast, e.g. "Beans added", "Small cup chosen". The text comes from
// PlayerMove.DoneText, so there is one list of texts for all levels.
// Lives on the Toast object in the UI prefab, so every Day scene has it.
// The guided tutorial is skipped (Grandma's next line is the cue there).
// =====================================================================
using UnityEngine;

[RequireComponent(typeof(ToastNotification))]
public class StepFeedback : MonoBehaviour
{
    [Tooltip("Seconds each cue stays on screen.")]
    public float seconds = 1.5f;
    [Tooltip("Also show cues during the guided tutorial.")]
    public bool showInTutorial = false;
    [Tooltip("Put in front of every cue.")]
    public string prefix = "";

    private ToastNotification toast;
    private BrewingSession session;

    private void Start()
    {
        toast = GetComponent<ToastNotification>();
        session = BrewingSession.Instance;
        if (session != null) session.OnMove += HandleMove;
    }

    private void OnDestroy()
    {
        if (session != null) session.OnMove -= HandleMove;
    }

    /// <summary>Shows the cue for one move (Serve has its own panel, so it is skipped).</summary>
    private void HandleMove(PlayerMove move)
    {
        if (move.action == StepAction.Serve) return;
        if (session.IsGuided && !showInTutorial) return;
        toast.Show(prefix + move.DoneText, seconds);
    }
}
