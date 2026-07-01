using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;

[RequireComponent(typeof(Collider))]
public class InteractWhenCrossed : MonoBehaviour
{
    [System.Serializable]
    public class PlayerCrossedEvent : UnityEvent<GameObject> {}

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
    [SerializeField] private PlayerCrossedEvent onCross;
    [SerializeField] private PlayerCrossedEvent onExit;
    [SerializeField] private PlayerCrossedEvent onBothCross;
    [SerializeField] private PlayerCrossedEvent onBothExit;

    private bool hasTriggered = false;
    private bool isActivated = false;
    private bool isOnePlayerActive = false;
    private bool isBothPlayersActive = false;
    private bool hasTriggeredOnePlayer = false;
    private bool hasTriggeredBothPlayers = false;
    private List<Transform> playersInside = new List<Transform>();
    private List<Transform> physicalPlayersInside = new List<Transform>();

    public bool IsActivated => isActivated;
    public int NumOfPlayersCrossed => playersInside.Count;
    public List<Transform> PlayersInside => playersInside;

    private float cleanTimer = 0f;
    private const float CLEAN_INTERVAL = 0.1f;

    private void Start()
    {
        // Ensure the collider is set as a trigger
        Collider col = GetComponent<Collider>();
        if (col != null)
        {
            col.isTrigger = true;
        }
    }

    private void Update()
    {
        if (playersInside.Count > 0)
        {
            cleanTimer += Time.deltaTime;
            if (cleanTimer >= CLEAN_INTERVAL)
            {
                cleanTimer = 0f;
                int initialCount = playersInside.Count;
                GameObject cleaned = CleanPlayersInsideList();
                
                if (playersInside.Count != initialCount)
                {
                    CheckDeactivation(cleaned);
                    if (multiplePeopleRequired && secondTrigger != null)
                    {
                        secondTrigger.CheckDeactivation(cleaned);
                    }
                }
            }
        }
        else
        {
            cleanTimer = 0f;
        }
    }

    private void OnTriggerEnter(Collider other)
    {
        FirstPersonController fpc = other.GetComponentInParent<FirstPersonController>();
        if (fpc != null)
        {
            Transform playerTransform = fpc.transform;
            
            // Clean list first to ensure accurate count
            CleanPlayersInsideList();

            if (!physicalPlayersInside.Contains(playerTransform))
            {
                physicalPlayersInside.Add(playerTransform);
            }

            if (!playersInside.Contains(playerTransform))
            {
                playersInside.Add(playerTransform);
                Debug.Log($"[InteractWhenCrossed] Player {playerTransform.name} entered trigger {gameObject.name}. Total players inside: {playersInside.Count}");
            }

            if (multiplePeopleRequired && secondTrigger != null)
            {
                if (!secondTrigger.playersInside.Contains(playerTransform))
                {
                    secondTrigger.playersInside.Add(playerTransform);
                }
            }

            CheckActivation(playerTransform.gameObject);
            if (multiplePeopleRequired && secondTrigger != null)
            {
                secondTrigger.CheckActivation(playerTransform.gameObject);
            }
        }
    }

    private void OnTriggerExit(Collider other)
    {
        FirstPersonController fpc = other.GetComponentInParent<FirstPersonController>();
        if (fpc != null)
        {
            Transform playerTransform = fpc.transform;

            if (physicalPlayersInside.Contains(playerTransform))
            {
                physicalPlayersInside.Remove(playerTransform);
            }

            if (playersInside.Contains(playerTransform))
            {
                playersInside.Remove(playerTransform);
                Debug.Log($"[InteractWhenCrossed] Player {playerTransform.name} exited trigger {gameObject.name}. Total players inside: {playersInside.Count}");
            }

            if (multiplePeopleRequired && secondTrigger != null)
            {
                if (secondTrigger.playersInside.Contains(playerTransform))
                {
                    secondTrigger.playersInside.Remove(playerTransform);
                }
            }

            CheckDeactivation(playerTransform.gameObject);
            if (multiplePeopleRequired && secondTrigger != null)
            {
                secondTrigger.CheckDeactivation(playerTransform.gameObject);
            }
        }
    }

