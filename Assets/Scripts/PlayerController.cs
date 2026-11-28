// =====================================================================
// PlayerController.cs
// ---------------------------------------------------------------------
// First-person barista: mouse-look (cursor hidden + locked), WASD, jump.
//
// Three states (static, so any script can switch them):
//   Playing  : look + walk; cursor hidden and locked.
//   Paused   : someone is talking. Look + walk still work (no freeze), cursor
//              stays hidden + locked (SetPaused). Stations and jump are blocked,
//              so click / Space / E only continue the dialogue.
//   UI open  : real buttons on screen (ending choice, Serve panel). Frozen,
//              arrow cursor shown (SetUIOpen).
// InputBlocked is true in Paused or UI open, and for the one frame after
// either ends, so the click that closes a dialogue never also uses a station.
// =====================================================================
using UnityEngine;

[RequireComponent(typeof(CharacterController))]
public class PlayerController : MonoBehaviour
{
    public float moveSpeed = 3f;
    public float jumpHeight = 1f;
    public float mouseSensitivity = 2f;
    public Transform cameraPivot;

    public static bool UIOpen { get; private set; }
    /// <summary>True while someone is talking: look + walk still work, stations and jump are blocked.</summary>
    public static bool Paused { get; private set; }
    /// <summary>True while Paused or UI open (and on the frame one of them ends). Stations ignore clicks then.</summary>
    public static bool InputBlocked => UIOpen || Paused || Time.frameCount <= unblockFrame;
    static int unblockFrame = -1;

    // Editor with domain reload off keeps statics between Play sessions: stopping Play while a menu
    // was open would leave the next session frozen. Reset at every Play start.
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics() { UIOpen = false; Paused = false; unblockFrame = -1; }

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
        if (Cursor.lockState == CursorLockMode.Locked && !UIOpen)
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
            if (!InputBlocked && Input.GetButtonDown("Jump"))
                yVelocity = Mathf.Sqrt(2f * -Physics.gravity.y * jumpHeight);
        }
        else
        {
            yVelocity += Physics.gravity.y * Time.deltaTime;
        }

        cc.Move((move * moveSpeed + Vector3.up * yVelocity) * Time.deltaTime);
    }

    /// <summary>
    /// Instantly turns the player (yaw) and the camera (pitch) so the centre of the
    /// screen points at a world position. Used to snap the view to the back counter.
    /// </summary>
    public void LookAt(Vector3 worldPoint)
    {
        Vector3 flat = worldPoint - transform.position;
        flat.y = 0f;
        if (flat.sqrMagnitude > 0.0001f) transform.rotation = Quaternion.LookRotation(flat);

        if (cameraPivot == null) return;
        Vector3 d = worldPoint - cameraPivot.position;
        float horizontal = new Vector2(d.x, d.z).magnitude;
        pitch = Mathf.Clamp(-Mathf.Atan2(d.y, horizontal) * Mathf.Rad2Deg, -80f, 80f);
        cameraPivot.localRotation = Quaternion.Euler(pitch, 0f, 0f);
    }

    // While a UI panel is open the player stands still and the cursor is free to click it
    public static void SetUIOpen(bool open)
    {
        UIOpen = open;
        if (!open) unblockFrame = Time.frameCount;
        SetCursorLocked(!open);
    }

    /// <summary>
    /// Someone is talking: blocks stations and jump (InputBlocked) but keeps looking and
    /// walking, and keeps the cursor hidden and locked (no arrow).
    /// </summary>
    public static void SetPaused(bool paused)
    {
        Paused = paused;
        if (!paused) unblockFrame = Time.frameCount;
        if (!UIOpen) SetCursorLocked(true);
    }

    static void SetCursorLocked(bool locked)
    {
        Cursor.lockState = locked ? CursorLockMode.Locked : CursorLockMode.None;
        Cursor.visible = !locked;
    }
}