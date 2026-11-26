// =====================================================================
// DialogueManager.cs
// ---------------------------------------------------------------------
// Plays DialogueData assets in the DialogueBox (the "stage" built in
// Unity). Lives on the DialogueBox prefab root, which stays active; the
// visible box is the child "Panel", shown and hidden from here.
//
// Two ways to show text:
//   Play(...)  A full conversation. Pauses the player (SetPaused: can still look
//              and walk, stations blocked; the arrow appears only for choices), types
//              each line, waits for click / Space / E, sets story flags,
//              shows 0 or 2 choices at the end, then hands control back.
//   PlayLine() One line played like a conversation (pause, click, close),
//              optionally under a custom name. Used for customer orders.
//   Say(...)   One line that stays on screen WITHOUT pausing the player
//              (tutorial step lines: "Grind.", "Choose a cup..."). No input
//              needed; replaced by the next Say or removed with Hide().
//
// Tokens filled in at runtime: {name} {drink} {a} {step}.
// Speaker names and portraits are set once in the Speakers table.
//
// UI is never created here (rule 2): every part is an Inspector field,
// and the choice buttons call Choose(0) / Choose(1) from their On Click
// inside the prefab (rule 4).
// =====================================================================
using System;
using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class DialogueManager : MonoBehaviour
{
    /// <summary>Name and portrait for one speaker.</summary>
    [Serializable]
    public class SpeakerInfo
    {
        public Speaker speaker;
        [Tooltip("Shown above the text. {name} works here (for the Player). Empty = no name shown (Narrator).")]
        public string displayName = "";
        [Tooltip("Optional portrait. Empty = the portrait is hidden.")]
        public Sprite portrait;
    }

    /// <summary>The DialogueManager in the current scene.</summary>
    public static DialogueManager Instance { get; private set; }

    [Header("Box parts (inside this prefab)")]
    [Tooltip("The visible box (child 'Panel'). Shown while text is on screen.")]
    public GameObject panel;
    public Image portrait;
    public TMP_Text nameLabel;
    public TMP_Text bodyText;
    [Tooltip("'Next' / 'Done' hint. Shown only when a line is finished and waits for input.")]
    public GameObject nextHint;
    [Tooltip("Hint text on every line except the last.")]
    public string nextText = "Click: Next";
    [Tooltip("Hint text on the last line (no choices after it).")]
    public string doneText = "Click: Done";

    [Header("Choices")]
    public GameObject choicesRoot;
    [Tooltip("ChoiceA, ChoiceB. Their On Click calls Choose(0) / Choose(1).")]
    public Button[] choiceButtons = new Button[2];
    public TMP_Text[] choiceLabels = new TMP_Text[2];

    [Header("Speakers")]
    public List<SpeakerInfo> speakers = new List<SpeakerInfo>
    {
        new SpeakerInfo { speaker = Speaker.Grandma,  displayName = "Grandma" },
        new SpeakerInfo { speaker = Speaker.Customer, displayName = "Customer" },
        new SpeakerInfo { speaker = Speaker.MrsLulu,  displayName = "Mrs. Lulu" },
        new SpeakerInfo { speaker = Speaker.Narrator, displayName = "" },
        new SpeakerInfo { speaker = Speaker.Player,   displayName = "{name}" },
    };

    [Header("Typing")]
    [Tooltip("Letters per second for the typewriter effect.")]
    public float charsPerSecond = 40f;
    [Tooltip("Used for {name} when there is no profile (testing).")]
    public string fallbackName = "Nour";

    [Header("Testing (editor)")]
    [Tooltip("Optional: plays this conversation when the scene starts.")]
    public DialogueData testDialogue;

    // Events for other systems (objectives, sound, tutorial...)
    public event Action<DialogueData> OnDialogueStarted;
    public event Action<DialogueData, int> OnLineShown;
    public event Action<DialogueData> OnDialogueEnded;

    /// <summary>True while a full conversation (Play) is running.</summary>
    public bool IsPlaying { get; private set; }

    /// <summary>True while a Say line is on screen.</summary>
    public bool IsSaying { get; private set; }

    // Current conversation state
    private DialogueData current;
    private RecipeData recipe;
    private string stepText;
    private Action onFinished;
    private int lineIndex;
    private bool typing;
    private bool waitingForChoice;
    private int ignoreInputFrame;
    private Coroutine typeRoutine;
    private bool cursorFreed;           // true while the choice buttons need the arrow
    private string nameOverride;        // PlayLine: name shown instead of the speaker's
    private DialogueData tempDialogue;  // PlayLine: in-memory one-line conversation


    // -----------------------------------------------------------------
    // Unity
    // -----------------------------------------------------------------

    /// <summary>Registers the instance and starts with the box hidden.</summary>
    private void Awake()
    {
        Instance = this;
        if (panel) panel.SetActive(false);
        if (choicesRoot) choicesRoot.SetActive(false);
    }

    /// <summary>Plays the test conversation, if one is set.</summary>
    private void Start()
    {
        if (testDialogue) Play(testDialogue);
    }

    private void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }

    /// <summary>
    /// During Play: click / Space / E first finishes the typing, then goes to the
    /// next line. Ignored while choices are shown and on the frame Play started
    /// (so the key that opened the conversation does not skip line 1).
    /// </summary>
    private void Update()
    {
        if (!IsPlaying || waitingForChoice || Time.frameCount == ignoreInputFrame) return;

        bool pressed = Input.GetMouseButtonDown(0) || Input.GetKeyDown(KeyCode.Space) || Input.GetKeyDown(KeyCode.E);
        if (!pressed) return;

        if (typing) FinishTyping();
        else NextLine();
    }

    // -----------------------------------------------------------------
    // Public API
    // -----------------------------------------------------------------

    /// <summary>
    /// Plays a full conversation. recipe fills {drink}/{a}, step fills {step}.
    /// onFinished runs when the conversation (and any follow-up from a choice) ends.
    /// Returns false (and does nothing) if the required flags are missing or
    /// another conversation is already playing.
    /// </summary>
    public bool Play(DialogueData dialogue, RecipeData recipe = null, string step = null, Action onFinished = null)
    {
        if (dialogue == null || dialogue.LineCount == 0) return false;
        if (IsPlaying)
        {
            Debug.LogWarning("[Dialogue] '" + dialogue.id + "' skipped: another conversation is playing.", this);
            return false;
        }
        if (!dialogue.CanPlay()) return false;

        IsSaying = false;
        this.recipe = recipe;
        stepText = step;
        this.onFinished = onFinished;
        IsPlaying = true;
        PlayerController.SetPaused(true);
        Begin(dialogue);
        return true;
    }

    /// <summary>
    /// Plays a single line like a conversation (pauses the player, waits for a click,
    /// then closes and runs onFinished). shownName replaces the speaker's name,
    /// e.g. a customer's own name. Used for customer orders.
    /// Returns false if another conversation is already playing.
    /// </summary>
    public bool PlayLine(Speaker speaker, string text, Action onFinished = null, string shownName = null, RecipeData recipe = null)
    {
        if (IsPlaying) return false;
        tempDialogue = ScriptableObject.CreateInstance<DialogueData>();
        tempDialogue.hideFlags = HideFlags.DontSave;
        tempDialogue.id = "line";
        tempDialogue.lines.Add(new DialogueLine { speaker = speaker, text = text });
        nameOverride = shownName;
        if (Play(tempDialogue, recipe, null, onFinished)) return true;
        ClearTemp();
        return false;
    }

    /// <summary>
    /// Shows one line without pausing the player (tutorial step lines).
    /// Stays until the next Say or Hide(). Ignored while a conversation plays.
    /// </summary>
    public void Say(Speaker speaker, string text, RecipeData recipe = null, string step = null)
    {
        if (IsPlaying) return;
        IsSaying = true;
        this.recipe = recipe;
        stepText = step;
        if (choicesRoot) choicesRoot.SetActive(false);
        if (nextHint) nextHint.SetActive(false);
        panel.SetActive(true);
        ShowSpeaker(speaker);
        StartTyping(Fill(text));
    }

    /// <summary>Removes a Say line. (Does not stop a running conversation.)</summary>
    public void Hide()
    {
        if (IsPlaying) return;
        StopTyping();
        IsSaying = false;
        panel.SetActive(false);
    }

    /// <summary>
    /// Called by ChoiceA / ChoiceB On Click (0 or 1). Sets the option's flag,
    /// then plays its follow-up conversation or ends.
    /// </summary>
    public void Choose(int index)
    {
        if (!waitingForChoice || current == null || index < 0 || index >= current.choices.Count) return;

        var choice = current.choices[index];
        waitingForChoice = false;
        choicesRoot.SetActive(false);
        FreeCursorEnd();
        SetFlag(choice.setFlag);

        if (choice.next != null && choice.next.CanPlay()) Begin(choice.next);
        else End();
    }

    // -----------------------------------------------------------------
    // Conversation flow
    // -----------------------------------------------------------------

    /// <summary>Starts a conversation (also used for a choice's follow-up).</summary>
    private void Begin(DialogueData dialogue)
    {
        current = dialogue;
        lineIndex = 0;
        waitingForChoice = false;
        ignoreInputFrame = Time.frameCount;
        if (choicesRoot) choicesRoot.SetActive(false);
        panel.SetActive(true);
        OnDialogueStarted?.Invoke(current);
        ShowLine();
    }

    /// <summary>Shows the current line: speaker, flag, typewriter.</summary>
    private void ShowLine()
    {
        var line = current.GetLine(lineIndex);
        ShowSpeaker(line.speaker);
        SetFlag(line.setFlag);
        StartTyping(Fill(line.text));
        OnLineShown?.Invoke(current, lineIndex);
    }

    /// <summary>Goes to the next line, or to the choices / end after the last one.</summary>
    private void NextLine()
    {
        lineIndex++;
        if (lineIndex < current.LineCount) { ShowLine(); return; }

        SetFlag(current.flagOnEnd);
        if (current.HasChoices) ShowChoices();
        else End();
    }

    /// <summary>Shows the 2 choice buttons with their labels.</summary>
    private void ShowChoices()
    {
        waitingForChoice = true;
        if (nextHint) nextHint.SetActive(false);
        for (int i = 0; i < choiceButtons.Length; i++)
        {
            bool has = i < current.choices.Count;
            if (choiceButtons[i]) choiceButtons[i].gameObject.SetActive(has);
            if (has && choiceLabels[i]) choiceLabels[i].text = Fill(current.choices[i].text);
        }
        choicesRoot.SetActive(true);
        PlayerController.SetUIOpen(true);   // arrow only now: there are buttons to click
        cursorFreed = true;
    }

    /// <summary>Hides the box, gives control back, runs onFinished.</summary>
    private void End()
    {
        StopTyping();
        var finished = current;
        var callback = onFinished;
        current = null;
        onFinished = null;
        IsPlaying = false;
        panel.SetActive(false);
        FreeCursorEnd();
        PlayerController.SetPaused(false);
        OnDialogueEnded?.Invoke(finished);
        ClearTemp();
        callback?.Invoke();
    }

    // -----------------------------------------------------------------
    // Helpers
    // -----------------------------------------------------------------

    /// <summary>Sets the name label and portrait from the Speakers table.</summary>
    private void ShowSpeaker(Speaker speaker)
    {
        SpeakerInfo info = speakers.Find(s => s.speaker == speaker);
        string shownName = info != null ? Fill(info.displayName) : speaker.ToString();
        if (IsPlaying && !string.IsNullOrEmpty(nameOverride)) shownName = nameOverride;
        if (nameLabel)
        {
            nameLabel.text = shownName;
            nameLabel.gameObject.SetActive(!string.IsNullOrEmpty(shownName));
        }
        if (portrait)
        {
            Sprite s = info != null ? info.portrait : null;
            portrait.sprite = s;
            portrait.gameObject.SetActive(s != null);
        }
    }

    /// <summary>Hides the arrow again after the choice buttons (back to Paused / Playing).</summary>
    private void FreeCursorEnd()
    {
        if (!cursorFreed) return;
        cursorFreed = false;
        PlayerController.SetUIOpen(false);
    }

    /// <summary>Forgets PlayLine's custom name and its in-memory conversation.</summary>
    private void ClearTemp()
    {
        nameOverride = null;
        if (tempDialogue != null) Destroy(tempDialogue);
        tempDialogue = null;
    }

    /// <summary>Replaces {name}, {drink}, {a} and {step}.</summary>
    private string Fill(string text)
    {
        if (string.IsNullOrEmpty(text)) return "";
        var story = StoryStateManager.Instance;
        string player = story != null && !string.IsNullOrEmpty(story.PlayerName) ? story.PlayerName : fallbackName;
        return text
            .Replace("{name}", player)
            .Replace("{drink}", recipe != null ? recipe.displayName : "coffee")
            .Replace("{a}", recipe != null ? recipe.Article : "a")
            .Replace("{step}", stepText ?? "");
    }

    /// <summary>Sets a story flag if one is given and a StoryStateManager exists.</summary>
    private void SetFlag(string flag)
    {
        if (string.IsNullOrWhiteSpace(flag)) return;
        if (StoryStateManager.Instance != null) StoryStateManager.Instance.SetFlag(flag);
        else Debug.Log("[Dialogue] Flag '" + flag + "' (no StoryStateManager in scene).", this);
    }

    /// <summary>Starts the typewriter for a line (uses maxVisibleCharacters, so rich text is safe).</summary>
    private void StartTyping(string text)
    {
        StopTyping();
        bodyText.text = text;
        bodyText.maxVisibleCharacters = 0;
        if (nextHint) nextHint.SetActive(false);
        typeRoutine = StartCoroutine(TypeRoutine());
    }

    private IEnumerator TypeRoutine()
    {
        typing = true;
        bodyText.ForceMeshUpdate();
        int total = bodyText.textInfo.characterCount;
        float shown = 0f;
        while (shown < total)
        {
            shown += charsPerSecond * Time.unscaledDeltaTime;
            bodyText.maxVisibleCharacters = Mathf.Min(total, Mathf.FloorToInt(shown));
            yield return null;
        }
        FinishTyping();
    }

    /// <summary>Shows the whole line at once; shows the Next hint during a conversation.</summary>
    private void FinishTyping()
    {
        StopTyping();
        bodyText.maxVisibleCharacters = int.MaxValue;
        if (!nextHint) return;
        bool show = IsPlaying && !waitingForChoice;
        nextHint.SetActive(show);
        if (!show) return;
        bool last = lineIndex >= current.LineCount - 1 && !current.HasChoices;
        var hint = nextHint.GetComponent<TMP_Text>();
        if (hint) hint.text = last ? doneText : nextText;
    }

    private void StopTyping()
    {
        if (typeRoutine != null) StopCoroutine(typeRoutine);
        typeRoutine = null;
        typing = false;
    }
}
