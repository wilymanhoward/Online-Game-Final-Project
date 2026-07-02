using UnityEngine;

public class PlayerDisability : MonoBehaviour
{
    public GameObject BlindOverlay;
    public GameObject DeafOverlay;

    public bool IsBlindActive { get; private set; }
    public bool IsDeafActive { get; private set; }

    void Start(){
        SetBlind(false);
        SetDeaf(false);
    }

    public void SetBlind(bool isBlind){
        IsBlindActive = isBlind;
        BlindOverlay.SetActive(isBlind);
    }

    public void SetDeaf(bool isDeaf){
        IsDeafActive = isDeaf;
        DeafOverlay.SetActive(isDeaf);
    }
}
