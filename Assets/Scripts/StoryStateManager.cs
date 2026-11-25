// =====================================================================
// StoryStateManager.cs
// ---------------------------------------------------------------------
// The game's memory. One instance lives for the whole game
// (DontDestroyOnLoad) and stores, for the ACTIVE PLAYER PROFILE:
//   - playerName        : fills the {name} token in dialogue
//   - flags             : yes/no story facts (symbolic preconditions/actions),
//                         e.g. "luluWarned", "luluNoteRead", "level1Complete"
//   - counters          : numbers that add up, e.g. "starsTotal"
//   - unlockedRecipes   : recipe ids restored from torn pages
//   - bestStars         : best star result per level (for the Levels page)
//
// Profiles: several players can each have their own save.
//   New Game  -> BeginNewProfile()      (empty profile, in memory only)
//   Name typed -> ConfirmProfileName()  (profile saved to disk under that name)
//   Home / profile list -> GetAllProfiles(), LoadProfile(), DeleteProfile()
// Each profile is one JSON file in Application.persistentDataPath/profiles/.
// Every change is saved immediately. Replaying a level only ADDS data; nothing
// here ever clears a flag, recipe or best-star value.
//
// Testing: if you press Play in a level scene directly (no MainMenu), a manager
// is created automatically with an in-memory "Tester" profile that is never
// written to disk, so real saves are never touched.
//
// Contains no UI. UI scripts call these methods and listen to the events.
// =====================================================================
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;

/// <summary>Everything saved for one player. Serialized to JSON.</summary>
[Serializable]
public class ProfileData
{
    public string playerName = "";
    public string createdAt = "";
    public string lastPlayed = "";
    public List<string> flags = new List<string>();
    public List<CounterEntry> counters = new List<CounterEntry>();
    public List<string> unlockedRecipes = new List<string>();
    public int[] bestStars = new int[3];   // index 0 = Level 1, 1 = Level 2, 2 = Level 3
}

/// <summary>One named counter (JsonUtility cannot save dictionaries).</summary>
[Serializable]
public class CounterEntry
{
    public string key;
    public int value;
}

/// <summary>Result of ConfirmProfileName, so the name prompt UI can show a message.</summary>
public enum NameResult { Ok, Empty, Taken }

public class StoryStateManager : MonoBehaviour
{
    // ---------- Singleton ----------
    /// <summary>The one manager in the game. Any script uses StoryStateManager.Instance.</summary>
    public static StoryStateManager Instance { get; private set; }

    // ---------- Events ----------
    /// <summary>Fired the first time a flag becomes true. Passes the flag name.</summary>
    public event Action<string> OnFlagSet;
    /// <summary>Fired when a counter changes. Passes the key and the new value.</summary>
    public event Action<string, int> OnCounterChanged;
    /// <summary>Fired when a profile is created, named, loaded or deleted-while-active.</summary>
    public event Action OnProfileChanged;

    // ---------- Inspector ----------
    [Tooltip("Name used when a level scene is played directly from the editor.")]
    [SerializeField] private string testProfileName = "Tester";

    [Header("Live view (read-only, for debugging)")]
    [Tooltip("The active profile. Shown here so you can watch flags change in Play mode. Edits made here are not saved.")]
    [SerializeField] private ProfileData active;

    [SerializeField] private bool activeIsSaved;   // false until the player confirms a name (or for the test profile)
    private bool isTestProfile;

    private const string LastProfileKey = "HappinessCafe_LastProfile";
    private static string ProfilesFolder => Path.Combine(Application.persistentDataPath, "profiles");

    // ---------- Read-only properties ----------
    /// <summary>True when a profile is loaded (named or still waiting for a name).</summary>
    public bool HasActiveProfile => active != null;
    /// <summary>The active player's name, or "" if none. Used for the {name} token.</summary>
    public string PlayerName => active != null ? active.playerName : "";

    // =================================================================
    // Unity lifecycle
    // =================================================================

