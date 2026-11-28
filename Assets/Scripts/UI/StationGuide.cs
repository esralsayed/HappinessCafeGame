// =====================================================================
// StationGuide.cs
// ---------------------------------------------------------------------
// Helps a first-time player find the station Grandma is talking about,
// during the tutorial only (rated customers get no hints).
//
//   Glow  : the target station's model pulses with a soft warm emission
//           until it is used (grinder for "walk to the grinder" / "Grind",
//           the machine's x1 knob, the correct cup, ...). For steps with a
//           choice only the correct part glows.
//   Arrow : when the target is off-screen (or behind you), an arrow at the
//           screen edge points towards it. Hidden while someone is talking.
//
// Lives on the GuideArrow object in the UI prefab (the arrow is built in
// Unity). Asks TutorialController.IsGuideTarget for each BrewingStation.
// Original materials are restored when a station stops glowing.
// =====================================================================
using System.Collections.Generic;
using TMPro;
using UnityEngine;

public class StationGuide : MonoBehaviour
{
    [Header("Arrow (inside this prefab)")]
    [Tooltip("The arrow graphic; points to the RIGHT at rotation 0.")]
    public RectTransform arrow;
    [Tooltip("Distance from the screen edge, in canvas units.")]
    public float edgeMargin = 70f;

    [Header("Glow")]
    public Color glowColor = new Color(1f, 0.6f, 0.2f, 1f);
    [Tooltip("Strongest glow (emission intensity).")]
    public float glowStrength = 0.9f;
    [Tooltip("Pulses per second.")]
    public float pulseSpeed = 1.2f;

    [Header("Scene")]
    [Tooltip("Empty = found automatically.")]
    public TutorialController tutorial;
    [Tooltip("Empty = Camera.main.")]
    public Camera cam;

    /// <summary>Original state of one material instance, to restore after glowing.</summary>
    private class MatState
    {
        public Material mat;
        public bool hadKeyword;
        public Color emission;
    }

    private readonly Dictionary<Renderer, List<MatState>> glowing = new Dictionary<Renderer, List<MatState>>();
    private readonly HashSet<Renderer> wanted = new HashSet<Renderer>();
    private BrewingStation[] stations;
    private RectTransform canvasRect;

    private void Start()
    {
        if (tutorial == null) tutorial = FindAnyObjectByType<TutorialController>();
        if (cam == null) cam = Camera.main;
        stations = FindObjectsByType<BrewingStation>(FindObjectsSortMode.None);
        var canvas = GetComponentInParent<Canvas>();
        if (canvas) canvasRect = canvas.transform as RectTransform;
        if (arrow) arrow.gameObject.SetActive(false);
    }

    private void OnDisable()
    {
        StopAllGlow();
    }

    /// <summary>Every frame: find the target stations, pulse their glow, place the arrow.</summary>
    private void Update()
    {
        wanted.Clear();
        Bounds targetBounds = new Bounds();
        bool hasTarget = false;

        if (tutorial != null && tutorial.IsGuiding && stations != null)
        {
            foreach (var s in stations)
            {
                if (s == null || !tutorial.IsGuideTarget(s)) continue;
                foreach (var r in RenderersFor(s))
                {
                    wanted.Add(r);
                    if (!hasTarget) { targetBounds = r.bounds; hasTarget = true; }
                    else targetBounds.Encapsulate(r.bounds);
                }
            }
        }

        // Stop glowing what is no longer a target, start/keep glowing the rest.
        var stop = new List<Renderer>();
        foreach (var r in glowing.Keys) if (!wanted.Contains(r)) stop.Add(r);
        foreach (var r in stop) StopGlow(r);

        float pulse = (Mathf.Sin(Time.time * pulseSpeed * Mathf.PI * 2f) + 1f) * 0.5f;   // 0..1
        Color e = glowColor * Mathf.Lerp(0.15f, glowStrength, pulse);
        foreach (var r in wanted) SetGlow(r, e);

        UpdateArrow(hasTarget, targetBounds.center);
    }

