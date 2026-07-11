using System.Collections.Generic;
using UnityEngine;
using Photon.Pun;

public class RedLightShooter : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private Transform shootOrigin;
    [SerializeField] private BoxCollider shootZone; // Only shoot players inside this zone
    [SerializeField] private GameObject projectilePrefab;
    [SerializeField] private string networkProjectilePrefabName = "ThrowableRock";

    [Header("Shooting Settings")]
    [SerializeField] private bool useShootZone = true;
    [SerializeField] private float projectileSpeed = 25f; // Fast speed
    [SerializeField] private float shotsPerSecondPerPlayer = 2f; // 2 shots per second per player (total 4/sec for 2 players)

    private Dictionary<FirstPersonController, float> playerCooldowns = new Dictionary<FirstPersonController, float>();
    private FirstPersonController[] cachedPlayers;
    private float playerCacheTimer = 0f;

    private void OnEnable()
    {
        // Reset all cooldowns so shooting starts immediately
        playerCooldowns.Clear();
        cachedPlayers = null;
        playerCacheTimer = 0f;
    }

    private void Update()
    {
        // Handle network authority: only Master Client shoots projectiles (avoids duplicate objects over network)
        if (PhotonNetwork.IsConnected && !PhotonNetwork.IsMasterClient) return;

        // Tick down cooldowns for all players
        List<FirstPersonController> activeKeys = new List<FirstPersonController>(playerCooldowns.Keys);
        foreach (var playerKey in activeKeys)
        {
            if (playerKey != null)
            {
                playerCooldowns[playerKey] -= Time.deltaTime;
            }
        }

        // Periodic player caching
        playerCacheTimer += Time.deltaTime;
        if (cachedPlayers == null || playerCacheTimer >= 0.5f)
        {
            cachedPlayers = FindObjectsOfType<FirstPersonController>();
            playerCacheTimer = 0f;
        }

        foreach (var player in cachedPlayers)
        {
            if (player == null || player.IsDead) continue;

            // Only shoot players inside the designated shoot zone
            if (useShootZone && shootZone != null)
            {
                if (!shootZone.bounds.Contains(player.transform.position))
                {
                    if (Time.frameCount % 120 == 0) // Log once every 2 seconds to avoid spam
                    {
                        Debug.Log($"[RedLightShooter] Skipping {player.name} on {gameObject.name}: Player is outside the shootZone bounds.");
                    }
                    continue;
                }
            }

            // Initialize player cooldown if not tracked
            if (!playerCooldowns.ContainsKey(player))
            {
                playerCooldowns[player] = 0f;
            }

            // Shoot if the player's cooldown is expired
            if (playerCooldowns[player] <= 0f)
            {
                Vector3 startPos = shootOrigin != null ? shootOrigin.position : transform.position;
                Vector3 targetPos = player.transform.position + Vector3.up * 1f; // Target chest/center
                Vector3 direction = (targetPos - startPos).normalized;

                // Perform a single raycast to get the direction/trajectory towards the player, ignoring the shooter itself
                RaycastHit[] hits = Physics.RaycastAll(startPos, direction, 100f);
                System.Array.Sort(hits, (a, b) => a.distance.CompareTo(b.distance));
                foreach (var h in hits)
                {
                    if (h.transform.IsChildOf(transform.root)) continue;
                    direction = (h.point - startPos).normalized;
                    break;
                }

                // Fire the projectile along the calculated path
                ShootAtPlayer(player, startPos, direction);

                // Set cooldown for this player
                playerCooldowns[player] = 1f / shotsPerSecondPerPlayer;
            }
        }
    }

    private void ShootAtPlayer(FirstPersonController player, Vector3 spawnPos, Vector3 direction)
    {
        Debug.Log($"[RedLightShooter] ShootAtPlayer called on {gameObject.name} for player: {player.name}!");

        PhotonView pv = GetComponent<PhotonView>();
        if (PhotonNetwork.IsConnected && PhotonNetwork.InRoom)
        {
            if (pv != null && pv.ViewID > 0)
            {
                Debug.Log($"[RedLightShooter] Sending RPC SpawnProjectileRPC from {gameObject.name}");
                pv.RPC("SpawnProjectileRPC", RpcTarget.All, spawnPos, direction);
            }
            else
            {
                Debug.Log($"[RedLightShooter] No PhotonView, routing through local player: {player.name}");
                // Route through local player controller
                FirstPersonController localPlayer = null;
                var controllers = FindObjectsOfType<FirstPersonController>();
                foreach (var c in controllers)
                {
                    if (c.IsLocalPlayer)
                    {
                        localPlayer = c;
                        break;
                    }
                }
                if (localPlayer != null)
                {
                    localPlayer.RouteRedLightShoot(GetGameObjectPath(gameObject), spawnPos, direction);
                }
            }
        }
        else
        {
            Debug.Log($"[RedLightShooter] Offline Mode, spawning locally from {gameObject.name}");
            SpawnProjectileLocal(spawnPos, direction);
        }
    }

    private string GetGameObjectPath(GameObject obj)
    {
        string path = "/" + obj.name;
        while (obj.transform.parent != null)
        {
            obj = obj.transform.parent.gameObject;
            path = "/" + obj.name + path;
        }
        return path;
    }

    [PunRPC]
    private void SpawnProjectileRPC(Vector3 spawnPos, Vector3 direction)
    {
        SpawnProjectileLocal(spawnPos, direction);
    }

    public void SpawnProjectileLocal(Vector3 spawnPos, Vector3 direction)
    {
        Debug.Log($"[RedLightShooter] SpawnProjectileLocal called at {spawnPos} (direction: {direction}) on {gameObject.name}");
        if (projectilePrefab != null)
        {
            Quaternion rotation = Quaternion.LookRotation(direction);
            GameObject proj = Instantiate(projectilePrefab, spawnPos, rotation);
            Debug.Log($"[RedLightShooter] Projectile instantiated successfully: {proj.name}");

            // Ignore collisions between the projectile and the shooter (Pharaoh + staff)
            Collider[] shooterColliders = transform.root.GetComponentsInChildren<Collider>();
            Collider[] projectileColliders = proj.GetComponentsInChildren<Collider>();
            foreach (var sCol in shooterColliders)
            {
                foreach (var pCol in projectileColliders)
                {
                    if (sCol != null && pCol != null)
                    {
                        Physics.IgnoreCollision(sCol, pCol, true);
                    }
                }
            }

            Rigidbody rb = proj.GetComponent<Rigidbody>();
            if (rb != null)
            {
                rb.isKinematic = true; // Kinematic Rigidbody
            }
        }
    }
}
