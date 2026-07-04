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
        if (BlindOverlay != null)
        {
            BlindOverlay.SetActive(isBlind);
        }
    }

    public void SetDeaf(bool isDeaf){
        IsDeafActive = isDeaf;
        if (DeafOverlay != null)
        {
            DeafOverlay.SetActive(isDeaf);
        }
    }
}
