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
            if (shootZone != null && !shootZone.bounds.Contains(player.transform.position)) continue;

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

                // Perform a single raycast to get the direction/trajectory towards the player
                RaycastHit hit;
                if (Physics.Raycast(startPos, direction, out hit, 100f))
                {
                    direction = (hit.point - startPos).normalized;
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
        Debug.Log($"[RedLightShooter] Shooting projectile at {player.name} along raycast path!");
        Quaternion rotation = Quaternion.LookRotation(direction);

        if (PhotonNetwork.IsConnected)
        {
            GameObject proj = PhotonNetwork.Instantiate(networkProjectilePrefabName, spawnPos, rotation);
            Rigidbody rb = proj.GetComponent<Rigidbody>();
            if (rb != null)
            {
                rb.isKinematic = true; // Kinematic Rigidbody
            }
            return;
        }

        // Offline / local fallback
        if (projectilePrefab != null)
        {
            GameObject proj = Instantiate(projectilePrefab, spawnPos, rotation);
            Rigidbody rb = proj.GetComponent<Rigidbody>();
            if (rb != null)
            {
                rb.isKinematic = true; // Kinematic Rigidbody
            }
        }
    }
}
