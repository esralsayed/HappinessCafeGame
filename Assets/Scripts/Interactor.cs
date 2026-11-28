// =====================================================================
// Interactor.cs
// ---------------------------------------------------------------------
// Lets the player USE things by aiming the dot in the centre of the screen.
//   - Left-click or E on an Interactable runs its onInteract event
//     (BrewingStation hooks itself onto that event).
//   - Left-click is ignored on objects with a Draggable: those are grabbed by
//     DragController instead, so one click never both uses and grabs.
//   - Nothing happens while someone is talking or a menu is open (PlayerController.InputBlocked).
//   - CurrentTarget always holds the Interactable the dot is aimed at (or null)
//     and OnTargetChanged fires when it changes: used later by the glow and
//     the contextual prompt ("Click: Grind").
// Aims exactly like DragController: a ray from the centre of the camera view.
// =====================================================================
using System;
using UnityEngine;

public class Interactor : MonoBehaviour
{
    [Tooltip("How far away (metres) the player can use things.")]
    public float reach = 3f;

    [Tooltip("Left mouse button uses things (E always works as a backup).")]
    public bool allowClick = true;

    /// <summary>The Interactable the centre dot is aimed at right now, or null.</summary>
    public Interactable CurrentTarget { get; private set; }

    /// <summary>Fired when CurrentTarget changes (passes the new target, possibly null).</summary>
    public event Action<Interactable> OnTargetChanged;

    private Camera cam;

    /// <summary>Finds the player's camera (child camera first, otherwise the main camera).</summary>
    void Start()
    {
        cam = GetComponentInChildren<Camera>();
        if (cam == null) cam = Camera.main;
    }

    /// <summary>Each frame: update what is aimed at, then use it on click / E.</summary>
    void Update()
    {
        if (cam == null) { cam = Camera.main; if (cam == null) return; }

        // 1. What is the dot aimed at?
        Interactable aimed = null;
        bool aimedIsDraggable = false;
        if (!PlayerController.InputBlocked)
        {
            Ray ray = cam.ViewportPointToRay(new Vector3(0.5f, 0.5f, 0f));
            if (Physics.Raycast(ray, out RaycastHit hit, reach))
            {
                aimed = hit.collider.GetComponentInParent<Interactable>();
                aimedIsDraggable = hit.collider.GetComponentInParent<Draggable>() != null;
            }
        }
        SetTarget(aimed);

        // 2. Use it.
        if (aimed == null) return;
        bool pressedE = Input.GetKeyDown(KeyCode.E);
        bool clicked = allowClick && Input.GetMouseButtonDown(0) && !aimedIsDraggable;
        if (pressedE || clicked) aimed.onInteract.Invoke();
    }

    /// <summary>Stores the new target and fires OnTargetChanged only when it actually changes.</summary>
    void SetTarget(Interactable target)
    {
        if (target == CurrentTarget) return;
        CurrentTarget = target;
        OnTargetChanged?.Invoke(target);
    }
}
