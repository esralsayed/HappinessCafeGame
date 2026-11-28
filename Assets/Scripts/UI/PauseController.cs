using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Drives the HomeMenu prefab (start page + menu 2). It sits on the always-active HomeMenu root.
///
/// In the MainMenu scene (no PlayerController) it only handles the "Start Menu" button:
/// menu 2 -> start page.
///
/// In a level (a PlayerController exists) the same prefab is the PAUSE menu:
///   - both pages start hidden; Esc shows menu 2, Esc again (or Continue playing) hides it;
///   - while shown: time stops, the arrow cursor is free and the player is frozen;
///   - Esc while the Recipe Book is open closes the book first;
///   - "Start Menu" leaves the level for the MainMenu scene and resets the cursor / frozen state.
/// Buttons call Continue() and StartMenu() (wired inside the prefab).
/// </summary>
public class PauseController : MonoBehaviour
{
    [SerializeField] private GameObject startPage;
    [SerializeField] private GameObject menuPage;
    [SerializeField] private RecipeBook recipeBook;
    [SerializeField] private GameObject pauseButton;   // HUD "Pause" tab: levels only, hidden while paused
    [SerializeField] private SceneLoader sceneLoader;

    private bool inLevel;
    private bool paused;

    private void Start()
    {
        inLevel = FindAnyObjectByType<PlayerController>() != null;
        if (pauseButton != null) pauseButton.SetActive(inLevel);
        if (inLevel)
        {
            if (startPage != null) startPage.SetActive(false);
            if (menuPage != null) menuPage.SetActive(false);
        }
    }

    private void OnDisable()
    {
        if (paused) Time.timeScale = 1f;   // never leave the game frozen (e.g. stopping Play while paused)
    }

    private void Update()
    {
        if (!inLevel || !Input.GetKeyDown(KeyCode.Escape)) return;

        if (recipeBook != null && recipeBook.IsOpen) { recipeBook.Close(); return; }
        if (paused) Resume(); else Pause();
    }

    /// <summary>Continue playing button: resumes in a level; in the menu, loads the current or next level.</summary>
    public void Continue()
    {
        if (inLevel && paused) Resume();
        else if (!inLevel && sceneLoader != null) sceneLoader.ContinueGame();
    }

    /// <summary>Start Menu button: back to the start page (menu) or out of the level (pause).</summary>
    public void StartMenu()
    {
        if (inLevel)
        {
            if (sceneLoader != null) sceneLoader.LoadMainMenu();
            return;
        }
        if (menuPage != null) menuPage.SetActive(false);
        if (startPage != null) startPage.SetActive(true);
    }

    /// <summary>Opens the pause menu (Esc, or the Pause tab when the cursor is free).</summary>
    public void Pause()
    {
        if (PlayerController.UIOpen) return;   // another panel (Serve, choices) owns the cursor
        paused = true;
        if (pauseButton != null) pauseButton.SetActive(false);
        menuPage.SetActive(true);
        Time.timeScale = 0f;
        PlayerController.SetUIOpen(true);
    }

    private void Resume()
    {
        paused = false;
        if (pauseButton != null) pauseButton.SetActive(true);
        menuPage.SetActive(false);
        Time.timeScale = 1f;
        PlayerController.SetUIOpen(false);
    }
}
