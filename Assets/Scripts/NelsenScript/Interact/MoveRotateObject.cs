using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;

public class MoveRotateObject : MonoBehaviour
{
    [Header("Animation Settings")]
    [SerializeField] private bool animatePosition = false;
    [SerializeField] private bool animateRotation = false;
    [SerializeField] private bool doNotDisturb = false;

    public bool IsMoving => progress > 0f && progress < 1f;

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
    private Quaternion closedRotationQuaternion;
    private Quaternion openRotationQuaternion;
    private float progress = 0f;

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

    [Header("Events")]
    public UnityEvent OnActivateObject = new UnityEvent();
    public UnityEvent OnDeactivateObject = new UnityEvent();
    public UnityEvent OnReachedClosed = new UnityEvent();
    public UnityEvent OnReachedOpen = new UnityEvent();

    void Start()
    {
        isActive = false;
        closedRotationQuaternion = Quaternion.Euler(closedRotation);
        openRotationQuaternion = Quaternion.Euler(openRotation);
        targetPosition = closedPosition;
        targetRotation = closedRotationQuaternion;
        progress = 0f;

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
        float speed = isActive ? currentOpenSpeed : currentCloseSpeed;
        float prevProgress = progress;

        if (isActive)
        {
            progress = Mathf.MoveTowards(progress, 1f, Time.deltaTime * speed);

            if (progress >= 1f && prevProgress < 1f)
            {
                Debug.Log($"[MoveRotateObject] {name} reached opening point (open position). Invoking OnReachedOpen.");
                OnReachedOpen?.Invoke();
            }
        }
        else
        {
            progress = Mathf.MoveTowards(progress, 0f, Time.deltaTime * speed);

            if (progress <= 0f && prevProgress > 0f)
            {
                Debug.Log($"[MoveRotateObject] {name} reached closing point (closed position). Invoking OnReachedClosed.");
                OnReachedClosed?.Invoke();
            }
        }

        if (animatePosition)
        {
            transform.localPosition = Vector3.Lerp(closedPosition, openPosition, progress);
        }
        if (animateRotation)
        {
            transform.localRotation = Quaternion.Slerp(closedRotationQuaternion, openRotationQuaternion, progress);
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
        Activate(openSpeed, false);
    }

    public void Activate(float openSpeed, bool force)
    {
        if (!force && doNotDisturb && IsMoving) return;

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
        Debug.Log($"[MoveRotateObject] {name} Activated (ExecuteOpen). Current progress: {progress}.");
        isActive = true;
        targetPosition = openPosition;
        targetRotation = openRotationQuaternion;

        OnActivateObject?.Invoke();

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
        DeactivateInternal(closeSpeed, false);
    }

    public void Deactivate(float closeSpeed, bool force)
    {
        DeactivateInternal(closeSpeed, force);
    }

    private void DeactivateInternal(float closeSpeed, bool force)
    {
        if (!force && doNotDisturb && IsMoving)
        {
            Debug.Log($"[MoveRotateObject] {name} Deactivate blocked by doNotDisturb (currently moving).");
            return;
        }

        Debug.Log($"[MoveRotateObject] {name} DeactivateInternal starting close. Current progress: {progress}, speed: {closeSpeed} (base: {baseCloseSpeed}), force: {force}.");

        if (openCoroutine != null)
        {
            StopCoroutine(openCoroutine);
            openCoroutine = null;
        }

        currentCloseSpeed = closeSpeed == 0f ? baseCloseSpeed : closeSpeed;
        Debug.Log($"{name} Deactivated");
        isActive = false;
        targetPosition = closedPosition;
        targetRotation = closedRotationQuaternion;

        OnDeactivateObject?.Invoke();

        if (closeCoroutine != null)
        {
            StopCoroutine(closeCoroutine);
            closeCoroutine = null;
        }
    }

    private IEnumerator OpenAfterDelay(float delay, float openSpeed)
    {
        yield return new WaitForSeconds(delay);
        openCoroutine = null;
        ExecuteOpen(openSpeed);
    }

    private IEnumerator CloseAfterDelay(float delay)
    {
        yield return new WaitForSeconds(delay);
        closeCoroutine = null;
        DeactivateInternal(0f, true);
    }
}
