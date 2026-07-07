using UnityEngine;

public class PharaohFootTrigger : MonoBehaviour
{
    [HideInInspector]
    public GiantPharaohAI pharaohAI;

    void OnTriggerEnter(Collider other)
    {
        if (pharaohAI == null || !pharaohAI.IsStompingActive()) return;

        FirstPersonController player = other.GetComponent<FirstPersonController>();
        if (player != null)
        {
            player.Respawn();
        }
    }

    void OnTriggerStay(Collider other)
    {
        OnTriggerEnter(other);
    }
}
