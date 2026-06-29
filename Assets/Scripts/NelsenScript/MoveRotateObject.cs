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

    [Header("Open Delay Settings")]
    [SerializeField] private bool openWithDelay = false;
    public bool OpenWithDelay
    {
        get => openWithDelay;
        set => openWithDelay = value;
    }
    [SerializeField] private float openDelay = 1f;

    private Coroutine openCoroutine;

    [Header("Close Over Time Settings")]
    [SerializeField] private bool closeOverTime = false;
    public bool CloseOverTime
    {
        get => closeOverTime;
        set => closeOverTime = value;
    }
    [SerializeField] private float closeDelay = 3f;

    private Coroutine closeCoroutine;

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
        if (openCoroutine != null)
        {
            StopCoroutine(openCoroutine);
            openCoroutine = null;
        }

        if (openWithDelay)
        {
            openCoroutine = StartCoroutine(OpenAfterDelay(openDelay, openSpeed));
        }
        else
        {
            ExecuteOpen(openSpeed);
        }
    }

    private void ExecuteOpen(float openSpeed)
    {
        currentOpenSpeed = openSpeed == 0f ? baseOpenSpeed : openSpeed;
        Debug.Log($"{name} Activated");
        isActive = true;
        targetPosition = openPosition;
        targetRotation = Quaternion.Euler(openRotation);

        if (closeOverTime)
        {
            if (closeCoroutine != null)
            {
                StopCoroutine(closeCoroutine);
            }
            closeCoroutine = StartCoroutine(CloseAfterDelay(closeDelay));
        }
    }

    public void Deactivate(float closeSpeed)
    {
        if (openCoroutine != null)
        {
            StopCoroutine(openCoroutine);
            openCoroutine = null;
        }

        currentCloseSpeed = closeSpeed == 0f ? baseCloseSpeed : closeSpeed;
        Debug.Log($"{name} Deactivated");
        isActive = false;
        targetPosition = closedPosition;
        targetRotation = Quaternion.Euler(closedRotation);

        if (closeCoroutine != null)
        {
            StopCoroutine(closeCoroutine);
            closeCoroutine = null;
        }
    }

    private IEnumerator OpenAfterDelay(float delay, float openSpeed)
    {
        yield return new WaitForSeconds(delay);
        ExecuteOpen(openSpeed);
    }

    private IEnumerator CloseAfterDelay(float delay)
    {
        yield return new WaitForSeconds(delay);
        Deactivate();
    }
}
