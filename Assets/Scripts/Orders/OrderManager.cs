// =====================================================================
// OrderManager.cs
// ---------------------------------------------------------------------
// The "cash register brain" for rated customers. One per Day scene, on
// Managers. No UI of its own: lines go through the DialogueManager.
//
// TakeOrder(customerName, onDone):
//   1. Picks a recipe from the Menu that the player knows (Base recipes
//      always; Bonus recipes only once unlocked in the profile).
//   2. Picks a random order line ("I want {a} {drink}, please :)") and the
//      customer says it in the dialogue box under their own name.
//   3. Starts the drink in FREE mode (every click accepted, timer running).
//   4. On serve: stars = 1 if a step was wrong/missing, 2 if correct but
//      slower than fastSeconds, 3 if correct and fast. The customer says
//      the reaction line for those stars, then OnOrderServed fires and
//      onDone runs.
//
// Only one drink can be made at a time, so orders placed while another
// is open wait in a queue and start when the current one is served.
// The guided tutorial drink is ignored (not rated).
// =====================================================================
using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class OrderManager : MonoBehaviour
{
    /// <summary>The OrderManager in the current level.</summary>
    public static OrderManager Instance { get; private set; }

    [Header("Menu")]
    [Tooltip("Recipes customers can order in this level. Bonus recipes are only ordered once unlocked.")]
    public List<RecipeData> menu = new List<RecipeData>();

    [Header("Order lines ({a} = a/an, {drink} = recipe name, {name} = player)")]
    [TextArea]
    public List<string> orderLines = new List<string>
    {
        "I want {a} {drink}, please :)",
        "I'm running late, give me one {drink} quickly!",
        "I'd love to have {a} {drink} from you.",
        "Good morning! One {drink}, please!",
        "Let's try something new today! {drink}, please."
    };

    [Header("Reactions")]
    [TextArea] public string reactionOneStar = "Hmmm, I hope you improve it next time :(";
    [TextArea] public string reactionTwoStars = "Not bad, but not good.";
    [TextArea] public string reactionThreeStars = "Amazing, I will get this again!";

    [Header("Rating")]
    [Tooltip("Correct and served within this many seconds = 3 stars; slower = 2 stars.")]
    public float fastSeconds = 60f;

    /// <summary>Fired after the reaction line: recipe, stars (1-3), full result.</summary>
    public event Action<RecipeData, int, DrinkResult> OnOrderServed;

    /// <summary>Fired when the customer's order line has been clicked away, just before the drink starts.</summary>
    public event Action<string, RecipeData> OnOrderTaken;

    /// <summary>Rated drinks served in this level.</summary>
    public int ServedCount { get; private set; }
    /// <summary>Total stars earned in this level.</summary>
    public int TotalStars { get; private set; }
    /// <summary>True while an order is open (ordered, not yet served).</summary>
    public bool HasOpenOrder => current != null;
    /// <summary>The recipe of the open order, or null.</summary>
    public RecipeData CurrentRecipe => current != null ? current.recipe : null;
    /// <summary>The customer of the open order (null if none or not given). Stays set until the reaction line ends.</summary>
    public Transform CurrentCustomer => current != null ? current.customer : null;

    /// <summary>One order: who, what, and what to call when done.</summary>
    private class Order
    {
        public string customerName;
        public RecipeData recipe;
        public string line;
        public Action<int> onDone;
        public Transform customer;
    }

    private Order current;
    private readonly Queue<Order> waiting = new Queue<Order>();
    private BrewingSession session;

    // -----------------------------------------------------------------
    // Unity
    // -----------------------------------------------------------------

    private void Awake()
    {
        Instance = this;
    }

    private void Start()
    {
        session = BrewingSession.Instance;
        if (session != null) session.OnDrinkServed += HandleServed;
        else Debug.LogWarning("[Orders] No BrewingSession in the scene.", this);
    }

    private void OnDestroy()
    {
        if (session != null) session.OnDrinkServed -= HandleServed;
        if (Instance == this) Instance = null;
    }

    // -----------------------------------------------------------------
    // Public
    // -----------------------------------------------------------------

    /// <summary>
    /// A customer orders. recipe = null picks from the Menu; line = null picks a
    /// random order line. onDone(stars) runs after the reaction line.
    /// If another order is open, this one waits its turn. customer = who to look at when served.
    /// </summary>
    public void TakeOrder(string customerName, Action<int> onDone = null, RecipeData recipe = null, string line = null, Transform customer = null)
    {
        var order = new Order
        {
            customerName = customerName,
            recipe = recipe != null ? recipe : PickRecipe(),
            line = !string.IsNullOrEmpty(line) ? line : PickLine(),
            onDone = onDone,
            customer = customer
        };
        if (order.recipe == null)
        {
            Debug.LogWarning("[Orders] No recipe on the menu can be ordered yet.", this);
            onDone?.Invoke(0);
            return;
        }

        if (current == null) StartCoroutine(Open(order));
        else waiting.Enqueue(order);
    }

    /// <summary>Stars for a result: 1 = wrong/missing step, 2 = correct but slow, 3 = correct and fast.</summary>
    public int StarsFor(DrinkResult result)
    {
        if (!result.correct) return 1;
        return result.seconds <= fastSeconds ? 3 : 2;
    }

    // -----------------------------------------------------------------
    // Flow
    // -----------------------------------------------------------------

    /// <summary>The customer says the order, then the drink starts (free mode).</summary>
    private IEnumerator Open(Order order)
    {
        current = order;
        yield return SayLine(order.customerName, order.line, order.recipe);
        OnOrderTaken?.Invoke(order.customerName, order.recipe);
        session.StartDrink(order.recipe, false);
        Debug.Log("[Orders] " + order.customerName + " ordered " + order.recipe.displayName);
    }

    /// <summary>Rated drink served: stars, reaction line, event, next order.</summary>
    private void HandleServed(DrinkResult result)
    {
        if (result.guided || current == null) return;
        StartCoroutine(Close(current, result));
    }

    private IEnumerator Close(Order order, DrinkResult result)
    {
        int stars = StarsFor(result);
        ServedCount++;
        TotalStars += stars;
        Debug.Log("[Orders] " + order.customerName + ": " + stars + " star(s) for " + order.recipe.displayName +
                  " (" + result.seconds.ToString("0.0") + " s" + (result.correct ? "" : ", missed: " + result.StepText) + ")");

        yield return SayLine(order.customerName, ReactionFor(stars), order.recipe);

        current = null;
        OnOrderServed?.Invoke(order.recipe, stars, result);
        order.onDone?.Invoke(stars);

        if (waiting.Count > 0) StartCoroutine(Open(waiting.Dequeue()));
    }

    // -----------------------------------------------------------------
    // Helpers
    // -----------------------------------------------------------------

    /// <summary>
    /// Plays one line in the dialogue box under the customer's name and waits
    /// until the player clicks it away (waits first if another conversation plays).
    /// </summary>
    private IEnumerator SayLine(string speakerName, string text, RecipeData recipe)
    {
        var dm = DialogueManager.Instance;
        if (dm == null) yield break;
        while (dm.IsPlaying) yield return null;

        bool done = false;
        if (!dm.PlayLine(Speaker.Customer, text, () => done = true, speakerName, recipe)) yield break;
        while (!done) yield return null;
    }

    /// <summary>Random recipe from the Menu that the player can make (Base, or unlocked Bonus).</summary>
    private RecipeData PickRecipe()
    {
        var story = StoryStateManager.Instance;
        var options = menu.FindAll(r => r != null &&
            (r.kind == RecipeKind.Base || (story != null && story.IsRecipeUnlocked(r.id))));
        return options.Count == 0 ? null : options[UnityEngine.Random.Range(0, options.Count)];
    }

    private string PickLine()
    {
        return orderLines.Count == 0 ? "{drink}, please." : orderLines[UnityEngine.Random.Range(0, orderLines.Count)];
    }

    private string ReactionFor(int stars)
    {
        return stars >= 3 ? reactionThreeStars : stars == 2 ? reactionTwoStars : reactionOneStar;
    }
}
