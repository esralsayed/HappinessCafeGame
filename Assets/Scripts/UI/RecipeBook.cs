// =====================================================================
// RecipeBook.cs
// ---------------------------------------------------------------------
// Grandma's recipe book as a menu. It is opened with a button (the main
// menu's "Recipe Book" button and the Pause menu's "Recipe Book" button
// call Open()), never with a key.
//
// How it works:
//   1. Open()      -> the book appears CLOSED, showing the cover.
//   2. Click the cover (OpenBook) -> the book opens to the spread that
//      holds the newest unlocked recipe.
//   3. The < and > buttons flip between spreads (2 recipes per spread).
//   4. X (Close) closes the book again; it will start closed next time.
//
// Each spread is ONE image drawn in Photoshop. Which image is shown
// depends on how many recipes of that spread are unlocked:
//   - none      -> allLockedSprite (the shared "2 pages locked" image)
//   - left only -> spreads[i].leftOnly
//   - both      -> spreads[i].both
// The first recipe is always unlocked. Every other recipe is unlocked when
// StoryStateManager.IsRecipeUnlocked(recipe.id) is true.
//
// The book is a prefab nested in UI > OrderCanvas, so every level has it.
// This script sits on the prefab root (always active); only the Panel
// child is shown or hidden, like the DialogueBox. All images and buttons
// are real objects set in the Inspector (no UI created in code).
// While the book is open the cursor is freed (PlayerController.SetUIOpen).
// =====================================================================
using System;
using UnityEngine;
using UnityEngine.UI;

public class RecipeBook : MonoBehaviour
{
    /// <summary>The two Photoshop images for one spread (two recipes side by side).</summary>
    [Serializable]
    public class SpreadSprites
    {
        [Tooltip("Left recipe unlocked, right recipe still a torn page.")]
        public Sprite leftOnly;
        [Tooltip("Both recipes unlocked.")]
        public Sprite both;
    }

    [Header("Objects in the prefab")]
    [Tooltip("The visible part (hidden until Open). Child 'Panel'.")]
    public GameObject panel;
    [Tooltip("The closed book. Clicking it opens the book. Needs a Button.")]
    public GameObject cover;
    [Tooltip("The Image that shows the open spread.")]
    public Image spreadImage;
    [Tooltip("Flip to the previous spread. Hidden on the first spread.")]
    public GameObject prevButton;
    [Tooltip("Flip to the next spread. Hidden on the last spread.")]
    public GameObject nextButton;

    [Header("Recipes, in book order (2 per spread)")]
    [Tooltip("Espresso, Double Espresso, Cappuccino, Vanilla Latte, Cinnamon Vanilla Latte, Caramel Latte.")]
    public RecipeData[] recipes;

    [Header("Spread images (Photoshop)")]
    [Tooltip("Shown when neither recipe of a spread is unlocked (shared by all spreads).")]
    public Sprite allLockedSprite;
    [Tooltip("One entry per spread: 3 entries for 6 recipes.")]
    public SpreadSprites[] spreads;

    private int currentSpread;
    private bool isOpen;
    private bool lockedPlayer;   // true if Open() froze a player (so Close() must free it again)

    /// <summary>Number of spreads: two recipes per spread, rounded up.</summary>
    public int SpreadCount => recipes == null ? 0 : (recipes.Length + 1) / 2;

    /// <summary>True while the book is on screen (closed or open).</summary>
    public bool IsOpen => isOpen;

    /// <summary>Hides the whole book when the scene starts.</summary>
    private void Awake()
    {
        if (panel != null) panel.SetActive(false);
    }

    // =================================================================
    // Buttons (wired in the prefab)
    // =================================================================

    /// <summary>
    /// Main menu / Pause menu button: shows the book CLOSED (cover only)
    /// and frees the cursor so the buttons can be clicked.
    /// </summary>
    public void Open()
    {
        if (isOpen || panel == null) return;
        isOpen = true;
        panel.SetActive(true);
        ShowClosed();
        // In a level the player is frozen and the cursor freed while the book is open.
        // In the main menu there is no player and the cursor is already free, so nothing to do.
        lockedPlayer = FindAnyObjectByType<PlayerController>() != null && !PlayerController.UIOpen;   // already open (pause menu) = it owns the cursor
        if (lockedPlayer) PlayerController.SetUIOpen(true);
    }

