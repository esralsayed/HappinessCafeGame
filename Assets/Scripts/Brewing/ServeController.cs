// =====================================================================
// ServeController.cs
// ---------------------------------------------------------------------
// Ends a drink with on-screen buttons instead of carrying the cup.
//
// When the player has done every step of the recipe except "Serve",
// the ServePanel (built in the UI prefab) appears and the cursor is
// freed:
//   - "Ready to serve?"  -> Serve(): the drink is served and judged by
//                           BrewingSession, and a cup of the chosen size
//                           appears on the front counter.
//   - "Trash"            -> ResetDrink(): the drink is thrown away and the
//                           same order starts again from step 1.
//                           Hidden in guided mode (Grandma's tutorial).
//
// The served cup is hidden again on the first move of the next drink.
// All UI and cups are real objects set in the Inspector (no UI in code).
// Put this on the level's Managers object, next to BrewingSession.
// =====================================================================
using UnityEngine;

public class ServeController : MonoBehaviour
{
    [Header("Serve panel (UI prefab > OrderCanvas > ServePanel)")]
    [Tooltip("The panel with the Trash and Ready to serve? buttons. Hidden until the drink is ready.")]
    public GameObject servePanel;

    [Tooltip("The Trash button. Hidden for the guided (tutorial) drink.")]
    public GameObject resetButton;

    [Header("Served cups on the front counter (Room > ... > Served Cups)")]
    public GameObject smallCup;
    public GameObject mediumCup;
    public GameObject largeCup;

    private BrewingSession session;
    private CupSize chosenCup = CupSize.Small;

    /// <summary>Hides the panel and the served cups at the start.</summary>
    private void Awake()
    {
        if (servePanel != null) servePanel.SetActive(false);
        HideServedCups();
    }

    /// <summary>Connects to the level's BrewingSession (done in Start so its Awake has run).</summary>
    private void Start()
    {
        session = BrewingSession.Instance;
        if (session == null)
        {
            Debug.LogWarning("[Serve] No BrewingSession in this scene, so the serve buttons will never show.", this);
            return;
        }
        if (servePanel == null)
            Debug.LogWarning("[Serve] Serve Panel is not set on " + name + ".", this);

        session.OnMove += HandleMove;
        session.OnDrinkServed += HandleServed;
    }

    /// <summary>Disconnects from BrewingSession.</summary>
    private void OnDestroy()
    {
        if (session != null)
        {
            session.OnMove -= HandleMove;
            session.OnDrinkServed -= HandleServed;
        }
    }

    /// <summary>
    /// After every accepted move: clears the counter on the first move of a
    /// new drink, and opens the panel once only the Serve step is left.
    /// </summary>
    private void HandleMove(PlayerMove move)
    {
        if (move.action == StepAction.Serve || !session.IsBrewing) return;

        if (session.Moves.Count == 1) HideServedCups();

        if (session.Moves.Count == session.CurrentRecipe.StepCount - 1) OpenPanel();
    }

    /// <summary>Shows the panel (Trash only for rated drinks) and frees the cursor.</summary>
    private void OpenPanel()
    {
        if (servePanel == null) return;
        if (resetButton != null) resetButton.SetActive(!session.IsGuided);
        servePanel.SetActive(true);
        PlayerController.SetUIOpen(true);
    }

    /// <summary>Hides the panel and locks the cursor again.</summary>
    private void ClosePanel()
    {
        if (servePanel != null) servePanel.SetActive(false);
        PlayerController.SetUIOpen(false);
    }

    /// <summary>
    /// "Ready to serve?" button: remembers the chosen cup size, closes the
    /// panel and sends the Serve step to BrewingSession, which judges the drink.
    /// </summary>
    public void Serve()
    {
        if (session == null || !session.IsBrewing) { ClosePanel(); return; }

        chosenCup = CupSize.Small;
        for (int i = session.Moves.Count - 1; i >= 0; i--)
        {
            if (session.Moves[i].action == StepAction.Cup)
            {
                chosenCup = (CupSize)session.Moves[i].choice;
                break;
            }
        }

        ClosePanel();
        session.RecordMove(StepAction.Serve);
    }

    /// <summary>
    /// "Trash" button: closes the panel and throws the drink away; the same
    /// recipe starts again from step 1 (BrewingSession.ResetDrink, timer restarts).
    /// </summary>
    public void ResetDrink()
    {
        ClosePanel();
        if (session != null) session.ResetDrink();
    }

    /// <summary>After the drink is served: shows the cup of the chosen size on the front counter.</summary>
    private void HandleServed(DrinkResult result)
    {
        HideServedCups();
        GameObject cup = chosenCup == CupSize.Large ? largeCup
                       : chosenCup == CupSize.Medium ? mediumCup
                       : smallCup;
        if (cup != null) cup.SetActive(true);
        else Debug.LogWarning("[Serve] The served cup for size " + chosenCup + " is not set.", this);
    }

    /// <summary>Hides all three served cups.</summary>
    private void HideServedCups()
    {
        if (smallCup != null) smallCup.SetActive(false);
        if (mediumCup != null) mediumCup.SetActive(false);
        if (largeCup != null) largeCup.SetActive(false);
    }
}
