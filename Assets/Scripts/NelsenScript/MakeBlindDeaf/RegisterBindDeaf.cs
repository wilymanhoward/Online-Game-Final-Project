using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class RegisterBindDeaf : MonoBehaviour
{
    public enum Disability
    {
        Blind, Deaf
    }

    public Disability disability;

    private GameObject Player;

    void OnTriggerEnter(Collider other){
        PlayerDisability pd = other.GetComponentInParent<PlayerDisability>();
        if(pd != null){
            Player = pd.gameObject;
            RegisterDisability();
        }
    }

    public void RegisterDisability(){
        if (Player == null) return;

        if(disability == Disability.Blind){
            DisabilityManager.Instance.RegisterBlind(Player);
            Debug.Log($"[RegisterBindDeaf] Registered {Player.name} as BLIND");
        }
        else{
            DisabilityManager.Instance.RegisterDeaf(Player);
            Debug.Log($"[RegisterBindDeaf] Registered {Player.name} as DEAF");
        }
    }
}
