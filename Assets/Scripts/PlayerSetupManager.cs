using UnityEngine;
using Photon.Pun;
using Photon.Realtime;

public class PlayerSetupManager : MonoBehaviourPunCallbacks
{
    private void Start()
    {
        // Optimize Photon update rates for smooth multiplayer movement sync
        PhotonNetwork.SendRate = 30;
        PhotonNetwork.SerializationRate = 30;

        InitializeOwnership();
    }

    private void InitializeOwnership()
    {
        if (!PhotonNetwork.IsConnected)
        {
            Debug.Log("[PlayerSetupManager] Offline. No ownership transfer needed.");
            return;
        }

        GameObject p2 = GameObject.Find("Player2");
        if (p2 == null)
        {
            Debug.LogError("[PlayerSetupManager] Player2 not found in the scene hierarchy!");
            return;
        }

        PhotonView p2View = p2.GetComponent<PhotonView>();
        if (p2View == null)
        {
            Debug.LogError("[PlayerSetupManager] Player2 is missing a PhotonView component!");
            return;
        }

        if (PhotonNetwork.IsMasterClient)
        {
            // Master Client transfers ownership of Player 2 to the guest player if they are already in the room
            if (PhotonNetwork.PlayerListOthers.Length > 0)
            {
                Player guest = PhotonNetwork.PlayerListOthers[0];
                Debug.Log($"[PlayerSetupManager] Local client is Master Client. Transferring Player 2 ownership to guest: {guest.NickName}");
                p2View.TransferOwnership(guest);
            }
        }
        else
        {
            // Guest client requests ownership of Player 2
            Debug.Log("[PlayerSetupManager] Local client is Guest Client. Requesting ownership of Player 2.");
            p2View.RequestOwnership();
        }
    }

    public override void OnPlayerEnteredRoom(Player newPlayer)
    {
        // When Guest player connects (if they loaded scene slightly slower), Master Client transfers Player 2
        if (PhotonNetwork.IsMasterClient)
        {
            GameObject p2 = GameObject.Find("Player2");
            if (p2 != null)
            {
                PhotonView p2View = p2.GetComponent<PhotonView>();
                if (p2View != null)
                {
                    Debug.Log($"[PlayerSetupManager] Guest player {newPlayer.NickName} entered room. Transferring Player 2 ownership.");
                    p2View.TransferOwnership(newPlayer);
                }
            }
        }
    }
}
