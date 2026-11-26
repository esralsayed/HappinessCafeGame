// =====================================================================
// TutorialController.cs
// ---------------------------------------------------------------------
// Runs Grandma's lesson for one level (one per Day scene, on Managers).
//
// Order:
//   1. Begin()           plays the intro conversation (click through).
//   2. Guided drink      starts the recipe in guided mode (only the right
//                        next step is accepted, no timer, no rating).
//   3. Step lines        Grandma's lines from the steps dialogue stay at the
//                        bottom (DialogueManager.Say) while the player moves.
//                        Each line moves on by its rule in the Beats list:
//                          AimAt     - the player aims at a station with that action
//                          StepDone  - the player does that step
//                          Seconds   - after a few seconds (explanations)
//                        A wrong click makes Grandma say her current line again.
//   4. Served            plays the outro conversation (sets its flags,
//                        e.g. luluWarned), then fires OnTutorialFinished.
//
// The Beats list has one entry per line of the steps dialogue, so Level 2
// and 3 reuse this script with their own lines and rules.
// No UI here: all text goes through the DialogueManager.
// =====================================================================
using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;

public class TutorialController : MonoBehaviour
{
    /// <summary>When a step line moves on to the next one.</summary>
    public enum AdvanceWhen { AimAt, StepDone, Seconds }

    /// <summary>The rule for one line of the steps dialogue (same index).</summary>
    [Serializable]
    public class Beat
    {
        [Tooltip("AimAt: aim at a station with this action. StepDone: do this step. Seconds: wait.")]
        public AdvanceWhen when = AdvanceWhen.StepDone;
        [Tooltip("The station action for AimAt / StepDone.")]
        public StepAction action = StepAction.AddBeans;
        [Tooltip("Seconds only: how long the line stays.")]
        public float seconds = 4f;
    }

    [Header("Dialogue")]
    [Tooltip("Conversation before the drink (click through).")]
    public DialogueData intro;
    [Tooltip("Grandma's step lines, shown one by one while the player brews.")]
    public DialogueData steps;
    [Tooltip("Conversation after the drink is served.")]
    public DialogueData outro;

    [Header("Drink")]
    [Tooltip("The recipe taught in this lesson (guided mode). Also fills {drink} / {a}.")]
    public RecipeData recipe;

    [Header("Beats (one per line of the steps dialogue)")]
    public List<Beat> beats = new List<Beat>();

    [Header("Scene")]
    [Tooltip("The player's Interactor (for AimAt). Empty = found automatically.")]
    public Interactor interactor;

    [Header("Testing")]
    [Tooltip("Start the lesson as soon as the scene plays. Later the LevelManager calls Begin().")]
    public bool autoStartOnPlay = false;

    [Header("Events")]
    public UnityEvent onTutorialFinished;

    /// <summary>Fired when the outro has finished (code listeners, e.g. LevelManager).</summary>
    public event Action OnTutorialFinished;

    /// <summary>Fired when the intro has ended and the guided drink starts (the player turns to the stations).</summary>
    public event Action OnLessonStarted;

    /// <summary>The tutorial customer at the counter (set by Customer before Begin). The view turns to them on serve.</summary>
    [System.NonSerialized] public Transform customer;

    /// <summary>True from Begin() until the outro has finished.</summary>
    public bool IsRunning { get; private set; }

    /// <summary>Index of the step line on screen (-1 before the drink starts).</summary>
    public int CurrentLine => lineIndex;

    private int lineIndex = -1;
    private float lineTimer;
    private bool stepping;          // true while the guided drink is being made
    private BrewingSession session;

    // -----------------------------------------------------------------
    // Unity
    // -----------------------------------------------------------------

    /// <summary>Finds the Interactor if not set; starts the lesson if testing.</summary>
    private void Start()
    {
        if (interactor == null) interactor = FindAnyObjectByType<Interactor>();
        if (autoStartOnPlay) Begin();
    }

    /// <summary>Checks the AimAt and Seconds rules every frame.</summary>
    private void Update()
    {
        if (!stepping) return;
        Beat beat = CurrentBeat();
        if (beat == null) return;

        if (beat.when == AdvanceWhen.AimAt && IsAimingAt(beat.action)) NextLine();
        else if (beat.when == AdvanceWhen.Seconds)
        {
            lineTimer += Time.deltaTime;
            if (lineTimer >= beat.seconds) NextLine();
        }
    }

    private void OnDisable()
    {
        Unsubscribe();
    }

    // -----------------------------------------------------------------
    // Public
    // -----------------------------------------------------------------

    /// <summary>
    /// Starts the lesson: intro conversation, then the guided drink.
    /// Ignored if the lesson is already running.
    /// </summary>
    public void Begin()
    {
        if (IsRunning) return;
        session = BrewingSession.Instance;
        if (session == null || recipe == null || DialogueManager.Instance == null)
        {
            Debug.LogWarning("[Tutorial] Needs a BrewingSession, a DialogueManager and a recipe.", this);
            return;
        }

        IsRunning = true;
        session.CancelDrink();   // drop any test drink
        if (intro == null || !DialogueManager.Instance.Play(intro, recipe, null, StartDrink))
            StartDrink();
    }

