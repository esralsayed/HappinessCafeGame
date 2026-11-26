// =====================================================================
// AimPrompt.cs
// ---------------------------------------------------------------------
// The aiming dot in the centre of the screen and the contextual prompt
// under it ("Click: Grind", "Click: Small cup", ...). Lives on the
// Crosshair object in the UI prefab; the dot and label are built in Unity.
//
//   - Nothing aimed at: small, soft dot, no text.
//   - Dot on a station: dot grows and turns warm orange, prompt shows the
//     station's PromptText (BrewingStation) or "Click: Use".
//   - Someone talking / a menu open (PlayerController.InputBlocked) or the
//     cursor freed with Esc: dot and prompt hidden.
// Reads Interactor.CurrentTarget; creates no UI.
// =====================================================================
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class AimPrompt : MonoBehaviour
{
    [Header("Parts (inside this prefab)")]
    public Image dot;
    public TMP_Text promptLabel;
    [Tooltip("Background box around the prompt; shown only when there is text.")]
    public GameObject promptBox;

    [Header("Look")]
    public Color idleColor = new Color(1f, 1f, 1f, 0.75f);
    public Color targetColor = new Color(1f, 0.72f, 0.35f, 1f);
    public float idleSize = 8f;
    public float targetSize = 14f;

    [Header("Scene")]
    [Tooltip("Empty = found automatically.")]
    public Interactor interactor;

    private RectTransform dotRect;

    private void Start()
    {
        if (interactor == null) interactor = FindAnyObjectByType<Interactor>();
        if (dot) dotRect = dot.rectTransform;
    }

    /// <summary>Updates the dot and the prompt every frame from what the Interactor is aiming at.</summary>
    private void Update()
    {
        bool hidden = PlayerController.InputBlocked || Cursor.lockState != CursorLockMode.Locked;
        if (dot) dot.enabled = !hidden;
        if (hidden || interactor == null)
        {
            if (promptLabel) promptLabel.text = "";
            if (promptBox) promptBox.SetActive(false);
            return;
        }

        var target = interactor.CurrentTarget;
        bool onTarget = target != null;
        if (dot)
        {
            dot.color = onTarget ? targetColor : idleColor;
            float s = onTarget ? targetSize : idleSize;
            if (dotRect) dotRect.sizeDelta = new Vector2(s, s);
        }
        if (promptLabel) promptLabel.text = onTarget ? PromptFor(target) : "";
        if (promptBox) promptBox.SetActive(onTarget);
    }

    /// <summary>The station's own prompt, or a generic one.</summary>
    private static string PromptFor(Interactable target)
    {
        var station = target.GetComponent<BrewingStation>();
        return station != null ? station.PromptText : "Click: Use";
    }
}
