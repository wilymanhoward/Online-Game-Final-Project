using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;

[RequireComponent(typeof(Collider))]
public class InteractWhenCrossed : MonoBehaviour
{
    [Header("Trigger Settings")]
    [SerializeField] private bool triggerOnce = false;
    [Range(1, 2)][SerializeField] private int requiredPlayers = 2;
    [SerializeField] private bool multiplePeopleRequired = false;
    public bool MultiplePeopleRequired
    {
        get => multiplePeopleRequired;
        set => multiplePeopleRequired = value;
    }

    [Header("Trigger References")]
    [SerializeField] private InteractWhenCrossed secondTrigger;

    [Header("Events")]
    [SerializeField] private UnityEvent onCross;
    [SerializeField] private UnityEvent onExit;
    [SerializeField] private UnityEvent onBothCrossed;
    [SerializeField] private UnityEvent onBothExit;

    private bool hasTriggered = false;
    private bool isActivated = false;
    private bool bothCrossedTriggered = false;
    private int numOfPlayersCrossed = 0;
    private HashSet<Transform> playersInside = new HashSet<Transform>();

    public bool IsActivated => isActivated;
    public int NumOfPlayersCrossed => numOfPlayersCrossed;

    private void Start()
    {
        // Ensure the collider is set as a trigger
        Collider col = GetComponent<Collider>();
        if (col != null)
        {
            col.isTrigger = true;
        }
    }

    private void OnTriggerEnter(Collider other)
    {
        if (triggerOnce && hasTriggered) return;

        if (other.CompareTag("Player") || other.GetComponentInParent<FirstPersonController>() != null)
        {
            Transform playerRoot = other.transform.root;
            if (!playersInside.Contains(playerRoot))
            {
                playersInside.Add(playerRoot);
                numOfPlayersCrossed = playersInside.Count;

                CheckActivation();
            }
        }
    }

    private void OnTriggerExit(Collider other)
    {
        if (other.CompareTag("Player") || other.GetComponentInParent<FirstPersonController>() != null)
        {
            Transform playerRoot = other.transform.root;
            if (playersInside.Contains(playerRoot))
            {
                playersInside.Remove(playerRoot);
                numOfPlayersCrossed = playersInside.Count;

                CheckDeactivation();
            }
        }
    }

    private void CheckActivation()
    {
        if (isActivated) return;
        if (triggerOnce && hasTriggered) return;

        if (numOfPlayersCrossed >= requiredPlayers)
        {
            Activate();
        }

        if (isActivated && multiplePeopleRequired && secondTrigger != null)
        {
            if (secondTrigger.isActivated && !bothCrossedTriggered)
            {
                bothCrossedTriggered = true;
                secondTrigger.bothCrossedTriggered = true;

                onBothCrossed?.Invoke();
                secondTrigger.onBothCrossed?.Invoke();
            }
        }
    }

    private void CheckDeactivation()
    {
        if (!isActivated) return;

        if (numOfPlayersCrossed < requiredPlayers)
        {
            Deactivate();
        }

        if (multiplePeopleRequired)
        {
            if ((!isActivated || secondTrigger == null || !secondTrigger.isActivated) && bothCrossedTriggered)
            {
                bothCrossedTriggered = false;
                if (secondTrigger != null)
                {
                    secondTrigger.bothCrossedTriggered = false;
                }

                onBothExit?.Invoke();
                secondTrigger?.onBothExit?.Invoke();
            }
        }
    }

    public void Activate()
    {
        isActivated = true;
        hasTriggered = true;
        onCross?.Invoke();
    }

    public void Deactivate()
    {
        isActivated = false;
        onExit?.Invoke();
    }

    public void ResetTrigger()
    {
        hasTriggered = false;
        isActivated = false;
        bothCrossedTriggered = false;
        playersInside.Clear();
        numOfPlayersCrossed = 0;
    }
}
