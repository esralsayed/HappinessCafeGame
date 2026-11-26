using UnityEngine;

public class DragController : MonoBehaviour
{
    public float reach = 4f;
    public float holdDistance = 1.5f;
    Camera cam;
    Draggable held;

    void Start() { cam = GetComponentInChildren<Camera>(); }

    void Update()
    {
        if (cam == null) return;
        // The cursor is locked, so aim from the center of the screen
        Ray ray = cam.ViewportPointToRay(new Vector3(0.5f, 0.5f, 0f));

        if (Input.GetMouseButtonDown(0) && held == null && !PlayerController.InputBlocked)
        {
            if (Physics.Raycast(ray, out RaycastHit hit, reach))
            {
                Draggable d = hit.collider.GetComponentInParent<Draggable>();
                if (d != null && !d.placed) { held = d; d.OnGrab(); }
            }
        }

        if (held != null)
        {
            held.transform.position = ray.GetPoint(holdDistance);
            if (Input.GetMouseButtonUp(0)) { held.OnRelease(); held = null; }
        }
    }
}