    /// <summary>Makes this the single instance and keeps it alive across scene loads.</summary>
    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);   // a second copy (e.g. returning to MainMenu) removes itself
            return;
        }
        Instance = this;
        DontDestroyOnLoad(gameObject);
        Directory.CreateDirectory(ProfilesFolder);
    }

    /// <summary>Saves one last time when the game closes.</summary>
    private void OnApplicationQuit()
    {
        Save();
    }

    /// <summary>
    /// Editor testing helper: runs after the first scene loads. If no manager exists
    /// (a level was played directly), creates one with an unsaved test profile.
    /// </summary>
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void CreateForTestingIfMissing()
    {
        if (Instance != null) return;
        var go = new GameObject("GameManagers (auto, testing)");
        var manager = go.AddComponent<StoryStateManager>();
        manager.StartTestProfile();
        Debug.Log("[StoryState] No manager in this scene, so a test one was created with an unsaved '" +
                  manager.testProfileName + "' profile.");
    }

    // =================================================================
    // Profiles
    // =================================================================

    /// <summary>
    /// New Game: starts an empty profile in memory. Nothing is written to disk
    /// until ConfirmProfileName() is called from the name prompt.
    /// </summary>
    public void BeginNewProfile()
    {
        active = NewProfileData("");
        activeIsSaved = false;
        isTestProfile = false;
        OnProfileChanged?.Invoke();
    }

    /// <summary>
    /// Name prompt "Confirm": gives the new profile its name and saves it.
    /// Returns Empty if the name is blank, Taken if another profile already uses it
    /// (not case-sensitive), otherwise Ok.
    /// </summary>
    public NameResult ConfirmProfileName(string name)
    {
        name = (name ?? "").Trim();
        if (name.Length == 0) return NameResult.Empty;
        if (ProfileExists(name)) return NameResult.Taken;

        if (active == null) active = NewProfileData("");
        active.playerName = name;
        activeIsSaved = true;
        isTestProfile = false;
        Save();
        PlayerPrefs.SetString(LastProfileKey, name);
        PlayerPrefs.Save();
        OnProfileChanged?.Invoke();
        return NameResult.Ok;
    }

    /// <summary>True if a saved profile with this name exists (not case-sensitive).</summary>
    public bool ProfileExists(string name)
    {
        return File.Exists(PathFor(name));
    }

    /// <summary>
    /// Returns every saved profile, most recently played first.
    /// The profile list UI uses playerName and bestStars from each entry.
    /// </summary>
    public List<ProfileData> GetAllProfiles()
    {
        var list = new List<ProfileData>();
        if (!Directory.Exists(ProfilesFolder)) return list;
        foreach (var file in Directory.GetFiles(ProfilesFolder, "*.json"))
        {
            var data = ReadFile(file);
            if (data != null && data.playerName.Length > 0) list.Add(data);
        }
        return list.OrderByDescending(p => p.lastPlayed).ToList();
    }

    /// <summary>Makes the named saved profile active. Returns false if it does not exist.</summary>
    public bool LoadProfile(string name)
    {
        var data = ReadFile(PathFor(name));
        if (data == null) return false;
        active = data;
        activeIsSaved = true;
        isTestProfile = false;
        Save();   // updates lastPlayed
        PlayerPrefs.SetString(LastProfileKey, active.playerName);
        PlayerPrefs.Save();
        OnProfileChanged?.Invoke();
        return true;
    }

    /// <summary>Loads the profile that was played last (for a "Continue" button). False if none.</summary>
    public bool LoadLastProfile()
    {
        string last = PlayerPrefs.GetString(LastProfileKey, "");
        return last.Length > 0 && LoadProfile(last);
    }

    /// <summary>Deletes a saved profile's file. If it was active, there is no active profile afterwards.</summary>
    public void DeleteProfile(string name)
    {
        string path = PathFor(name);
        if (File.Exists(path)) File.Delete(path);
        if (PlayerPrefs.GetString(LastProfileKey, "").Equals(name, StringComparison.OrdinalIgnoreCase))
            PlayerPrefs.DeleteKey(LastProfileKey);
        if (active != null && active.playerName.Equals(name, StringComparison.OrdinalIgnoreCase))
        {
            active = null;
            activeIsSaved = false;
            OnProfileChanged?.Invoke();
        }
    }

    // =================================================================
    // Flags
    // =================================================================

    /// <summary>
    /// Marks a story fact as true (e.g. "luluNoteRead"), saves, and fires OnFlagSet.
    /// Setting a flag that is already set does nothing (the event fires only once).
    /// </summary>
    public void SetFlag(string flag)
    {
        if (!RequireProfile() || string.IsNullOrEmpty(flag) || active.flags.Contains(flag)) return;
        active.flags.Add(flag);
        Save();
        OnFlagSet?.Invoke(flag);
    }

    /// <summary>True if the flag has been set for the active profile.</summary>
    public bool HasFlag(string flag)
    {
        return active != null && active.flags.Contains(flag);
    }

    /// <summary>True only if every listed flag is set (used for required-flag checks).</summary>
    public bool HasAllFlags(IEnumerable<string> flags)
    {
        return flags == null || flags.All(HasFlag);
    }

    // =================================================================
    // Counters
    // =================================================================

    /// <summary>Adds amount to a counter (creating it at 0 if new), saves, fires OnCounterChanged.</summary>
    public void AddToCounter(string key, int amount = 1)
    {
        if (!RequireProfile() || string.IsNullOrEmpty(key)) return;
        var entry = active.counters.Find(c => c.key == key);
        if (entry == null)
        {
            entry = new CounterEntry { key = key, value = 0 };
            active.counters.Add(entry);
        }
        entry.value += amount;
        Save();
        OnCounterChanged?.Invoke(key, entry.value);
    }

    /// <summary>Current value of a counter, or 0 if it was never used.</summary>
    public int GetCounter(string key)
    {
        if (active == null) return 0;
        var entry = active.counters.Find(c => c.key == key);
        return entry != null ? entry.value : 0;
    }

    // =================================================================
    // Recipes and level results
    // =================================================================

    /// <summary>Adds a recipe id (e.g. "doubleEspresso") to the Recipe Book and saves.</summary>
    public void UnlockRecipe(string recipeId)
    {
        if (!RequireProfile() || string.IsNullOrEmpty(recipeId) || active.unlockedRecipes.Contains(recipeId)) return;
        active.unlockedRecipes.Add(recipeId);
        Save();
    }

    /// <summary>True if the recipe has been unlocked for the active profile.</summary>
    public bool IsRecipeUnlocked(string recipeId)
    {
        return active != null && active.unlockedRecipes.Contains(recipeId);
    }

    /// <summary>
    /// Records stars for a level (levelNumber 1-3). Only keeps the result if it is
    /// better than the saved best, so replaying can never lower it.
    /// </summary>
    public void RecordLevelStars(int levelNumber, int stars)
    {
        if (!RequireProfile()) return;
        int i = levelNumber - 1;
        if (i < 0 || i >= active.bestStars.Length) return;
        if (stars > active.bestStars[i])
        {
            active.bestStars[i] = Mathf.Clamp(stars, 0, 3);
            Save();
        }
    }

    /// <summary>Best stars saved for a level (0 if never completed).</summary>
    public int GetBestStars(int levelNumber)
    {
        int i = levelNumber - 1;
        if (active == null || i < 0 || i >= active.bestStars.Length) return 0;
        return active.bestStars[i];
    }

    // =================================================================
    // Saving
    // =================================================================

    /// <summary>
    /// Writes the active profile to its JSON file. Skipped while the profile has
    /// no name yet, and always skipped for the editor test profile.
    /// </summary>
    public void Save()
    {
        if (active == null || !activeIsSaved || isTestProfile) return;
        active.lastPlayed = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
        try
        {
            Directory.CreateDirectory(ProfilesFolder);
            File.WriteAllText(PathFor(active.playerName), JsonUtility.ToJson(active, true));
        }
        catch (Exception e)
        {
            Debug.LogError("[StoryState] Could not save profile '" + active.playerName + "': " + e.Message);
        }
    }

    // =================================================================
    // Helpers
    // =================================================================

    /// <summary>Starts the in-memory test profile used when a level is played directly.</summary>
    private void StartTestProfile()
    {
        active = NewProfileData(testProfileName);
        activeIsSaved = false;
        isTestProfile = true;
    }

    /// <summary>Creates a blank ProfileData with timestamps filled in.</summary>
    private static ProfileData NewProfileData(string name)
    {
        string now = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
        return new ProfileData { playerName = name, createdAt = now, lastPlayed = now };
    }

    /// <summary>Logs a warning and returns false if there is no active profile.</summary>
    private bool RequireProfile()
    {
        if (active != null) return true;
        Debug.LogWarning("[StoryState] No active profile. Call BeginNewProfile() or LoadProfile() first.");
        return false;
    }

    /// <summary>File path for a profile name: unsafe characters removed, lower-case so names are not case-sensitive.</summary>
    private static string PathFor(string name)
    {
        string safe = new string((name ?? "").Trim().ToLowerInvariant()
            .Select(ch => Path.GetInvalidFileNameChars().Contains(ch) ? '_' : ch).ToArray());
        return Path.Combine(ProfilesFolder, safe + ".json");
    }

    /// <summary>Reads one profile file; returns null if missing or unreadable.</summary>
    private static ProfileData ReadFile(string path)
    {
        if (!File.Exists(path)) return null;
        try
        {
            var data = JsonUtility.FromJson<ProfileData>(File.ReadAllText(path));
            if (data != null && (data.bestStars == null || data.bestStars.Length < 3))
            {
                var fixedStars = new int[3];
                if (data.bestStars != null) Array.Copy(data.bestStars, fixedStars, data.bestStars.Length);
                data.bestStars = fixedStars;
            }
            return data;
        }
        catch (Exception e)
        {
            Debug.LogWarning("[StoryState] Could not read profile file " + path + ": " + e.Message);
            return null;
        }
    }
}
