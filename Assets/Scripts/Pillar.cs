using UnityEngine;
using UnityEngine.Events;
using Photon.Pun;

public class Pillar : MonoBehaviourPun
{
    [Header("Settings")]
    [Tooltip("The tag required to trigger the event. Default is 'Hitwall'.")]
    [SerializeField] private string targetTag = "Hitwall";

    [Tooltip("Initial state of the pillar.")]
    [SerializeField] private bool startRepaired = true;

    [Header("Events")]
    [SerializeField] private UnityEvent onBreak;
    [SerializeField] private UnityEvent onRepair;

    private bool isBroken = false;

    public bool IsBroken => isBroken;

    private void Start()
    {
        isBroken = !startRepaired;
    }

    private void OnTriggerEnter(Collider other)
    {
        CheckAndTrigger(other.gameObject);
    }

    private void OnCollisionEnter(Collision collision)
    {
        CheckAndTrigger(collision.gameObject);
    }

    private void CheckAndTrigger(GameObject obj)
    {
        if (isBroken) return;

        // Check if the object has the target tag, supporting common casing variations (Hitwall vs HitWall)
        if (obj.CompareTag(targetTag) || 
            (targetTag.Equals("Hitwall", System.StringComparison.OrdinalIgnoreCase) && 
             (obj.CompareTag("Hitwall") || obj.CompareTag("HitWall"))))
        {
            BreakPillar();
        }
    }

    /// <summary>
    /// Breaks the pillar. Can be called locally or synced via RPC if PhotonView is attached.
    /// </summary>
    public void BreakPillar()
    {
        if (isBroken) return;

        PhotonView pv = GetComponent<PhotonView>();
        if (PhotonNetwork.IsConnected && PhotonNetwork.InRoom && pv != null && pv.ViewID > 0)
        {
            pv.RPC("SyncBreakRPC", RpcTarget.All);
        }
        else
        {
            BreakPillarLocal();
        }
    }

    /// <summary>
    /// Repairs the pillar. Intended to be called by an external script.
    /// </summary>
    public void RepairPillar()
    {
        if (!isBroken) return;

        PhotonView pv = GetComponent<PhotonView>();
        if (PhotonNetwork.IsConnected && PhotonNetwork.InRoom && pv != null && pv.ViewID > 0)
        {
            pv.RPC("SyncRepairRPC", RpcTarget.All);
        }
        else
        {
            RepairPillarLocal();
        }
    }

    [PunRPC]
    private void SyncBreakRPC()
    {
        BreakPillarLocal();
    }

    [PunRPC]
    private void SyncRepairRPC()
    {
        RepairPillarLocal();
    }

    private void BreakPillarLocal()
    {
        if (isBroken) return;
        isBroken = true;
        Debug.Log($"[Pillar] Broken: {gameObject.name}");
        onBreak?.Invoke();
    }

    private void RepairPillarLocal()
    {
        if (!isBroken) return;
        isBroken = false;
        Debug.Log($"[Pillar] Repaired: {gameObject.name}");
        onRepair?.Invoke();
    }
}
