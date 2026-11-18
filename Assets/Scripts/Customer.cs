using System.Collections;
using UnityEngine;
using UnityEngine.AI;

[RequireComponent(typeof(NavMeshAgent))]
public class Customer : MonoBehaviour
{
    public float turnSpeed = 6f;
    public float sitTime = 0.6f;
    public float sittingRootOffset = -0.82f;
    public float sittingForwardOffset = 0.15f;

    public string customerName;
    public string order;

    NavMeshAgent agent;
    Animator animator;
    CustomerSeat seat;

    public void Begin(string nameText, string orderText, Vector3 orderSpot, Vector3 faceDirection)
    {
        customerName = nameText;
        order = orderText;
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
        OrderCaption.Instance.Show(customerName + ": " + order, () => confirmed = true);
        while (!confirmed) yield return null;

        // Find a free chair and sit down
        seat = CustomerSeat.FindFree();
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
