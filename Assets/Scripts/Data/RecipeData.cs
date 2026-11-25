// =====================================================================
// RecipeData.cs
// ---------------------------------------------------------------------
// One coffee recipe, stored as a ScriptableObject asset (a data file you
// edit in the Inspector). Create one with:
//   Project window > Right-click > Create > Happiness Cafe > Recipe
//
// A recipe is written once here and read by every other system:
//   - BrewingStation  : walks the steps in order and checks the player's
//                       choice at each one (IsCorrect)
//   - OrderManager    : uses displayName / ArticleName for {drink} and {a}
//   - Recipe Book     : shows the steps, the coffee fact and Grandma's
//                       margin note
//   - Tutorials       : Grandma's lines follow the same step order
//
// The coffee fact is a field on the recipe (one fact per recipe), which
// replaces a separate CoffeeFact asset.
//
// Contains no UI and no scene references.
// =====================================================================
using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>What the player does at one step of a recipe.</summary>
public enum StepAction { AddBeans, Grind, Espresso, Cup, Flavour, Steam, Pour, Topping, Serve }

/// <summary>Cup sizes: small = espresso drinks, medium = cappuccino, large = lattes.</summary>
public enum CupSize { Small, Medium, Large }

/// <summary>Flavours on the flavour shelf (Level 3), also used for toppings.</summary>
public enum Flavour { Vanilla, Caramel, Cinnamon }

/// <summary>Base = taught by Grandma's tutorial. Bonus = unlocked by a restored torn page.</summary>
public enum RecipeKind { Base, Bonus }

/// <summary>
/// One step of a recipe. Only the choice field that matches the action is used:
/// Espresso uses shots, Cup uses cup, Steam uses temperature, Flavour/Topping use flavour.
/// </summary>
[Serializable]
public class RecipeStep
{
    [Tooltip("What the player does at this step.")]
    public StepAction action;

    [Header("Correct choice (only the one matching the action is used)")]
    [Tooltip("Espresso only: 1 = single, 2 = double.")]
    [Range(1, 2)] public int shots = 1;

    [Tooltip("Cup only: the correct cup size.")]
    public CupSize cup = CupSize.Small;

    [Tooltip("Steam only: the correct temperature in °C (40, 60 or 80). 60 is always correct in this game.")]
    public int temperature = 60;

    [Tooltip("Flavour / Topping only: the correct bottle.")]
    public Flavour flavour = Flavour.Vanilla;

    [Header("Texts (leave empty to use the automatic text)")]
    [Tooltip("Fills {step} in \"Oh no, you forgot to {step}\". Empty = automatic, e.g. \"grind the beans\".")]
    public string forgotText = "";

    [Tooltip("Contextual prompt on the station, e.g. \"Click: Grind\". Empty = automatic.")]
    public string promptText = "";

    /// <summary>The {step} text: your own text if written, otherwise an automatic one built from the action and choice.</summary>
    public string ForgotText => string.IsNullOrWhiteSpace(forgotText) ? DefaultForgotText() : forgotText;

    /// <summary>The prompt text: your own text if written, otherwise an automatic one.</summary>
    public string PromptText => string.IsNullOrWhiteSpace(promptText) ? DefaultPromptText() : promptText;

    /// <summary>
    /// The choice value that is correct for this step, as one int:
    /// shots (1/2), (int)cup, temperature (°C) or (int)flavour. 0 for steps without a choice.
    /// </summary>
    public int CorrectChoice
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

    /// <summary>True if this action has a choice the player must get right.</summary>
    public bool HasChoice =>
        action == StepAction.Espresso || action == StepAction.Cup || action == StepAction.Steam ||
        action == StepAction.Flavour || action == StepAction.Topping;

    /// <summary>Automatic text for "you forgot to {step}".</summary>
    private string DefaultForgotText()
    {
        switch (action)
        {
            case StepAction.AddBeans: return "put beans in the grinder";
            case StepAction.Grind: return "grind the beans";
            case StepAction.Espresso: return shots == 2 ? "make a double shot" : "make the espresso";
            case StepAction.Cup: return "choose a " + cup.ToString().ToLower() + " cup";
            case StepAction.Flavour: return "add " + flavour.ToString().ToLower();
            case StepAction.Steam: return "steam the milk to " + temperature + " °C";
            case StepAction.Pour: return "pour the milk";
            case StepAction.Topping: return "sprinkle " + flavour.ToString().ToLower() + " on top";
            case StepAction.Serve: return "serve the drink";
            default: return action.ToString();
        }
    }

