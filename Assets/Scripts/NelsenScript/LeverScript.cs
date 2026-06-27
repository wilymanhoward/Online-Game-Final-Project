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
        transform.rotation = Quaternion.Euler(ClosedPosition);    
    }

    public void ActivateLever()
    {
        LeverState = true;
        transform.rotation = Quaternion.Euler(OpenPosition);
    }

    public void DeactivateLever()
    {
        LeverState = false;
        transform.rotation = Quaternion.Euler(ClosedPosition);
    }
}
