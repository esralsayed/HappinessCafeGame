using UnityEngine;
using UnityEngine.Events;

public class Draggable : MonoBehaviour
{
    public Transform dropPoint;
    public float snapDistance = 0.6f;
    public UnityEvent onPlaced;

    [HideInInspector] public bool placed;
    Vector3 startPos;
    Quaternion startRot;
    Collider[] cols;

    void Awake()
    {
        startPos = transform.position;
        startRot = transform.rotation;
        cols = GetComponentsInChildren<Collider>();
    }

    void SetColliders(bool on)
    {
        foreach (Collider c in cols) c.enabled = on;
    }

    public void OnGrab() { SetColliders(false); }

    public void OnRelease()
    {
        SetColliders(true);
        if (dropPoint != null && Vector3.Distance(transform.position, dropPoint.position) <= snapDistance)
        {
            transform.position = dropPoint.position;
            transform.rotation = dropPoint.rotation;
            placed = true;
            onPlaced.Invoke();
        }
        else
        {
            transform.position = startPos;
            transform.rotation = startRot;
        }
    }
}