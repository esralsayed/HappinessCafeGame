using System.Collections;
using UnityEngine;

public class CustomerSpawner : MonoBehaviour
{
    public Customer customerPrefab;
    public string customerName = "Esra";
    public string order = "I want a cappuccino and a chocolate cake";
    public float startDelay = 2f;

    public Transform door;
    public Transform doorHinge;
    public float doorOpenAngle = -80f;
    public float doorSwingTime = 0.5f;
    public float doorHoldTime = 1.5f;

    public Transform spawnPoint;
    public Transform orderSpot;

    IEnumerator Start()
    {
        yield return new WaitForSeconds(startDelay);
        yield return SwingDoor(doorOpenAngle);

        Customer customer = Instantiate(customerPrefab, spawnPoint.position, spawnPoint.rotation);
        customer.Begin(customerName, order, orderSpot.position, orderSpot.forward);

        yield return new WaitForSeconds(doorHoldTime);
        yield return SwingDoor(-doorOpenAngle);
    }

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
