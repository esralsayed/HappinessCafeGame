// =====================================================================
// BrewingSession.cs
// ---------------------------------------------------------------------
// The "brain" for the drink the player is currently making. One per level,
// on the scene's Managers object. It has no visuals and no UI.
//
// Flow:
//   1. StartDrink(recipe, guided)   called by the order system (later) when a
//                                   customer orders. Until then, the Inspector's
//                                   Test Recipe is started automatically on Play.
//   2. RecordMove(action, choice)   called by every BrewingStation when clicked,
//                                   e.g. (Cup, (int)CupSize.Small). Serve ends the drink.
//   3. OnDrinkServed(result)        fired on Serve with: correct yes/no, the first
//                                   missed or wrong step (fills {step}) and the seconds
//                                   taken. The order system turns that into 1-3 stars.
//
// Two modes:
//   Free   (rated customers)   : every move is accepted; mistakes are only found
//                                when the drink is served (tests the order of steps).
//   Guided (Grandma's tutorial): only the expected next step is accepted; a wrong
//                                click is refused and fires OnWrongMove so Grandma
//                                can repeat that step's line. No rating.
// =====================================================================
using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>One thing the player did at a station (action + the choice picked).</summary>
[Serializable]
public class PlayerMove
{
    public StepAction action;
    public int choice;   // same encoding as RecipeStep.CorrectChoice

    public override string ToString() => action + (choice != 0 ? "(" + choice + ")" : "");

    /// <summary>Short "step done" text for on-screen feedback, e.g. "Beans added", "Small cup".</summary>
    public string DoneText
    {
        get
        {
            switch (action)
            {
                case StepAction.AddBeans: return "Beans added";
                case StepAction.Grind: return "Beans ground";
                case StepAction.Espresso: return choice == 2 ? "Double shot made" : "Single shot made";
                case StepAction.Cup: return ((CupSize)choice) + " cup chosen";
                case StepAction.Steam: return "Milk steamed to " + choice + " °C";
                case StepAction.Pour: return "Milk poured";
                case StepAction.Flavour: return ((Flavour)choice) + " added";
                case StepAction.Topping: return ((Flavour)choice) + " sprinkled on top";
                default: return action.ToString();
            }
        }
    }
}

/// <summary>What happened when a drink was served. Sent with OnDrinkServed.</summary>
public class DrinkResult
{
    public RecipeData recipe;
    public bool correct;              // every step done, in order, with the right choices
    public int firstWrongIndex = -1;  // index of the first missed/wrong step, -1 if correct
    public RecipeStep firstWrongStep; // that step (null if correct)
    public float seconds;             // time from StartDrink to Serve
    public bool guided;               // true for the tutorial drink (not rated)

    /// <summary>Text for the {step} token, e.g. "grind the beans". Empty if correct.</summary>
    public string StepText => firstWrongStep != null ? firstWrongStep.ForgotText : "";
}

public class BrewingSession : MonoBehaviour
{
    /// <summary>The session in the current level. Stations use BrewingSession.Instance.</summary>
    public static BrewingSession Instance { get; private set; }

    // ---------- Events (other scripts subscribe in code) ----------
    /// <summary>A new drink started. Passes the recipe.</summary>
    public event Action<RecipeData> OnDrinkStarted;
    /// <summary>A move was accepted (use for sounds/animations). Passes the move.</summary>
    public event Action<PlayerMove> OnMove;
    /// <summary>Guided mode only: a wrong click was refused. Passes the step that was expected.</summary>
    public event Action<RecipeStep> OnWrongMove;
    /// <summary>The drink was served. Passes the result.</summary>
    public event Action<DrinkResult> OnDrinkServed;

    // ---------- Inspector ----------
    [Header("Testing (before the order system exists)")]
    [Tooltip("Started automatically on Play so brewing can be tested alone. Leave empty when the order system starts drinks.")]
    [SerializeField] private RecipeData testRecipe;
    [Tooltip("Test the tutorial behaviour (wrong clicks refused) instead of free mode.")]
    [SerializeField] private bool testGuided = false;

    [Header("Live view (read-only, for debugging)")]
    [SerializeField] private RecipeData currentRecipe;
    [SerializeField] private bool guided;
    [SerializeField] private List<PlayerMove> moves = new List<PlayerMove>();
    [SerializeField] private float secondsSoFar;

    private float startTime;

    // ---------- Read-only properties ----------
    /// <summary>True while a drink is being made.</summary>
    public bool IsBrewing => currentRecipe != null;
    /// <summary>The recipe being made, or null.</summary>
    public RecipeData CurrentRecipe => currentRecipe;
    /// <summary>True if the current drink is the guided tutorial drink.</summary>
    public bool IsGuided => guided;
    /// <summary>The step the recipe expects next (for prompts and Grandma's lines), or null.</summary>
    public RecipeStep ExpectedStep => currentRecipe != null ? currentRecipe.GetStep(moves.Count) : null;
    /// <summary>Moves made so far for the current drink.</summary>
    public IReadOnlyList<PlayerMove> Moves => moves;

