// =====================================================================
// DialogueData.cs
// ---------------------------------------------------------------------
// One conversation, stored as a ScriptableObject asset (a data file you
// edit in the Inspector). Create one with:
//   Project window > Right-click > Create > Happiness Cafe > Dialogue
//
// This is the game's dialogue database: every line from the dialogue
// script lives in one of these assets, not in code.
//   - lines          : who speaks and what they say, in order
//   - requiredFlags  : the conversation only plays if all these story
//                      flags are set (symbolic preconditions)
//   - setFlag / flagOnEnd : story flags set while it plays (actions)
//   - choices        : 0 or 2 options shown after the last line; each can
//                      set a flag and lead to another DialogueData
//                      (the small dialogue tree for the ending)
//
// Tokens in the text are filled in at runtime by DialogueManager:
//   {name} = player's name, {drink} = ordered drink, {a} = "a"/"an",
//   {step} = the first missed step.
//
// Contains no UI and no scene references. The speaker's display name
// and portrait are set once on the DialogueManager, not on every line.
// =====================================================================
using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>Who says a line. DialogueManager maps each speaker to a name and portrait.</summary>
public enum Speaker { Grandma, Customer, MrsLulu, Narrator, Player }

/// <summary>One line of dialogue.</summary>
[Serializable]
public class DialogueLine
{
    [Tooltip("Who says this line. The name and portrait come from the DialogueManager.")]
    public Speaker speaker = Speaker.Grandma;

    [Tooltip("The text. May contain {name}, {drink}, {a} and {step}.")]
    [TextArea(2, 5)] public string text = "";

    [Tooltip("Optional: story flag set when this line is shown, e.g. luluWarned. Leave empty for none.")]
    public string setFlag = "";
}

/// <summary>One option the player can pick after the last line.</summary>
[Serializable]
public class DialogueChoice
{
    [Tooltip("The player's line shown on the button, e.g. \"I can do this.\"")]
    public string text = "";

    [Tooltip("Optional: story flag set when this option is picked, e.g. endingKeys.")]
    public string setFlag = "";

    [Tooltip("Optional: conversation that plays next (e.g. Grandma's reply). Empty = the dialogue ends.")]
    public DialogueData next;
}

[CreateAssetMenu(fileName = "NewDialogue", menuName = "Happiness Cafe/Dialogue")]
public class DialogueData : ScriptableObject
{
    [Header("Identity")]
    [Tooltip("Unique id, e.g. \"L1_TutorialIntro\". Used in logs and by triggers.")]
    public string id = "";

    [Header("Lines (in order)")]
    public List<DialogueLine> lines = new List<DialogueLine>();

    [Header("Story state")]
    [Tooltip("This conversation only plays if ALL of these flags are set. Empty = always plays.")]
    public List<string> requiredFlags = new List<string>();

    [Tooltip("Optional: story flag set when the last line closes. Leave empty for none.")]
    public string flagOnEnd = "";

    [Header("Choices (leave empty, or exactly 2)")]
    [Tooltip("Shown after the last line. Empty = no choice, the dialogue just ends.")]
    public List<DialogueChoice> choices = new List<DialogueChoice>();

    // -----------------------------------------------------------------
    // Read helpers used by DialogueManager and triggers
    // -----------------------------------------------------------------

    /// <summary>Number of lines in the conversation.</summary>
    public int LineCount => lines.Count;

    /// <summary>The line at index i, or null if i is out of range.</summary>
    public DialogueLine GetLine(int i)
    {
        return (i >= 0 && i < lines.Count) ? lines[i] : null;
    }

    /// <summary>True if the player picks an option at the end.</summary>
    public bool HasChoices => choices.Count > 0;

    /// <summary>
    /// True if every required flag is set in the current profile.
    /// With no StoryStateManager (e.g. a test scene) it only plays if nothing is required.
    /// </summary>
    public bool CanPlay()
    {
        if (requiredFlags.Count == 0) return true;
        var story = StoryStateManager.Instance;
        return story != null && story.HasAllFlags(requiredFlags);
    }

    /// <summary>
    /// Inspector check: warns in the Console while editing if the dialogue has no id,
    /// no lines, empty lines, or a number of choices other than 0 or 2.
    /// </summary>
    private void OnValidate()
    {
        if (string.IsNullOrWhiteSpace(id))
            Debug.LogWarning("[Dialogue] '" + name + "' has no id.", this);
        for (int i = 0; i < lines.Count; i++)
            if (string.IsNullOrWhiteSpace(lines[i].text))
                Debug.LogWarning("[Dialogue] '" + name + "' line " + (i + 1) + " is empty.", this);
        if (choices.Count != 0 && choices.Count != 2)
            Debug.LogWarning("[Dialogue] '" + name + "' has " + choices.Count +
                             " choices. The dialogue box shows 0 or 2.", this);
        foreach (var c in choices)
            if (c.next == this)
                Debug.LogWarning("[Dialogue] '" + name + "' has a choice that leads back to itself.", this);
    }
}