    // -----------------------------------------------------------------
    // Arrow
    // -----------------------------------------------------------------

    /// <summary>Shows the arrow at the screen edge when the target is off-screen or behind the camera.</summary>
    private void UpdateArrow(bool hasTarget, Vector3 worldPos)
    {
        if (arrow == null) return;
        if (!hasTarget || cam == null || canvasRect == null || PlayerController.InputBlocked)
        {
            arrow.gameObject.SetActive(false);
            return;
        }

        Vector3 sp = cam.WorldToScreenPoint(worldPos);
        bool behind = sp.z < 0f;
        bool onScreen = !behind && sp.x > 0f && sp.x < Screen.width && sp.y > 0f && sp.y < Screen.height;
        if (onScreen) { arrow.gameObject.SetActive(false); return; }

        // Direction from the screen centre to the target (flipped if behind the camera).
        Vector2 centre = new Vector2(Screen.width * 0.5f, Screen.height * 0.5f);
        Vector2 dir = new Vector2(sp.x, sp.y) - centre;
        if (behind) dir = -dir;
        if (dir.sqrMagnitude < 0.01f) dir = Vector2.down;
        dir.Normalize();

        // Place on the canvas edge (canvas units), keeping edgeMargin away from it.
        Vector2 half = canvasRect.rect.size * 0.5f - new Vector2(edgeMargin, edgeMargin);
        float scale = Mathf.Min(Mathf.Abs(half.x / (dir.x == 0f ? 0.0001f : dir.x)),
                                Mathf.Abs(half.y / (dir.y == 0f ? 0.0001f : dir.y)));
        arrow.anchoredPosition = dir * scale;
        arrow.localRotation = Quaternion.Euler(0f, 0f, Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg);
        arrow.gameObject.SetActive(true);
    }

    // -----------------------------------------------------------------
    // Glow
    // -----------------------------------------------------------------

    /// <summary>
    /// The visible models for a station: its own renderers, or (for invisible
    /// hotspots on a one-piece model) its parent's. 3D text labels are skipped.
    /// </summary>
    private static List<Renderer> RenderersFor(BrewingStation s)
    {
        var list = Collect(s.transform);
        if (list.Count == 0 && s.transform.parent != null) list = Collect(s.transform.parent);
        return list;
    }

    private static List<Renderer> Collect(Transform root)
    {
        var list = new List<Renderer>();
        foreach (var r in root.GetComponentsInChildren<Renderer>())
            if (r.enabled && r.GetComponent<TMP_Text>() == null) list.Add(r);
        return list;
    }

    /// <summary>Turns on (first time) and updates the emission on a renderer's own material instances.</summary>
    private void SetGlow(Renderer r, Color emission)
    {
        if (!glowing.TryGetValue(r, out var states))
        {
            states = new List<MatState>();
            foreach (var m in r.materials)   // instances: shared materials stay untouched
            {
                var st = new MatState { mat = m, hadKeyword = m.IsKeywordEnabled("_EMISSION") };
                st.emission = m.HasProperty("_EmissionColor") ? m.GetColor("_EmissionColor") : Color.black;
                m.EnableKeyword("_EMISSION");
                states.Add(st);
            }
            glowing[r] = states;
        }
        foreach (var st in states)
            if (st.mat.HasProperty("_EmissionColor")) st.mat.SetColor("_EmissionColor", st.emission + emission);
    }

    /// <summary>Puts a renderer's emission back the way it was.</summary>
    private void StopGlow(Renderer r)
    {
        if (glowing.TryGetValue(r, out var states))
        {
            foreach (var st in states)
            {
                if (st.mat == null) continue;
                if (st.mat.HasProperty("_EmissionColor")) st.mat.SetColor("_EmissionColor", st.emission);
                if (!st.hadKeyword) st.mat.DisableKeyword("_EMISSION");
            }
        }
        glowing.Remove(r);
    }

    private void StopAllGlow()
    {
        foreach (var r in new List<Renderer>(glowing.Keys)) StopGlow(r);
    }
}