    /// <summary>Automatic contextual prompt (verb shown on the glowing station).</summary>
    private string DefaultPromptText()
    {
        switch (action)
        {
            case StepAction.AddBeans: return "Click: Add beans";
            case StepAction.Grind: return "Click: Grind";
            case StepAction.Espresso: return "Click: Espresso";
            case StepAction.Cup: return "Click: Choose cup";
            case StepAction.Flavour: return "Click: Add flavour";
            case StepAction.Steam: return "Click: Steam";
            case StepAction.Pour: return "Click: Pour";
            case StepAction.Topping: return "Click: Sprinkle";
            case StepAction.Serve: return "Click: Serve";
            default: return "Click";
        }
    }
}

[CreateAssetMenu(fileName = "NewRecipe", menuName = "Happiness Cafe/Recipe")]
public class RecipeData : ScriptableObject
{
    [Header("Identity")]
    [Tooltip("Unique id saved in the player's profile when unlocked, e.g. \"vanillaLatte\". Do not change after release.")]
    public string id = "";

    [Tooltip("Name shown on tickets and in the Recipe Book. Fills {drink}.")]
    public string displayName = "";

    [Tooltip("Base = taught in a tutorial. Bonus = unlocked by a restored torn page.")]
    public RecipeKind kind = RecipeKind.Base;

    [Tooltip("The level that teaches (Base) or unlocks (Bonus) this recipe: 1, 2 or 3.")]
    [Range(1, 3)] public int level = 1;

    [Header("Steps (in order)")]
    [Tooltip("The order the player must follow. The last step should be Serve.")]
    public List<RecipeStep> steps = new List<RecipeStep>();

    [Header("Recipe Book")]
    [Tooltip("Coffee fact shown on this recipe's page and on loading screens.")]
    [TextArea(2, 4)] public string fact = "";

    [Tooltip("Grandma's handwritten margin note (bonus pages). Leave empty for none.")]
    [TextArea(2, 4)] public string marginNote = "";

    [Tooltip("Picture for the Recipe Book and the order ticket (optional).")]
    public Sprite icon;

    // -----------------------------------------------------------------
    // Read helpers used by the brewing station, orders and the book
    // -----------------------------------------------------------------

    /// <summary>Number of steps in the recipe.</summary>
    public int StepCount => steps.Count;

    /// <summary>The step at index i, or null if i is out of range.</summary>
    public RecipeStep GetStep(int i)
    {
        return (i >= 0 && i < steps.Count) ? steps[i] : null;
    }

    /// <summary>
    /// Checks a player's move against step i: the action must match, and for steps
    /// with a choice (shots, cup, temperature, flavour) the choice must match too.
    /// choice uses the same encoding as RecipeStep.CorrectChoice.
    /// </summary>
    public bool IsCorrect(int i, StepAction action, int choice = 0)
    {
        var step = GetStep(i);
        if (step == null || step.action != action) return false;
        return !step.HasChoice || step.CorrectChoice == choice;
    }

    /// <summary>The name with "a" or "an" in front, for the {a} {drink} order lines: "an Espresso", "a Cappuccino".</summary>
    public string ArticleName
    {
        get
        {
            if (string.IsNullOrEmpty(displayName)) return "";
            char first = char.ToLower(displayName[0]);
            return ("aeiou".IndexOf(first) >= 0 ? "an " : "a ") + displayName;
        }
    }

    /// <summary>Just the article, for templates that keep {a} and {drink} separate.</summary>
    public string Article => ArticleName.StartsWith("an ") ? "an" : "a";

    /// <summary>
    /// Inspector check: warns in the Console while editing if the recipe is missing
    /// an id, has no steps, does not end with Serve, or uses an unusual temperature.
    /// </summary>
    private void OnValidate()
    {
        if (string.IsNullOrWhiteSpace(id))
            Debug.LogWarning("[Recipe] '" + name + "' has no id.", this);
        if (steps.Count == 0) return;
        if (steps[steps.Count - 1].action != StepAction.Serve)
            Debug.LogWarning("[Recipe] '" + name + "' does not end with a Serve step.", this);
        foreach (var s in steps)
            if (s.action == StepAction.Steam && s.temperature != 40 && s.temperature != 60 && s.temperature != 80)
                Debug.LogWarning("[Recipe] '" + name + "' has a Steam step at " + s.temperature +
                                 " °C. The machine only offers 40, 60 and 80.", this);
    }
}
