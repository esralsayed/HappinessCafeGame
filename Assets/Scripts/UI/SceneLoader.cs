using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Moves between scenes with a story loading screen. Sits on the always-active LoadingScreen object
/// inside the HomeMenu prefab (so it exists in the MainMenu and in every level, nothing to set up per scene).
///
/// Levels table: scene name, title, story recap, a recipe whose coffee fact is shown, and the story flags:
///   requiredFlag = must be set before the level can open (empty = always open),
///   completeFlag = set by LevelManager when the level is finished.
///
/// Public calls (buttons / scripts):
///   LoadLevel(index)  - 0 = Day 1 ... ; refused (false) if its required flag is missing.
///   ContinueGame()    - the first level that is not complete yet (the last one if all are).
///   LoadMainMenu()    - back to the start menu.
/// Every load resets time, the frozen-player state and the cursor first, then shows the loading screen
/// for at least minSeconds so the recap can be read.
/// </summary>
public class SceneLoader : MonoBehaviour
{
    [System.Serializable]
    public class LevelEntry
    {
        public string sceneName = "Day 1";
        public string title;
        [TextArea(2, 5)] public string recap;
        public RecipeData factRecipe;
        public string requiredFlag;
        public string completeFlag;
    }

    [SerializeField] private GameObject panel;
    [SerializeField] private TMP_Text titleText;
    [SerializeField] private TMP_Text recapText;
    [SerializeField] private TMP_Text factText;
    [SerializeField] private List<LevelEntry> levels = new List<LevelEntry>();
    [SerializeField] private string mainMenuScene = "MainMenu";
    [SerializeField] private float minSeconds = 2.5f;

    private bool loading;

    /// <summary>True if this level may be opened (its required flag is set).</summary>
    public bool CanLoad(int index)
    {
        if (index < 0 || index >= levels.Count) return false;
        string flag = levels[index].requiredFlag;
        if (string.IsNullOrEmpty(flag)) return true;
        return StoryStateManager.Instance != null && StoryStateManager.Instance.HasFlag(flag);
    }

    /// <summary>Index of the level to play next: first one not complete, else the last.</summary>
    public int NextLevelIndex()
    {
        for (int i = 0; i < levels.Count; i++)
        {
            string done = levels[i].completeFlag;
            bool complete = !string.IsNullOrEmpty(done) && StoryStateManager.Instance != null && StoryStateManager.Instance.HasFlag(done);
            if (!complete) return CanLoad(i) ? i : Mathf.Max(0, i - 1);
        }
        return levels.Count - 1;
    }

    public bool LoadLevel(int index)
    {
        if (loading || !CanLoad(index)) return false;
        var level = levels[index];
        Show(level.title, level.recap, level.factRecipe != null ? level.factRecipe.fact : "");
        StartCoroutine(LoadRoutine(level.sceneName));
        return true;
    }

    /// <summary>Continue playing button (menu): the current or next level.</summary>
    public void ContinueGame()
    {
        if (levels.Count > 0) LoadLevel(NextLevelIndex());
    }

    public void LoadMainMenu()
    {
        if (loading) return;
        Show("", "", "");
        StartCoroutine(LoadRoutine(mainMenuScene));
    }

    private void Show(string title, string recap, string fact)
    {
        if (titleText) titleText.text = title;
        if (recapText) recapText.text = recap;
        if (factText) factText.text = string.IsNullOrEmpty(fact) ? "" : fact;
        panel.SetActive(true);
    }

    private IEnumerator LoadRoutine(string sceneName)
    {
        loading = true;
        // leave a clean state behind: no frozen game, no stuck menu, arrow cursor for the menu
        Time.timeScale = 1f;
        PlayerController.SetPaused(false);
        PlayerController.SetUIOpen(false);
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;

        AsyncOperation op = SceneManager.LoadSceneAsync(sceneName);
        op.allowSceneActivation = false;
        float start = Time.realtimeSinceStartup;
        while (op.progress < 0.9f || Time.realtimeSinceStartup - start < minSeconds) yield return null;
        op.allowSceneActivation = true;
    }
}
