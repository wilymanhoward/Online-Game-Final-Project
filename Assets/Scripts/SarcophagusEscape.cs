using UnityEngine;
using Photon.Pun;

public class SarcophagusEscape : MonoBehaviourPun
{
    [Header("Sarcophagus Elements")]
    public GameObject sarcophagusLid;
    public GameObject player1;
    public int requiredClicks = 10;
    
    [Header("Lid Physics Settings")]
    public float pushForce = 8f;
    public Vector3 pushDirection = new Vector3(0f, 1f, 3f); // Push upward and forward

    private int clickCount = 0;
    private FirstPersonController fpc;
    private bool isEscaped = false;
    private Vector3 initialLidLocalPos;

    private void Start()
    {
        if (player1 == null)
        {
            player1 = GameObject.Find("Player1");
        }

        if (sarcophagusLid == null)
        {
            // Try to find the user's custom sarcophagus and lid in the hierarchy first
            GameObject customSarc = GameObject.Find("Player 1 Sarcophagus");
            if (customSarc != null)
            {
                Transform lidTrans = customSarc.transform.Find("Lid");
                if (lidTrans != null)
                {
                    sarcophagusLid = lidTrans.gameObject;
                }
            }

            // Fallback: search closest matching lid
            if (sarcophagusLid == null)
            {
                float closestDist = float.MaxValue;
                GameObject closestLid = null;
                Vector3 targetRefPos = player1 != null ? player1.transform.position : new Vector3(-1.20f, 0.01f, -2.07f);

                GameObject[] allObjects = GameObject.FindObjectsOfType<GameObject>();
                foreach (GameObject go in allObjects)
                {
                    if (go.name == "Lid" || go.name == "SM_Prop_Sarcophagus_01_Lid_01")
                    {
                        float dist = Vector3.Distance(go.transform.position, targetRefPos);
                        if (dist < closestDist)
                        {
                            closestDist = dist;
                            closestLid = go;
                        }
                    }
                }
                sarcophagusLid = closestLid;
            }
        }

        if (sarcophagusLid != null)
        {
            initialLidLocalPos = sarcophagusLid.transform.localPosition;
        }

        // Only lock controls and position for the local Player 1 (Master Client)
        if (PhotonNetwork.IsConnected && !PhotonNetwork.IsMasterClient)
        {
            return;
        }

        if (player1 != null)
        {
            fpc = player1.GetComponent<FirstPersonController>();
            if (fpc != null)
            {
                // Lock player in sarcophagus
                fpc.isParalyzed = true;
                
                // Position player inside the sarcophagus base
                player1.transform.position = new Vector3(-1.24f, 0.4f, -2.10f);
            }
        }
    }

    private void Update()
    {
        if (isEscaped) return;

        // Only Player 1 (Master Client / host) handles the escape clicks
        if (PhotonNetwork.IsConnected && !PhotonNetwork.IsMasterClient) return;

        if (Input.GetMouseButtonDown(0))
        {
            clickCount++;
            Debug.Log($"[SarcophagusEscape] Click {clickCount}/{requiredClicks} registered.");
            
            // Shake lid and camera for push feedback
            if (sarcophagusLid != null)
            {
                StartCoroutine(ShakeLid());
            }
            if (fpc != null)
            {
                fpc.TriggerCameraShake(0.15f, 0.05f);
            }

            if (clickCount >= requiredClicks)
            {
                EscapeSarcophagus();
            }
        }
    }

    private System.Collections.IEnumerator ShakeLid()
    {
        float elapsed = 0f;
        float duration = 0.1f;
        Vector3 shakeOffset = new Vector3(Random.Range(-0.02f, 0.02f), Random.Range(0.01f, 0.03f), 0f);
        
        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            sarcophagusLid.transform.localPosition = initialLidLocalPos + shakeOffset;
            yield return null;
        }
        sarcophagusLid.transform.localPosition = initialLidLocalPos;
    }

    private void EscapeSarcophagus()
    {
        isEscaped = true;
        Debug.Log("[SarcophagusEscape] Escape triggered! Releasing player.");
        
        if (fpc != null)
        {
            fpc.isParalyzed = false;
        }

        // Push the lid off over the network on all clients
        if (PhotonNetwork.IsConnected && PhotonNetwork.InRoom)
        {
            photonView.RPC("PushLidOffRPC", RpcTarget.AllBuffered);
        }
        else
        {
            Debug.Log("[SarcophagusEscape] Offline/Not in room. Playing animation locally.");
            PushLidOffRPC();
        }
    }

    [PunRPC]
    private void PushLidOffRPC()
    {
        Debug.Log("[SarcophagusEscape] PushLidOffRPC called.");
        if (sarcophagusLid == null)
        {
            Debug.LogError("[SarcophagusEscape] Cannot animate: sarcophagusLid is NULL!");
            return;
        }

        // Run smooth procedural rotation and slide
        StartCoroutine(RotateLidCoroutine());
    }

    private System.Collections.IEnumerator RotateLidCoroutine()
    {
        Debug.Log("[SarcophagusEscape] RotateLidCoroutine started.");
        float duration = 1.0f;
        float elapsed = 0f;
        
        // Deparent so world space coordinates work correctly
        sarcophagusLid.transform.SetParent(null);

        // Ensure any Rigidbody doesn't fight the coroutine animation
        Rigidbody rb = sarcophagusLid.GetComponent<Rigidbody>();
        if (rb != null)
        {
            rb.isKinematic = true;
        }
        
        Vector3 startPos = sarcophagusLid.transform.position;
        Quaternion startRot = sarcophagusLid.transform.rotation;
        
        // Target: Slide forward and drop down relative to player orientation
        Vector3 pushOffset = player1 != null ? (player1.transform.forward * 1.6f - player1.transform.up * 1.1f) : new Vector3(0f, -1.1f, 1.6f);
        Vector3 targetPos = startPos + pushOffset;
        
        // Target rotation: Fall flat on its face (pitch rotation relative to player)
        Quaternion targetRot = startRot;
        if (player1 != null)
        {
            // Rotate 90 degrees around the player's right axis (pitch forward)
            targetRot = Quaternion.AngleAxis(90f, player1.transform.right) * startRot;
        }
        else
        {
            targetRot = Quaternion.Euler(90f, startRot.eulerAngles.y, startRot.eulerAngles.z);
        }

        // Disable collider temporarily to prevent any clipping/jitter with sarcophagus base
        var col = sarcophagusLid.GetComponent<Collider>();
        if (col != null) col.enabled = false;

        // Perform smooth lerp with satisfying bounce at the end
        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / duration);
            float progress = EaseOutBounce(t);

            sarcophagusLid.transform.position = Vector3.Lerp(startPos, targetPos, progress);
            sarcophagusLid.transform.rotation = Quaternion.Slerp(startRot, targetRot, progress);
            yield return null;
        }

        sarcophagusLid.transform.position = targetPos;
        sarcophagusLid.transform.rotation = targetRot;

        // Re-enable collider so player can step on it on the ground
        if (col != null) col.enabled = true;
    }

    private float EaseOutBounce(float x)
    {
        float n1 = 7.5625f;
        float d1 = 2.75f;

        if (x < 1f / d1)
        {
            return n1 * x * x;
        }
        else if (x < 2f / d1)
        {
            return n1 * (x -= 1.5f / d1) * x + 0.75f;
        }
        else if (x < 2.5f / d1)
        {
            return n1 * (x -= 2.25f / d1) * x + 0.9375f;
        }
        else
        {
            return n1 * (x -= 2.625f / d1) * x + 0.984375f;
        }
    }
}
