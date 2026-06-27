using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class LeverScript : MonoBehaviour
{
    [SerializeField] private Vector3 ClosedPosition;
    [SerializeField] private Vector3 OpenPosition;

    private bool LeverState = false;

    void Start()
    {
        LeverState = false;
        transform.position = ClosedPosition;    
    }

    void ActivateLever()
    {
        LeverState = true;
        transform.position = OpenPosition;
    }

    void DeactivateLever()
    {
        LeverState = false;
        transform.position = ClosedPosition;
    }
}
