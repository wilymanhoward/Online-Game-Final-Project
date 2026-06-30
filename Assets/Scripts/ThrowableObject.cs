using UnityEngine;
using Photon.Pun;

[RequireComponent(typeof(Rigidbody))]
public class ThrowableObject : MonoBehaviourPun
{
    [Header("Settings")]
    public float lifetime = 5f;
    public float bounceForce = 0.5f;

    private Rigidbody rb;
    private bool isDestroyed = false;

    void Awake()
    {
        rb = GetComponent<Rigidbody>();
    }

    void Start()
    {
        // Only the master client or spawning client should control the lifetime and network destruction
        if (PhotonNetwork.IsConnected)
        {
            if (photonView.IsMine)
            {
                Destroy(gameObject, lifetime);
            }
        }
        else
        {
            Destroy(gameObject, lifetime);
        }
    }

    public void InitializeVelocity(Vector3 velocity)
    {
        if (rb == null) rb = GetComponent<Rigidbody>();
        rb.velocity = velocity;
    }

    void OnCollisionEnter(Collision collision)
    {
        // Prevent double destruction
        if (isDestroyed) return;

        // Ignore collisions with the thrower/other players
        if (collision.gameObject.CompareTag("Player") || collision.gameObject.GetComponentInParent<FirstPersonController>() != null)
        {
            return;
        }

        // Check if hit the giant Pharaoh
        GiantPharaohAI giant = collision.gameObject.GetComponentInParent<GiantPharaohAI>();
        if (giant != null)
        {
            isDestroyed = true;

            if (PhotonNetwork.IsConnected)
            {
                if (photonView.IsMine)
                {
                    photonView.RPC("SyncPharaohKnockdown", RpcTarget.All);
                    PhotonNetwork.Destroy(gameObject);
                }
            }
            else
            {
                giant.Knockdown();
                Destroy(gameObject);
            }
            return;
        }
    }

    [PunRPC]
    public void SyncPharaohKnockdown()
    {
        GiantPharaohAI giant = FindObjectOfType<GiantPharaohAI>();
        if (giant != null)
        {
            giant.Knockdown();
        }
    }
}
