using System.Collections;
using UnityEngine;
using UnityEngine.AI;

/// <summary>
/// One customer: walks to the counter; a Tutorial customer starts Grandma's lesson and waits
/// for it to end, a Rated customer orders through the OrderManager (or says its own
/// order text in the dialogue box if the scene has none),
/// then finds a chair (a fixed one if given, otherwise any free one) and sits down.
/// Spawned and started by CustomerSpawner.
/// </summary>
[RequireComponent(typeof(NavMeshAgent))]
public class Customer : MonoBehaviour
{
    public float turnSpeed = 6f;
    public float sitTime = 0.6f;
    public float sittingRootOffset = -0.82f;
    public float sittingForwardOffset = 0.15f;

    public string customerName;
    public string order;
    [Tooltip("Stars for this customer's drink (1-3), -1 until served. Set by the OrderManager.")]
    public int starsReceived = -1;

    NavMeshAgent agent;
    Animator animator;
    CustomerSeat seat;
    CustomerSeat preferredSeat;
    System.Action onOrdered;
    VisitKind kind = VisitKind.Rated;

    /// <summary>
    /// Starts this customer's visit. Called by CustomerSpawner right after spawning.
    /// onOrderTaken runs once the player confirms the order; fixedSeat is an optional chair
    /// (e.g. Esra's corner table), otherwise any free chair is used.
    /// </summary>
    public void Begin(string nameText, string orderText, Vector3 orderSpot, Vector3 faceDirection,
                      System.Action onOrderTaken = null, CustomerSeat fixedSeat = null,
                      VisitKind visitKind = VisitKind.Rated)
    {
        customerName = nameText;
        order = orderText;
        onOrdered = onOrderTaken;
        preferredSeat = fixedSeat;
        kind = visitKind;
        agent = GetComponent<NavMeshAgent>();
        animator = GetComponent<Animator>();
        StartCoroutine(Visit(orderSpot, faceDirection));
    }

    IEnumerator Visit(Vector3 orderSpot, Vector3 faceDirection)
    {
        // Walk to the counter and place the order
        yield return WalkTo(orderSpot);
        yield return Turn(faceDirection);

        bool confirmed = false;
        TutorialController tutorial = kind == VisitKind.Tutorial ? FindAnyObjectByType<TutorialController>() : null;
        if (tutorial != null)
        {
            // Tutorial customer: Grandma's lesson runs while this customer waits at the counter.
            bool lessonDone = false;
            System.Action finished = () => lessonDone = true;
            tutorial.OnTutorialFinished += finished;
            tutorial.customer = transform;
            tutorial.Begin();
            while (!lessonDone) yield return null;
            tutorial.OnTutorialFinished -= finished;
            confirmed = true;
        }
        else if (OrderManager.Instance != null)
        {
            // The OrderManager picks a real recipe + order line, the customer says it,
            // and the drink starts. Stars arrive in starsReceived after serving.
            OrderManager.Instance.TakeOrder(customerName, stars => starsReceived = stars, null, null, transform);
            yield return null;
            while (DialogueManager.Instance != null && DialogueManager.Instance.IsPlaying) yield return null;
            confirmed = true;
        }
        else
        {
            // No OrderManager in this scene: say the visit's own order text.
            // If another conversation is playing (e.g. Grandma), wait until it ends.
            while (DialogueManager.Instance != null && DialogueManager.Instance.IsPlaying) yield return null;
            if (DialogueManager.Instance == null ||
                !DialogueManager.Instance.PlayLine(Speaker.Customer, order, () => confirmed = true, customerName))
                confirmed = true;
        }
        while (!confirmed) yield return null;
        onOrdered?.Invoke();   // lets the spawner send the next customer

        // Sit on the fixed chair if it's free, otherwise on any free chair
        seat = (preferredSeat != null && !preferredSeat.taken) ? preferredSeat : CustomerSeat.FindFree();
        if (seat == null) yield break;
        seat.taken = true;

        yield return WalkTo(FindSpotBeside(seat));
        yield return SitDown();
    }

    IEnumerator WalkTo(Vector3 target)
    {
        agent.SetDestination(target);
        animator.SetBool("Walking", true);

        while (agent.pathPending || agent.remainingDistance > agent.stoppingDistance)
            yield return null;

        animator.SetBool("Walking", false);
    }

    IEnumerator Turn(Vector3 direction)
    {
        Quaternion target = Quaternion.LookRotation(direction);
        while (Quaternion.Angle(transform.rotation, target) > 1f)
        {
            transform.rotation = Quaternion.Slerp(transform.rotation, target, turnSpeed * Time.deltaTime);
            yield return null;
        }
    }

    Vector3 FindSpotBeside(CustomerSeat chair)
    {
        // The chair itself isn't walkable, so stop on whichever side has the shorter walk
        Vector3 best = chair.transform.position;
        float bestDistance = float.MaxValue;
        NavMeshPath path = new NavMeshPath();

        foreach (Vector3 side in new[] { chair.transform.right, -chair.transform.right, -chair.transform.forward })
        {
            if (!NavMesh.SamplePosition(chair.transform.position + side * 0.9f, out NavMeshHit hit, 0.6f, NavMesh.AllAreas)) continue;
            if (!agent.CalculatePath(hit.position, path) || path.status != NavMeshPathStatus.PathComplete) continue;

            float distance = 0f;
            for (int i = 1; i < path.corners.Length; i++)
                distance += Vector3.Distance(path.corners[i - 1], path.corners[i]);

            if (distance < bestDistance)
            {
                bestDistance = distance;
                best = hit.position;
            }
        }
        return best;
    }

    // Walk_N calls this on each step, there's no footstep sound yet
    void OnFootstep(AnimationEvent animationEvent) { }

    IEnumerator SitDown()
    {
        agent.enabled = false;
        animator.SetBool("Sitting", true);

        Vector3 startPos = transform.position;
        Quaternion startRot = transform.rotation;
        Vector3 endPos = seat.SeatPosition + Vector3.up * sittingRootOffset + seat.transform.forward * sittingForwardOffset;
        Quaternion endRot = seat.transform.rotation;

        for (float t = 0f; t < 1f; t += Time.deltaTime / sitTime)
        {
            transform.position = Vector3.Lerp(startPos, endPos, t);
            transform.rotation = Quaternion.Slerp(startRot, endRot, t);
            yield return null;
        }
        transform.SetPositionAndRotation(endPos, endRot);
    }
}
