using UnityEngine;

[RequireComponent(typeof(CharacterController))]
public class PlayerController : MonoBehaviour
{
    public float moveSpeed = 3f;
    public float jumpHeight = 1f;
    public float mouseSensitivity = 2f;
    public Transform cameraPivot;

    public static bool UIOpen { get; private set; }

    CharacterController cc;
    float pitch;
    float yVelocity;

    void Start()
    {
        cc = GetComponent<CharacterController>();
        SetCursorLocked(true);
    }

    void Update()
    {
        // Esc frees the cursor, clicking back into the game locks it again
        if (!UIOpen)
        {
            if (Input.GetKeyDown(KeyCode.Escape)) SetCursorLocked(false);
            else if (Input.GetMouseButtonDown(0)) SetCursorLocked(true);
        }

        // Look around
        if (Cursor.lockState == CursorLockMode.Locked)
        {
            float mx = Input.GetAxis("Mouse X") * mouseSensitivity;
            float my = Input.GetAxis("Mouse Y") * mouseSensitivity;
            transform.Rotate(0f, mx, 0f);
            pitch = Mathf.Clamp(pitch - my, -80f, 80f);
            cameraPivot.localRotation = Quaternion.Euler(pitch, 0f, 0f);
        }

        // Move (WASD)
        Vector3 move = transform.right * Input.GetAxis("Horizontal")
                     + transform.forward * Input.GetAxis("Vertical");
        move = UIOpen ? Vector3.zero : Vector3.ClampMagnitude(move, 1f);

        // Jump (Space) and gravity
        if (cc.isGrounded)
        {
            yVelocity = -1f;
            if (!UIOpen && Input.GetButtonDown("Jump"))
                yVelocity = Mathf.Sqrt(2f * -Physics.gravity.y * jumpHeight);
        }
        else
        {
            yVelocity += Physics.gravity.y * Time.deltaTime;
        }

        cc.Move((move * moveSpeed + Vector3.up * yVelocity) * Time.deltaTime);
    }

    // While a UI panel is open the player stands still and the cursor is free to click it
    public static void SetUIOpen(bool open)
    {
        UIOpen = open;
        SetCursorLocked(!open);
    }

    static void SetCursorLocked(bool locked)
    {
        Cursor.lockState = locked ? CursorLockMode.Locked : CursorLockMode.None;
        Cursor.visible = !locked;
    }
}