    // -----------------------------------------------------------------
    // Flow
    // -----------------------------------------------------------------

    /// <summary>After the intro: guided drink + first step line.</summary>
    private void StartDrink()
    {
        Subscribe();
        session.StartDrink(recipe, true);
        OnLessonStarted?.Invoke();
        stepping = true;
        lineIndex = -1;
        NextLine();
    }

    /// <summary>Shows the next step line (or hides the box after the last one).</summary>
    private void NextLine()
    {
        lineIndex++;
        lineTimer = 0f;
        var line = steps != null ? steps.GetLine(lineIndex) : null;
        if (line == null) { DialogueManager.Instance.Hide(); return; }
        DialogueManager.Instance.Say(line.speaker, line.text, recipe);
    }

    /// <summary>Says the current step line again (after a wrong click).</summary>
    private void RepeatLine()
    {
        var line = steps != null ? steps.GetLine(lineIndex) : null;
        if (line != null) DialogueManager.Instance.Say(line.speaker, line.text, recipe);
    }

    /// <summary>
    /// A correct step was done. Moves past any AimAt / Seconds lines that came
    /// before it, then past the line waiting for this step.
    /// </summary>
    private void HandleMove(PlayerMove move)
    {
        if (!stepping) return;
        for (int guard = 0; guard < 20; guard++)
        {
            Beat beat = CurrentBeat();
            if (beat == null) return;
            if (beat.when == AdvanceWhen.StepDone)
            {
                if (beat.action == move.action) NextLine();
                return;
            }
            NextLine();   // AimAt / Seconds line: the player is already past it
        }
    }

    /// <summary>Guided mode refused a click: Grandma repeats herself.</summary>
    private void HandleWrongMove(RecipeStep expected)
    {
        if (stepping) RepeatLine();
    }

    /// <summary>The lesson drink was served: outro, then finished.</summary>
    private void HandleServed(DrinkResult result)
    {
        if (!stepping || !result.guided) return;
        stepping = false;
        Unsubscribe();
        DialogueManager.Instance.Hide();
        if (outro == null || !DialogueManager.Instance.Play(outro, recipe, null, Finish))
            Finish();
    }

    /// <summary>End of the lesson.</summary>
    private void Finish()
    {
        IsRunning = false;
        lineIndex = -1;
        Debug.Log("[Tutorial] Finished: " + recipe.displayName);
        OnTutorialFinished?.Invoke();
        onTutorialFinished?.Invoke();
    }

    // -----------------------------------------------------------------
    // Helpers
    // -----------------------------------------------------------------

    /// <summary>The rule for the line on screen. Missing entries = StepDone of the expected step.</summary>
    private Beat CurrentBeat()
    {
        if (steps == null || lineIndex < 0 || lineIndex >= steps.LineCount) return null;
        if (lineIndex < beats.Count) return beats[lineIndex];
        var expected = session != null ? session.ExpectedStep : null;
        return new Beat { when = AdvanceWhen.StepDone, action = expected != null ? expected.action : StepAction.Serve };
    }

    /// <summary>True if the dot is on a station with this action.</summary>
    private bool IsAimingAt(StepAction action)
    {
        if (interactor == null || interactor.CurrentTarget == null) return false;
        var station = interactor.CurrentTarget.GetComponent<BrewingStation>();
        return station != null && station.action == action;
    }

    /// <summary>True while a step line points at a station (used by StationGuide for the glow and arrow).</summary>
    public bool IsGuiding
    {
        get
        {
            if (!stepping) return false;
            var beat = CurrentBeat();
            return beat != null && beat.when != AdvanceWhen.Seconds;
        }
    }

    /// <summary>
    /// True if this station is the one Grandma's current line is about. For steps with a
    /// choice (shots, cup size, temperature, flavour) only the correct part counts.
    /// </summary>
    public bool IsGuideTarget(BrewingStation station)
    {
        if (station == null || !IsGuiding) return false;
        var beat = CurrentBeat();
        if (station.action != beat.action) return false;
        var expected = session != null ? session.ExpectedStep : null;
        if (expected == null || expected.action != beat.action || !expected.HasChoice) return true;
        return station.ChoiceValue == expected.CorrectChoice;
    }

    private void Subscribe()
    {
        Unsubscribe();
        session.OnMove += HandleMove;
        session.OnWrongMove += HandleWrongMove;
        session.OnDrinkServed += HandleServed;
    }

    private void Unsubscribe()
    {
        if (session == null) return;
        session.OnMove -= HandleMove;
        session.OnWrongMove -= HandleWrongMove;
        session.OnDrinkServed -= HandleServed;
    }

    /// <summary>Inspector check: the Beats list should have one entry per step line.</summary>
    private void OnValidate()
    {
        if (steps != null && beats.Count != steps.LineCount)
            Debug.LogWarning("[Tutorial] '" + name + "': " + beats.Count + " beats for " + steps.LineCount +
                             " step lines. Missing beats wait for the expected step.", this);
    }
}
