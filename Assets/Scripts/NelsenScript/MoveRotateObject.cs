using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class MoveRotateObject : MonoBehaviour
{
    [Header("Animation Settings")]
    [SerializeField] private bool animatePosition = false;
    [SerializeField] private bool animateRotation = false;

    [Header("Speed Settings")]
    [SerializeField] private float baseOpenSpeed = 2f;
    [SerializeField] private float baseCloseSpeed = 2f;

    private float currentOpenSpeed = 0f;
    private float currentCloseSpeed = 0f;

    [Header("Position Config")]
    [SerializeField] private Vector3 closedPosition;
    [SerializeField] private Vector3 openPosition;

    [Header("Rotation Config")]
    [SerializeField] private Vector3 closedRotation;
    [SerializeField] private Vector3 openRotation;

    private bool isActive = false;
    private Vector3 targetPosition;
    private Quaternion targetRotation;

    void Start()
    {
        isActive = false;
        targetPosition = closedPosition;
        targetRotation = Quaternion.Euler(closedRotation);

        if (animatePosition)
        {
            transform.localPosition = targetPosition;
        }
        if (animateRotation)
        {
            transform.localRotation = targetRotation;
        }
    }

    void Update()
    {
        if (animatePosition)
        {
            float currentPosSpeed = isActive ? currentOpenSpeed : currentCloseSpeed;
            transform.localPosition = Vector3.Lerp(transform.localPosition, targetPosition, Time.deltaTime * currentPosSpeed);
        }
        if (animateRotation)
        {
            float currentRotSpeed = isActive ? currentOpenSpeed : currentCloseSpeed;
            transform.localRotation = Quaternion.Slerp(transform.localRotation, targetRotation, Time.deltaTime * currentRotSpeed);
        }
    }

    public void Activate()
    {
        Activate(0f);
    }

    public void Deactivate()
    {
        Deactivate(0f);
    }

    public void Toggle()
    {
        Toggle(0f);
    }

    public void Toggle(float speed)
    {
        if (isActive)
        {
            Deactivate(speed);
        }
        else
        {
            Activate(speed);
        }
    }

    public void Activate(float openSpeed)
    {
        currentOpenSpeed = openSpeed == 0f ? baseOpenSpeed : openSpeed;
        Debug.Log($"{name} Activated");
        isActive = true;
        targetPosition = openPosition;
        targetRotation = Quaternion.Euler(openRotation);
    }

    public void Deactivate(float closeSpeed)
    {
        currentCloseSpeed = closeSpeed == 0f ? baseCloseSpeed : closeSpeed;
        Debug.Log($"{name} Deactivated");
        isActive = false;
        targetPosition = closedPosition;
        targetRotation = Quaternion.Euler(closedRotation);
    }
}
