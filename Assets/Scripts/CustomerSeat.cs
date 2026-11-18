using System.Collections.Generic;
using UnityEngine;

public class CustomerSeat : MonoBehaviour
{
    static readonly List<CustomerSeat> seats = new List<CustomerSeat>();

    public float seatHeight = 0.75f;
    public bool taken;

    void OnEnable() { seats.Add(this); }
    void OnDisable() { seats.Remove(this); }

    public Vector3 SeatPosition => transform.position + Vector3.up * seatHeight;

    public static CustomerSeat FindFree()
    {
        List<CustomerSeat> free = seats.FindAll(s => !s.taken);
        return free.Count > 0 ? free[Random.Range(0, free.Count)] : null;
    }
}
