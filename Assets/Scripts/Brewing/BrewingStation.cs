// =====================================================================
// BrewingStation.cs
// ---------------------------------------------------------------------
// Put this on EVERY clickable part of a coffee station: the grinder hopper,
// the grind button, the espresso x1 / x2 buttons, each cup (S/M/L), each
// steamer dial (40/60/80), each flavour bottle, the pour spot and the serve spot.
//
// Each part says WHAT it does (action) and WHICH choice it stands for
// (shots, cup, temperature or flavour). When the player uses it, it sends
// that move to the level's BrewingSession.
//
// Input: it hooks itself onto the Interactable on the same object, so it
// works with whatever triggers that Interactable (E today, left-click after
// the Interactor edit). Use() is public, so a UnityEvent can call it too:
// the carry cup's Draggable.onPlaced calls the serve spot's Use().
//
// Scene reactions (sounds, showing coffee in the cup, showing the carry cup)
// are wired in the Inspector on onUsed / onRefused. No UI in this script.
// =====================================================================
using UnityEngine;
using UnityEngine.Events;

[RequireComponent(typeof(Interactable))]
public class BrewingStation : MonoBehaviour
{
    [Header("What this part does")]
    [Tooltip("The step this part performs.")]
    public StepAction action = StepAction.AddBeans;

    [Header("Choice this part stands for (only the one matching the action is used)")]
    [Tooltip("Espresso button: 1 = single, 2 = double.")]
    [Range(1, 2)] public int shots = 1;

    [Tooltip("Cup: the size of THIS cup.")]
    public CupSize cup = CupSize.Small;

    [Tooltip("Steam dial: the temperature of THIS dial in °C (40, 60 or 80).")]
    public int temperature = 60;

    [Tooltip("Flavour bottle / topping: the flavour of THIS bottle.")]
    public Flavour flavour = Flavour.Vanilla;

    [Header("Prompt")]
    [Tooltip("Text shown when aiming at this part. Empty = automatic, e.g. \"Click: Small cup\".")]
    public string promptText = "";

    [Header("Scene reactions (wire in the Inspector)")]
    [Tooltip("Runs when the move is accepted: sounds, animations, show coffee, show the carry cup...")]
    public UnityEvent onUsed = new UnityEvent();

    [Tooltip("Runs when Grandma's tutorial refuses the move (guided mode only): e.g. a 'no' sound.")]
    public UnityEvent onRefused = new UnityEvent();

    private Interactable interactable;

    /// <summary>
    /// The choice as one int, in the same encoding as RecipeStep.CorrectChoice:
    /// shots, (int)cup, temperature or (int)flavour. 0 for parts without a choice.
    /// </summary>
    public int ChoiceValue
    {
        get
        {
            switch (action)
            {
                case StepAction.Espresso: return shots;
                case StepAction.Cup: return (int)cup;
                case StepAction.Steam: return temperature;
                case StepAction.Flavour:
                case StepAction.Topping: return (int)flavour;
                default: return 0;
            }
        }
    }

    /// <summary>The prompt for this part: your own text if written, otherwise automatic.</summary>
    public string PromptText => string.IsNullOrWhiteSpace(promptText) ? DefaultPrompt() : promptText;

    /// <summary>Connects Use() to the Interactable on this object, so E / click reach it.</summary>
    private void Awake()
    {
        interactable = GetComponent<Interactable>();
        if (interactable.onInteract == null) interactable.onInteract = new UnityEvent();
        interactable.onInteract.AddListener(Use);
    }

    /// <summary>Disconnects from the Interactable when this object is destroyed.</summary>
    private void OnDestroy()
    {
        if (interactable != null && interactable.onInteract != null)
            interactable.onInteract.RemoveListener(Use);
    }

    /// <summary>
    /// Uses this part: sends (action, choice) to BrewingSession.RecordMove.
    /// Fires onUsed if the move is accepted, onRefused if the tutorial refused it.
    /// Safe to call from UnityEvents (e.g. Draggable.onPlaced on the serve spot).
    /// </summary>
    public void Use()
    {
        var session = BrewingSession.Instance;
        if (session == null)
        {
            Debug.LogWarning("[Brewing] " + name + " was used, but there is no BrewingSession in this scene " +
                             "(add one to the Managers object).", this);
            return;
        }

        if (session.RecordMove(action, ChoiceValue)) onUsed?.Invoke();
        else if (session.IsBrewing && session.IsGuided) onRefused?.Invoke();
    }

    /// <summary>Automatic prompt text built from the action and this part's choice.</summary>
    private string DefaultPrompt()
    {
        switch (action)
        {
            case StepAction.AddBeans: return "Click: Add beans";
            case StepAction.Grind: return "Click: Grind";
            case StepAction.Espresso: return shots == 2 ? "Click: Double shot (×2)" : "Click: Single shot (×1)";
            case StepAction.Cup: return "Click: " + cup + " cup";
            case StepAction.Flavour: return "Click: Add " + flavour.ToString().ToLower();
            case StepAction.Steam: return "Click: " + temperature + " °C";
            case StepAction.Pour: return "Click: Pour milk";
            case StepAction.Topping: return "Click: Sprinkle " + flavour.ToString().ToLower();
            case StepAction.Serve: return "Drop the cup here to serve";
            default: return "Click";
        }
    }

    /// <summary>Inspector check: warns about a steam dial that is not 40, 60 or 80 °C.</summary>
    private void OnValidate()
    {
        if (action == StepAction.Steam && temperature != 40 && temperature != 60 && temperature != 80)
            Debug.LogWarning("[Brewing] Steam dial '" + name + "' is set to " + temperature +
                             " °C. Recipes use 40, 60 or 80.", this);
    }
}
