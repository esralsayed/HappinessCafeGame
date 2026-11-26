using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>Tutorial = Grandma's lesson customer (no rating). Rated = orders through the OrderManager.</summary>
public enum VisitKind { Rated, Tutorial }

/// <summary>One customer's visit. Leave any field empty to have it picked at random from the spawner's pools.</summary>
[Serializable]
public class CustomerVisit
{
    [Tooltip("Tutorial: starts Grandma's lesson at the counter. Rated: orders through the OrderManager.")]
    public VisitKind kind = VisitKind.Rated;
    [Tooltip("Empty = random name")] public string customerName;
    [TextArea, Tooltip("Empty = random order")] public string order;
    [Tooltip("Empty = random model")] public Customer prefab;
    [Tooltip("Empty = any free chair")] public CustomerSeat seat;
}

/// <summary>
/// Sends the day's customers into the cafe one at a time: opens the door, spawns each
/// customer, waits until their order is taken, then sends the next one.
/// </summary>
public class CustomerSpawner : MonoBehaviour
{
    [Header("Random pools (used when a visit field is empty)")]
    [Tooltip("Default model, used when the Customer Models pool is empty")]
    public Customer customerPrefab;
    public List<Customer> customerModels = new List<Customer>();
    public List<string> randomNames = new List<string> { "Omar", "Mariam", "Youssef", "Laila", "Ali", "Nada" };
    [TextArea] public List<string> randomOrders = new List<string> { "One espresso, please.", "An Americano, please." };

    [Header("Today's customers, in order")]
    public List<CustomerVisit> visits = new List<CustomerVisit>();
    public float startDelay = 2f;
    public float timeBetweenCustomers = 4f;
    [Tooltip("Turn off if a story beat should start the queue by calling StartQueue()")]
    public bool startOnAwake = true;

    [Header("Door")]
    public Transform door;
    public Transform doorHinge;
    public float doorOpenAngle = -80f;
    public float doorSwingTime = 0.5f;
    public float doorHoldTime = 1.5f;

    [Header("Points")]
    public Transform spawnPoint;
    public Transform orderSpot;

    /// <summary>Number of Rated visits in today's list (used by the LevelManager).</summary>
    public int RatedVisitCount => visits.FindAll(v => v.kind == VisitKind.Rated).Count;

    /// <summary>Raised once every customer in the list has placed their order.</summary>
    public event Action OnAllCustomersArrived;

    /// <summary>A customer came through the door (passes the customer and the visit kind).</summary>
    public event Action<Customer, VisitKind> OnCustomerEntered;

    void Start()
    {
        if (startOnAwake) StartQueue();
    }

    /// <summary>Starts sending today's customers in, one after another.</summary>
    public void StartQueue() => StartCoroutine(RunQueue());

    IEnumerator RunQueue()
    {
        yield return new WaitForSeconds(startDelay);

        foreach (CustomerVisit visit in visits)
        {
            // Fill any empty field from the pools
            Customer prefab = visit.prefab != null ? visit.prefab
                            : customerModels.Count > 0 ? Pick(customerModels) : customerPrefab;
            string nameText = string.IsNullOrEmpty(visit.customerName) ? Pick(randomNames) : visit.customerName;
            string orderText = string.IsNullOrEmpty(visit.order) ? Pick(randomOrders) : visit.order;

            bool ordered = false;
            yield return SwingDoor(doorOpenAngle);
            Customer customer = Instantiate(prefab, spawnPoint.position, spawnPoint.rotation);
            customer.Begin(nameText, orderText, orderSpot.position, orderSpot.forward,
                           () => ordered = true, visit.seat, visit.kind);
            OnCustomerEntered?.Invoke(customer, visit.kind);
            yield return new WaitForSeconds(doorHoldTime);
            yield return SwingDoor(-doorOpenAngle);

            while (!ordered) yield return null;   // wait until this customer's order is taken
            yield return new WaitForSeconds(timeBetweenCustomers);
        }

        OnAllCustomersArrived?.Invoke();
    }

    /// <summary>Returns a random item from a list, or the default value if the list is empty.</summary>
    static T Pick<T>(List<T> list) => list.Count == 0 ? default : list[UnityEngine.Random.Range(0, list.Count)];

    /// <summary>Rotates the door around its hinge by the given angle over doorSwingTime.</summary>
    IEnumerator SwingDoor(float angle)
    {
        float swung = 0f;
        while (Mathf.Abs(swung) < Mathf.Abs(angle))
        {
            float step = angle * Time.deltaTime / doorSwingTime;
            if (Mathf.Abs(swung + step) > Mathf.Abs(angle)) step = angle - swung;
            door.RotateAround(doorHinge.position, Vector3.up, step);
            swung += step;
            yield return null;
        }
    }
}
