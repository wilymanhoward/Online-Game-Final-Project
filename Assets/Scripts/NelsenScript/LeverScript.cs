using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class DoorScript : MonoBehaviour
{
    [SerializeField] private Vector3 ClosedPosition;
    [SerializeField] private Vector3 OpenPosition;

    void Start()
    {
        transform.position = ClosedPosition;    
    }

    void Update()
    {
        
    }
}