    private GameObject CleanPlayersInsideList()
    {
        GameObject cleanedPlayer = null;
        for (int i = playersInside.Count - 1; i >= 0; i--)
        {
            Transform player = playersInside[i];
            if (player == null || !player.gameObject.activeInHierarchy || !IsPlayerPhysicallyInside(player))
            {
                Debug.Log($"[InteractWhenCrossed] Cleaning player {player?.name} from trigger {gameObject.name} (Null/Inactive/Outside).");
                if (player != null)
                {
                    cleanedPlayer = player.gameObject;
                }
                playersInside.RemoveAt(i);
            }
        }

        for (int i = physicalPlayersInside.Count - 1; i >= 0; i--)
        {
            Transform player = physicalPlayersInside[i];
            if (player == null || !player.gameObject.activeInHierarchy || !IsPlayerPhysicallyInside(player))
            {
                if (player != null && cleanedPlayer == null)
                {
                    cleanedPlayer = player.gameObject;
                }
                physicalPlayersInside.RemoveAt(i);
            }
        }

        return cleanedPlayer;
    }

    private bool IsPlayerPhysicallyInside(Transform playerTransform)
    {
        if (playerTransform == null) return false;
        
        Collider triggerCollider = GetComponent<Collider>();
        if (triggerCollider == null) return false;

        Vector3 closestPoint = triggerCollider.ClosestPoint(playerTransform.position);
        float distance = Vector3.Distance(closestPoint, playerTransform.position);
        
        // 3.0f tolerance to allow jumping / physics lag but filter out teleports/respawns
        return distance < 3.0f;
    }

    private void CheckActivation(GameObject player)
    {
        // 1 Player Crossed (based on physical presence)
        if (physicalPlayersInside.Count >= 1)
        {
            if (!isOnePlayerActive)
            {
                if (!triggerOnce || !hasTriggeredOnePlayer)
                {
                    isOnePlayerActive = true;
                    hasTriggeredOnePlayer = true;
                    Debug.Log($"[InteractWhenCrossed] 1 Player Crossed: {gameObject.name} by {player?.name}");
                    onCross?.Invoke(player);
                }
            }
        }

        // Both Players Crossed (based on synced presence)
        if (playersInside.Count >= 2)
        {
            if (!isBothPlayersActive)
            {
                if (!triggerOnce || !hasTriggeredBothPlayers)
                {
                    isBothPlayersActive = true;
                    hasTriggeredBothPlayers = true;
                    Debug.Log($"[InteractWhenCrossed] Both Players Crossed: {gameObject.name} by {player?.name}");
                    onBothCross?.Invoke(player);
                }
            }
        }

        // Update legacy activation flag
        if (playersInside.Count >= requiredPlayers)
        {
            isActivated = true;
            hasTriggered = true;
        }
    }

    private void CheckDeactivation(GameObject player)
    {
        // Both Players Exited (goes from 2 to < 2, based on synced presence)
        if (isBothPlayersActive && playersInside.Count < 2)
        {
            isBothPlayersActive = false;
            Debug.Log($"[InteractWhenCrossed] Both Players Exited: {gameObject.name} by {player?.name}");
            onBothExit?.Invoke(player);
        }

        // 1 Player Exited (goes from 1 to 0, based on physical presence)
        if (isOnePlayerActive && physicalPlayersInside.Count < 1)
        {
            isOnePlayerActive = false;
            Debug.Log($"[InteractWhenCrossed] 1 Player Exited: {gameObject.name} by {player?.name}");
            onExit?.Invoke(player);
        }

        // Update legacy activation flag
        if (playersInside.Count < requiredPlayers)
        {
            isActivated = false;
        }
    }

    public void Activate(GameObject player)
    {
        isActivated = true;
        hasTriggered = true;

        if (!isOnePlayerActive)
        {
            isOnePlayerActive = true;
            hasTriggeredOnePlayer = true;
            onCross?.Invoke(player);
        }

        if (playersInside.Count >= 2 && !isBothPlayersActive)
        {
            isBothPlayersActive = true;
            hasTriggeredBothPlayers = true;
            onBothCross?.Invoke(player);
        }
    }

    public void Deactivate(GameObject player)
    {
        isActivated = false;

        if (isBothPlayersActive)
        {
            isBothPlayersActive = false;
            onBothExit?.Invoke(player);
        }

        if (isOnePlayerActive)
        {
            isOnePlayerActive = false;
            onExit?.Invoke(player);
        }
    }

    public void ResetTrigger()
    {
        hasTriggered = false;
        isActivated = false;
        isOnePlayerActive = false;
        isBothPlayersActive = false;
        hasTriggeredOnePlayer = false;
        hasTriggeredBothPlayers = false;
        playersInside.Clear();
        physicalPlayersInside.Clear();
    }
}
