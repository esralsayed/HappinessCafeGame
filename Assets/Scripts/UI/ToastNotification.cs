// =====================================================================
// ToastNotification.cs
// ---------------------------------------------------------------------
// A short banner at the top of the screen, e.g. "New customer entered".
// Lives on the Toast object in the UI prefab (box + text built in Unity).
//
//   ToastNotification.Instance.Show("New customer entered", 3f);
//
// Fades in, stays, fades out (unscaled time). A new Show replaces the
// current message and restarts the timer. Creates no UI.
// =====================================================================
using System.Collections;
using TMPro;
using UnityEngine;

[RequireComponent(typeof(CanvasGroup))]
public class ToastNotification : MonoBehaviour
{
    /// <summary>The toast in the current scene.</summary>
    public static ToastNotification Instance { get; private set; }

    [Header("Parts (inside this prefab)")]
    public TMP_Text label;

    [Header("Timing")]
    public float fadeSeconds = 0.25f;
    public float defaultSeconds = 3f;

    private CanvasGroup group;
    private Coroutine routine;

    private void Awake()
    {
        Instance = this;
        group = GetComponent<CanvasGroup>();
        group.alpha = 0f;
        group.blocksRaycasts = false;
        group.interactable = false;
    }

    private void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }

    /// <summary>Shows a message for a few seconds (seconds &lt;= 0 uses the default).</summary>
    public void Show(string text, float seconds = 0f)
    {
        if (label) label.text = text;
        if (routine != null) StopCoroutine(routine);
        routine = StartCoroutine(Run(seconds > 0f ? seconds : defaultSeconds));
    }

    private IEnumerator Run(float seconds)
    {
        for (float t = group.alpha * fadeSeconds; t < fadeSeconds; t += Time.unscaledDeltaTime)
        {
            group.alpha = t / fadeSeconds;
            yield return null;
        }
        group.alpha = 1f;
        yield return new WaitForSecondsRealtime(seconds);
        for (float t = 0f; t < fadeSeconds; t += Time.unscaledDeltaTime)
        {
            group.alpha = 1f - t / fadeSeconds;
            yield return null;
        }
        group.alpha = 0f;
        routine = null;
    }
}
