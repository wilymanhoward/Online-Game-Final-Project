using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class DoorScript : MonoBehaviour
{
    private bool DoorState = false;

    [SerializeField] private Vector3 ClosedPosition;
    [SerializeField] private Vector3 OpenPosition;

    void Start()
    {
        transform.position = ClosedPosition;    
    }

    public void OpenDoor()
    {
        DoorState = true;
        transform.position = OpenPosition;
    }

    public void CloseDoor()
    {
        DoorState = false;
        transform.position = ClosedPosition;
    }
}
