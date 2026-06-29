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
    
    [Header("Events")]
    [SerializeField] private UnityEvent onCross;

    private bool hasTriggered = false;
    private int numOfPlayersCrossed = 0;
    private HashSet<Transform> playersInside = new HashSet<Transform>();

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

                if (numOfPlayersCrossed >= requiredPlayers)
                {
                    hasTriggered = true;
                    onCross?.Invoke();
                }
            }
        }
    }

    private void OnTriggerExit(Collider other){
        if (other.CompareTag("Player") || other.GetComponentInParent<FirstPersonController>() != null)
        {
            Transform playerRoot = other.transform.root;
            if (playersInside.Contains(playerRoot))
            {
                playersInside.Remove(playerRoot);
                numOfPlayersCrossed = playersInside.Count;
            }
        }
    }

    public void ResetTrigger()
    {
        hasTriggered = false;
        playersInside.Clear();
        numOfPlayersCrossed = 0;
    }
}