    // =================================================================
    // Unity lifecycle
    // =================================================================

    /// <summary>Registers this as the level's session.</summary>
    private void Awake()
    {
        if (Instance != null && Instance != this)
            Debug.LogWarning("[Brewing] More than one BrewingSession in the scene. Using the newest.", this);
        Instance = this;
    }

    /// <summary>Clears the static reference when the level unloads.</summary>
    private void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }

    /// <summary>Starts the Test Recipe, if one is set.</summary>
    private void Start()
    {
        if (testRecipe != null && !IsBrewing) StartDrink(testRecipe, testGuided);
    }

    /// <summary>Updates the live timer shown in the Inspector.</summary>
    private void Update()
    {
        if (IsBrewing) secondsSoFar = Time.time - startTime;
    }

    // =================================================================
    // Public methods
    // =================================================================

    /// <summary>
    /// Begins a new drink. guided = true for Grandma's tutorial customer
    /// (only the expected step is accepted). Any unfinished drink is discarded.
    /// </summary>
    public void StartDrink(RecipeData recipe, bool guidedMode = false)
    {
        if (recipe == null)
        {
            Debug.LogWarning("[Brewing] StartDrink called with no recipe.", this);
            return;
        }
        currentRecipe = recipe;
        guided = guidedMode;
        moves.Clear();
        startTime = Time.time;
        secondsSoFar = 0f;
        Debug.Log("[Brewing] Started " + recipe.displayName + (guided ? " (guided)" : ""));
        OnDrinkStarted?.Invoke(recipe);
    }

    /// <summary>
    /// Called by a station when the player uses it. Serve finishes the drink.
    /// Returns true if the move was accepted (always in free mode; only the
    /// expected step in guided mode).
    /// </summary>
    public bool RecordMove(StepAction action, int choice = 0)
    {
        if (!IsBrewing)
        {
            Debug.Log("[Brewing] No drink in progress, so " + action + " was ignored.");
            return false;
        }

        if (guided && !currentRecipe.IsCorrect(moves.Count, action, choice))
        {
            OnWrongMove?.Invoke(ExpectedStep);
            Debug.Log("[Brewing] Guided: refused " + action + ". Expected: " +
                      (ExpectedStep != null ? ExpectedStep.ForgotText : "nothing"));
            return false;
        }

        var move = new PlayerMove { action = action, choice = choice };
        moves.Add(move);
        OnMove?.Invoke(move);

        if (action == StepAction.Serve) Serve();
        return true;
    }

    /// <summary>
    /// Remake: throws away the moves and restarts the timer, keeping the same
    /// recipe (used after Mrs. Lulu's "Again, please.").
    /// </summary>
    public void ResetDrink()
    {
        if (!IsBrewing) return;
        moves.Clear();
        startTime = Time.time;
        secondsSoFar = 0f;
        Debug.Log("[Brewing] Remaking " + currentRecipe.displayName);
    }

    /// <summary>Stops the current drink without serving it (e.g. the customer left).</summary>
    public void CancelDrink()
    {
        currentRecipe = null;
        moves.Clear();
    }

    // =================================================================
    // Internal
    // =================================================================

    /// <summary>
    /// Compares the moves with the recipe step by step, builds the result,
    /// ends the drink and fires OnDrinkServed. If the Test Recipe is in use,
    /// a new test drink starts right away so you can keep testing.
    /// </summary>
    private void Serve()
    {
        var result = new DrinkResult
        {
            recipe = currentRecipe,
            seconds = Time.time - startTime,
            guided = guided,
            correct = true
        };

        int count = Mathf.Max(moves.Count, currentRecipe.StepCount);
        for (int i = 0; i < count; i++)
        {
            bool ok = i < moves.Count && currentRecipe.IsCorrect(i, moves[i].action, moves[i].choice);
            if (!ok)
            {
                result.correct = false;
                result.firstWrongIndex = i;
                // The step the player should have done here (the last recipe step if they did extra moves).
                result.firstWrongStep = currentRecipe.GetStep(Mathf.Min(i, currentRecipe.StepCount - 1));
                break;
            }
        }

        Debug.Log("[Brewing] Served " + currentRecipe.displayName + " in " + result.seconds.ToString("0.0") + " s: " +
                  (result.correct ? "correct" : "wrong at step " + (result.firstWrongIndex + 1) + " (" + result.StepText + ")") +
                  "  Moves: " + string.Join(" > ", moves.ConvertAll(m => m.ToString()).ToArray()));

        var served = currentRecipe;
        currentRecipe = null;
        OnDrinkServed?.Invoke(result);

        // Testing convenience: keep the test drink going.
        if (testRecipe != null && served == testRecipe && !IsBrewing) StartDrink(testRecipe, testGuided);
    }
}
