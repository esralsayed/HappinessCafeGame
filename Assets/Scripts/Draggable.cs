// =====================================================================
// Draggable.cs
// ---------------------------------------------------------------------
// An object the player can HOLD (left mouse, via DragController), carry and
// drop. Used for the carry cup: hold it, bring it to the serve spot, let go.
//   - While held, its colliders are off so they don't block the aim.
//   - Dropped within snapDistance of dropPoint: snaps there, placed = true,
//     onPlaced fires (e.g. the serve spot's BrewingStation.Use()).
//   - Dropped anywhere else: jumps back to where it started.
//   - ReturnToStart() puts it back and makes it grabbable again; it can be
//     called from any UnityEvent, or automatically after returnAfterPlacedSeconds.
// =====================================================================
using UnityEngine;
using UnityEngine.Events;

public class Draggable : MonoBehaviour
{
    [Tooltip("Where this object can be dropped (e.g. the serve spot). Empty = it always returns to its start.")]
    public Transform dropPoint;

    [Tooltip("How close (metres) to the drop point it must be released to snap there.")]
    public float snapDistance = 0.6f;

    [Tooltip("Seconds to stay on the drop point before going back to the start by itself. 0 = stay there.")]
    public float returnAfterPlacedSeconds = 0f;

    [Tooltip("Runs when the object snaps onto the drop point.")]
    public UnityEvent onPlaced = new UnityEvent();

    [HideInInspector] public bool placed;
    Vector3 startPos;
    Quaternion startRot;
    Collider[] cols;

    /// <summary>Remembers the start position/rotation and this object's colliders.</summary>
    void Awake()
    {
        startPos = transform.position;
        startRot = transform.rotation;
        cols = GetComponentsInChildren<Collider>();
    }

    /// <summary>Turns all colliders on this object on or off.</summary>
    void SetColliders(bool on)
    {
        foreach (Collider c in cols) c.enabled = on;
    }

    /// <summary>Called by DragController when the player grabs it: colliders off while held.</summary>
    public void OnGrab() { SetColliders(false); }

    /// <summary>
    /// Called by DragController when the player lets go: snaps to the drop point
    /// (and fires onPlaced) if close enough, otherwise returns to the start.
    /// </summary>
    public void OnRelease()
    {
        SetColliders(true);
        if (dropPoint != null && Vector3.Distance(transform.position, dropPoint.position) <= snapDistance)
        {
            transform.position = dropPoint.position;
            transform.rotation = dropPoint.rotation;
            placed = true;
            onPlaced.Invoke();
            if (returnAfterPlacedSeconds > 0f) Invoke(nameof(ReturnToStart), returnAfterPlacedSeconds);
        }
        else
        {
            transform.position = startPos;
            transform.rotation = startRot;
        }
    }

    /// <summary>
    /// Puts the object back where it started and makes it grabbable again
    /// (placed = false, colliders on). Safe to call from UnityEvents.
    /// </summary>
    public void ReturnToStart()
    {
        CancelInvoke(nameof(ReturnToStart));
        transform.position = startPos;
        transform.rotation = startRot;
        placed = false;
        if (cols != null) SetColliders(true);
    }
}