    /// <summary>Cover click: opens the book at the spread with the newest unlocked recipe.</summary>
    public void OpenBook()
    {
        if (!isOpen) return;
        currentSpread = Mathf.Clamp(NewestUnlockedIndex() / 2, 0, Mathf.Max(0, SpreadCount - 1));
        ShowSpread();
    }

    /// <summary>X button: closes the book (it will start closed next time) and locks the cursor again.</summary>
    public void Close()
    {
        if (!isOpen) return;
        isOpen = false;
        if (panel != null) panel.SetActive(false);
        if (lockedPlayer) PlayerController.SetUIOpen(false);
        lockedPlayer = false;
    }

    /// <summary> &gt; button: next spread.</summary>
    public void NextSpread()
    {
        if (currentSpread >= SpreadCount - 1) return;
        currentSpread++;
        ShowSpread();
    }

    /// <summary> &lt; button: previous spread.</summary>
    public void PrevSpread()
    {
        if (currentSpread <= 0) return;
        currentSpread--;
        ShowSpread();
    }

    // =================================================================
    // Showing things
    // =================================================================

    /// <summary>Closed book: only the cover (and the dim layer behind it).</summary>
    private void ShowClosed()
    {
        if (cover != null) cover.SetActive(true);
        if (spreadImage != null) spreadImage.gameObject.SetActive(false);
        if (prevButton != null) prevButton.SetActive(false);
        if (nextButton != null) nextButton.SetActive(false);
    }

    /// <summary>Open book: picks the right image for currentSpread and shows only the buttons that make sense.</summary>
    private void ShowSpread()
    {
        if (cover != null) cover.SetActive(false);
        if (spreadImage != null)
        {
            spreadImage.sprite = SpriteForSpread(currentSpread);
            spreadImage.gameObject.SetActive(true);
        }
        if (prevButton != null) prevButton.SetActive(currentSpread > 0);
        if (nextButton != null) nextButton.SetActive(currentSpread < SpreadCount - 1);
    }

    /// <summary>The Photoshop image that matches how many recipes of this spread are unlocked.</summary>
    private Sprite SpriteForSpread(int spread)
    {
        bool left = IsUnlocked(spread * 2);
        bool right = IsUnlocked(spread * 2 + 1);
        SpreadSprites s = (spreads != null && spread < spreads.Length) ? spreads[spread] : null;

        if (!left && !right) return allLockedSprite;
        if (s == null) return allLockedSprite;
        // The right page is never open while the left one is locked (recipes unlock in order),
        // so "right only" falls back to the left-only image.
        return right && left ? s.both : s.leftOnly;
    }

    // =================================================================
    // Unlock checks
    // =================================================================

    /// <summary>True if recipe number i (0 = first) is unlocked. The first recipe is always unlocked.</summary>
    private bool IsUnlocked(int i)
    {
        if (recipes == null || i < 0 || i >= recipes.Length || recipes[i] == null) return false;
        if (i == 0) return true;
        var state = StoryStateManager.Instance;
        return state != null && state.IsRecipeUnlocked(recipes[i].id);
    }

    /// <summary>Index of the newest unlocked recipe, so the book opens where the latest page is.</summary>
    private int NewestUnlockedIndex()
    {
        for (int i = (recipes == null ? 0 : recipes.Length) - 1; i >= 0; i--)
            if (IsUnlocked(i)) return i;
        return 0;
    }

    /// <summary>Inspector check: warns about missing objects or a wrong number of spreads.</summary>
    private void OnValidate()
    {
        if (recipes != null && spreads != null && spreads.Length != 0 && spreads.Length != SpreadCount)
            Debug.LogWarning("[RecipeBook] " + recipes.Length + " recipes need " + SpreadCount +
                             " spread entries, but there are " + spreads.Length + ".", this);
    }
}
