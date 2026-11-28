// =====================================================================
// LevelManager.cs
// ---------------------------------------------------------------------
// Runs one level (one per Day scene, on Managers).
//
//   Play  -> starts the CustomerSpawner's queue. The visits list decides the
//            order: the Tutorial customer starts Grandma's lesson at the
//            counter, Rated customers order through the OrderManager.
//   Count -> every rated drink served (OrderManager.OnOrderServed) adds its
//            stars. When all Rated visits are served, the level is complete.
//   Complete:
//            level stars = average of the rated customers' stars, rounded down
//            -> RecordLevelStars, unlock the bonus recipe (torn page restored),
//            set the complete flag (e.g. level1Complete), show the torn-page
//            message + Grandma's margin note in the dialogue box, then fire
//            OnLevelComplete (the summary screen will listen later).
//   Fail()   stops the level and fires OnLevelFailed (the patience timer will
//            call it later).
//
// No UI of its own: messages go through the DialogueManager.
// =====================================================================
using System;
using UnityEngine;
using UnityEngine.Events;

public class LevelManager : MonoBehaviour
{
    /// <summary>The LevelManager in the current scene.</summary>
    public static LevelManager Instance { get; private set; }

    [Header("Level")]
    [Tooltip("1, 2 or 3. Used for RecordLevelStars.")]
    [Range(1, 3)] public int levelNumber = 1;
    [Tooltip("Story flag set when the level is complete, e.g. level1Complete.")]
    public string completeFlag = "level1Complete";

    [Header("Reward (torn page)")]
    [Tooltip("Bonus recipe unlocked when the level is complete (its margin note is shown).")]
    public RecipeData bonusRecipe;
    [Tooltip("Shown first (Narrator). Empty = skipped.")]
    [TextArea] public string tornPageLine = "A torn page is restored!";

    [Header("Scene")]
    [Tooltip("Empty = found automatically.")]
    public CustomerSpawner spawner;

    [Header("Start")]
    [Tooltip("Start the customer queue as soon as the scene plays.")]
    public bool startOnPlay = true;

    [Header("Events")]
    public UnityEvent onLevelComplete;
    public UnityEvent onLevelFailed;

    /// <summary>Fired after the torn-page message: level stars (1-3).</summary>
    public event Action<int> OnLevelComplete;
    /// <summary>Fired when the level fails (e.g. a customer waited too long).</summary>
    public event Action OnLevelFailed;

    public bool IsRunning { get; private set; }
    public bool IsComplete { get; private set; }
    public bool IsFailed { get; private set; }
    /// <summary>Level stars after completion (average, rounded down), 0 before.</summary>
    public int LevelStars { get; private set; }
    /// <summary>Rated drinks served so far in this level.</summary>
    public int RatedServed => ratedServed;

    private int ratedServed;
    private int starsTotal;
    private OrderManager orders;

    // -----------------------------------------------------------------
    // Unity
    // -----------------------------------------------------------------

    private void Awake()
    {
        Instance = this;
    }

    /// <summary>Finds the spawner and OrderManager, listens for served orders, starts if set.</summary>
    private void Start()
    {
        if (spawner == null) spawner = FindAnyObjectByType<CustomerSpawner>();
        orders = OrderManager.Instance;
        if (orders != null) orders.OnOrderServed += HandleOrderServed;
        else Debug.LogWarning("[Level] No OrderManager in the scene: rated drinks cannot be counted.", this);

        if (startOnPlay) Begin();
    }

    private void OnDestroy()
    {
        if (orders != null) orders.OnOrderServed -= HandleOrderServed;
        if (Instance == this) Instance = null;
    }

    // -----------------------------------------------------------------
    // Public
    // -----------------------------------------------------------------

    /// <summary>Starts the level: sends the customers in. Ignored if already running.</summary>
    public void Begin()
    {
        if (IsRunning || IsComplete) return;
        if (spawner == null)
        {
            Debug.LogWarning("[Level] No CustomerSpawner in the scene.", this);
            return;
        }
        IsRunning = true;
        ratedServed = 0;
        starsTotal = 0;
        Debug.Log("[Level] Level " + levelNumber + " started: " + spawner.RatedVisitCount + " rated customer(s).");
        spawner.StartQueue();
    }

    /// <summary>Ends the level as failed (called by the patience timer later).</summary>
    public void Fail()
    {
        if (!IsRunning) return;
        IsRunning = false;
        IsFailed = true;
        if (BrewingSession.Instance != null) BrewingSession.Instance.CancelDrink();
        Debug.Log("[Level] Level " + levelNumber + " failed.");
        OnLevelFailed?.Invoke();
        onLevelFailed?.Invoke();
    }

    // -----------------------------------------------------------------
    // Flow
    // -----------------------------------------------------------------

    /// <summary>A rated drink was served: add its stars; complete when all are served.</summary>
    private void HandleOrderServed(RecipeData recipe, int stars, DrinkResult result)
    {
        if (!IsRunning) return;
        ratedServed++;
        starsTotal += stars;
        if (ratedServed >= spawner.RatedVisitCount) Complete();
    }

    /// <summary>Stars, unlock, flag, torn-page message, event.</summary>
    private void Complete()
    {
        IsRunning = false;
        IsComplete = true;
        LevelStars = ratedServed > 0 ? Mathf.Clamp(starsTotal / ratedServed, 1, 3) : 0;   // average, rounded down

        var story = StoryStateManager.Instance;
        if (story != null)
        {
            if (LevelStars > 0) story.RecordLevelStars(levelNumber, LevelStars);
            if (bonusRecipe != null) story.UnlockRecipe(bonusRecipe.id);
            if (!string.IsNullOrWhiteSpace(completeFlag)) story.SetFlag(completeFlag);
        }
        Debug.Log("[Level] Level " + levelNumber + " complete: " + LevelStars + " star(s)" +
                  (bonusRecipe != null ? ", unlocked " + bonusRecipe.displayName : "") + ".");

        StartCoroutine(ShowTornPage());
    }

    /// <summary>Torn-page message and Grandma's margin note, then OnLevelComplete.</summary>
    private System.Collections.IEnumerator ShowTornPage()
    {
        var dm = DialogueManager.Instance;
        if (dm != null)
        {
            if (!string.IsNullOrWhiteSpace(tornPageLine))
                yield return Say(dm, Speaker.Narrator, tornPageLine);
            if (bonusRecipe != null && !string.IsNullOrWhiteSpace(bonusRecipe.marginNote))
                yield return Say(dm, Speaker.Grandma, bonusRecipe.marginNote);
        }
        OnLevelComplete?.Invoke(LevelStars);
        onLevelComplete?.Invoke();
    }

    /// <summary>Plays one line and waits until it is clicked away (waits first if another conversation plays).</summary>
    private System.Collections.IEnumerator Say(DialogueManager dm, Speaker speaker, string text)
    {
        while (dm.IsPlaying) yield return null;
        bool done = false;
        if (!dm.PlayLine(speaker, text, () => done = true)) yield break;
        while (!done) yield return null;
    }
}
