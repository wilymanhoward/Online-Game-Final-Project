using UnityEngine;
using Photon.Pun;

public class NetworkSpawnManager : MonoBehaviourPunCallbacks
{
    [Header("Prefab Settings")]
    public string playerPrefabName = "SM_Chr_Mummy_03";

    [Header("Spawn Settings")]
    public Vector3 spawnAreaMin = new Vector3(-2f, 1f, -2f);
    public Vector3 spawnAreaMax = new Vector3(2f, 1f, 2f);

    void Start()
    {
        if (PhotonNetwork.IsConnectedAndReady)
        {
            SpawnPlayer();
        }
        else
        {
            Debug.Log("Not connected to Photon. Enabling offline mode for direct scene testing...");
            PhotonNetwork.OfflineMode = true;
            PhotonNetwork.CreateRoom("OfflineTestingRoom");
        }
    }

    public override void OnJoinedRoom()
    {
        SpawnPlayer();
    }

    private void SpawnPlayer()
    {
        // Check if we already have a player spawned for this client to prevent double spawns
        var views = FindObjectsOfType<PhotonView>();
        foreach (var view in views)
        {
            if (view.IsMine && view.gameObject.name.Contains(playerPrefabName))
            {
                Debug.Log("Player already spawned!");
                return;
            }
        }

        Vector3 randomPos = new Vector3(
            Random.Range(spawnAreaMin.x, spawnAreaMax.x),
            Random.Range(spawnAreaMin.y, spawnAreaMax.y),
            Random.Range(spawnAreaMin.z, spawnAreaMax.z)
        );

        Debug.Log("Spawning networked player: " + playerPrefabName + " at " + randomPos);
        PhotonNetwork.Instantiate(playerPrefabName, randomPos, Quaternion.identity);
    }
}
