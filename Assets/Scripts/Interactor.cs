using UnityEngine;

public class Interactor : MonoBehaviour
{
    public float reach = 3f;

    void Update()
    {
        if (!Input.GetKeyDown(KeyCode.E)) return;

        Transform cam = Camera.main.transform;
        if (Physics.Raycast(cam.position, cam.forward, out RaycastHit hit, reach))
        {
            Interactable target = hit.collider.GetComponentInParent<Interactable>();
            if (target != null) target.onInteract.Invoke();
        }
    }
}