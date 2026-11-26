// =====================================================================
// CounterFlow.cs
// ---------------------------------------------------------------------
// Front counter <-> back counter flow. Lives on the BackCounterView marker
// inside the Room prefab, so every Day scene has it (no per-scene setup).
//
//   - Level start -> the view faces the customers (frontCounterView), so
//     every level starts the same way without setting it per scene.
//   - A Rated customer comes through the door -> "New customer entered"
//     banner (ToastNotification), so the player turns to take the order.
//     (The tutorial customer gets no banner: the player starts facing them.)
//   - The order has been taken (OrderManager.OnOrderTaken) or Grandma's
//     lesson starts (TutorialController.OnLessonStarted) -> the view SNAPS
//     to the back counter (backCounterView), where the stations are.
//   - A drink is served -> the view SNAPS to the customer who ordered it
//     (OrderManager.CurrentCustomer, or the tutorial customer), so the
//     "Thank you" / reaction line is said face to face.
//
// The snap target is an empty marker placed in the scene, so the angle can
// be changed in Unity without touching code.
// =====================================================================
using UnityEngine;

public class CounterFlow : MonoBehaviour
{
    [Header("Back counter")]
    [Tooltip("Empty marker at the stations. Empty = this object.")]
    public Transform backCounterView;

    [Header("Front counter")]
    [Tooltip("Marker the player faces when the level starts (the customers' side).")]
    public Transform frontCounterView;

    [Header("Customer view")]
    [Tooltip("Height above the customer's feet the view aims at (face).")]
    public float customerFaceHeight = 1.5f;

    [Header("New customer banner")]
    [TextArea] public string newCustomerText = "New customer entered";
    public float bannerSeconds = 3f;
    [Tooltip("Also show the banner for the tutorial customer.")]
    public bool bannerForTutorialCustomer = false;

    [Header("Scene (empty = found automatically)")]
    public PlayerController player;
    public CustomerSpawner spawner;
    public TutorialController tutorial;

    private OrderManager orders;
    private BrewingSession session;

    private void Start()
    {
        if (player == null) player = FindAnyObjectByType<PlayerController>();
        if (spawner == null) spawner = FindAnyObjectByType<CustomerSpawner>();
        if (tutorial == null) tutorial = FindAnyObjectByType<TutorialController>();
        if (backCounterView == null) backCounterView = transform;
        orders = OrderManager.Instance;
        session = BrewingSession.Instance;

        if (spawner != null) spawner.OnCustomerEntered += HandleCustomerEntered;
        if (orders != null) orders.OnOrderTaken += HandleOrderTaken;
        if (tutorial != null) tutorial.OnLessonStarted += SnapToBackCounter;
        if (session != null) session.OnDrinkServed += HandleServed;

        if (player != null && frontCounterView != null) player.LookAt(frontCounterView.position);
    }

    private void OnDestroy()
    {
        if (spawner != null) spawner.OnCustomerEntered -= HandleCustomerEntered;
        if (orders != null) orders.OnOrderTaken -= HandleOrderTaken;
        if (tutorial != null) tutorial.OnLessonStarted -= SnapToBackCounter;
        if (session != null) session.OnDrinkServed -= HandleServed;
    }

    /// <summary>Banner when a customer comes in (Rated only, unless set otherwise).</summary>
    private void HandleCustomerEntered(Customer customer, VisitKind kind)
    {
        if (kind == VisitKind.Tutorial && !bannerForTutorialCustomer) return;
        if (ToastNotification.Instance != null) ToastNotification.Instance.Show(newCustomerText, bannerSeconds);
    }

    private void HandleOrderTaken(string customerName, RecipeData recipe)
    {
        SnapToBackCounter();
    }

    /// <summary>Drink served: turn to the customer who ordered it.</summary>
    private void HandleServed(DrinkResult result)
    {
        Transform who = result.guided ? (tutorial != null ? tutorial.customer : null)
                                      : (orders != null ? orders.CurrentCustomer : null);
        if (player == null || who == null) return;
        player.LookAt(who.position + Vector3.up * customerFaceHeight);
    }

    /// <summary>Instantly turns the player to face the back counter.</summary>
    public void SnapToBackCounter()
    {
        if (player == null || backCounterView == null) return;
        player.LookAt(backCounterView.position);
    }
}
