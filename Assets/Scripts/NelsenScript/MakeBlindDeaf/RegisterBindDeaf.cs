using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class RegisterBindDeaf : MonoBehaviour
{
    public enum Disability
    {
        Blind,
        Deaf,
        Both
    }

    public Disability disability;

    void OnTriggerEnter(Collider other)
    {
        PlayerDisability pd = other.GetComponentInParent<PlayerDisability>();
        if (pd != null)
        {
            // Register disability on the player based on the trigger's setting
            if (disability == Disability.Blind)
            {
                pd.RegisterDisability(PlayerDisability.DisabilityType.Blind);
            }
            else if (disability == Disability.Deaf)
            {
                pd.RegisterDisability(PlayerDisability.DisabilityType.Deaf);
            }
            else if (disability == Disability.Both)
            {
                pd.RegisterDisability(PlayerDisability.DisabilityType.Both);
            }

            // Immediately activate the disability on the player
            pd.EnableDisability();
        }
    }
}
