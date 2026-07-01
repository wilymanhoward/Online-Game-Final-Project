using System.Collections.Generic;
using UnityEngine;

public class DisabilityManager : MonoBehaviour
{
    public static DisabilityManager Instance;

    void Awake(){
        Instance = this;
    }

    public GameObject BlindPlayer;
    public GameObject DeafPlayer;

    public bool IsBlindCurseActive { get; private set; }
    public bool IsDeafCurseActive { get; private set; }

    public void RegisterBlind(GameObject Player){
        BlindPlayer = Player;
        Debug.Log($"[DisabilityManager] BlindPlayer registered: {Player.name}");
        if (IsBlindCurseActive)
        {
            PlayerDisability pd = Player.GetComponent<PlayerDisability>();
            if (pd != null)
            {
                pd.SetBlind(true);
                Debug.Log($"[DisabilityManager] Curse is already active: Enabled Blindness overlay on {Player.name} immediately upon registration.");
            }
        }
    }
    
    public void RegisterDeaf(GameObject Player){
        DeafPlayer = Player;
        Debug.Log($"[DisabilityManager] DeafPlayer registered: {Player.name}");
        if (IsDeafCurseActive)
        {
            PlayerDisability pd = Player.GetComponent<PlayerDisability>();
            if (pd != null)
            {
                pd.SetDeaf(true);
                Debug.Log($"[DisabilityManager] Curse is already active: Enabled Deafness overlay on {Player.name} immediately upon registration.");
            }
        }
    }

    public void UnregisterBlind(){
        BlindPlayer = null;
    }

    public void UnregisterDeaf(){
        DeafPlayer = null;
    }

    public void EnableBlind(){
        IsBlindCurseActive = true;
        if (BlindPlayer != null)
        {
            PlayerDisability pd = BlindPlayer.GetComponent<PlayerDisability>();
            if (pd != null)
            {
                pd.SetBlind(true);
                Debug.Log($"[DisabilityManager] Enabled Blindness overlay on {BlindPlayer.name}");
            }
            else
            {
                Debug.LogError($"[DisabilityManager] {BlindPlayer.name} is missing a PlayerDisability component!");
            }
        }
        else
        {
            Debug.LogWarning("[DisabilityManager] EnableBlind was called, but no player is registered as BLIND! Curse state saved.");
        }
    }

    public void EnableDeaf(){
        IsDeafCurseActive = true;
        if (DeafPlayer != null)
        {
            PlayerDisability pd = DeafPlayer.GetComponent<PlayerDisability>();
            if (pd != null)
            {
                pd.SetDeaf(true);
                Debug.Log($"[DisabilityManager] Enabled Deafness overlay on {DeafPlayer.name}");
            }
            else
            {
                Debug.LogError($"[DisabilityManager] {DeafPlayer.name} is missing a PlayerDisability component!");
            }
        }
        else
        {
            Debug.LogWarning("[DisabilityManager] EnableDeaf was called, but no player is registered as DEAF! Curse state saved.");
        }
    }

    public void DisableBlind(){
        IsBlindCurseActive = false;
        if (BlindPlayer != null)
        {
            PlayerDisability pd = BlindPlayer.GetComponent<PlayerDisability>();
            if (pd != null)
            {
                pd.SetBlind(false);
                Debug.Log($"[DisabilityManager] Disabled Blindness overlay on {BlindPlayer.name}");
            }
            else
            {
                Debug.LogError($"[DisabilityManager] {BlindPlayer.name} is missing a PlayerDisability component!");
            }
        }
        else
        {
            Debug.LogWarning("[DisabilityManager] DisableBlind was called, but no player is registered as BLIND!");
        }
    }

    public void DisableDeaf(){
        IsDeafCurseActive = false;
        if (DeafPlayer != null)
        {
            PlayerDisability pd = DeafPlayer.GetComponent<PlayerDisability>();
            if (pd != null)
            {
                pd.SetDeaf(false);
                Debug.Log($"[DisabilityManager] Disabled Deafness overlay on {DeafPlayer.name}");
            }
            else
            {
                Debug.LogError($"[DisabilityManager] {DeafPlayer.name} is missing a PlayerDisability component!");
            }
        }
        else
        {
            Debug.LogWarning("[DisabilityManager] DisableDeaf was called, but no player is registered as DEAF!");
        }
    }
}
