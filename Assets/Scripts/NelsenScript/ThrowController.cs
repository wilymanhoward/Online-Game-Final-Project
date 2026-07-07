using UnityEngine;

public class ThrowController : MonoBehaviour
{
    [Tooltip("Target player's PlayerThrow component. If null, will try to find it on this GameObject.")]
    [SerializeField] private PlayerThrow targetPlayerThrow;

    private void Awake()
    {
        if (targetPlayerThrow == null)
        {
            targetPlayerThrow = GetComponent<PlayerThrow>();
        }
    }

    private void Start(){
        SetThrowingEnabled(false);
    }

    /// <summary>
    /// Enables or disables the throwing mechanic for the target player.
    /// Can be called from triggers, buttons, or custom level logic.
    /// </summary>
    public void SetThrowingEnabled(bool isEnabled)
    {
        if (targetPlayerThrow == null)
        {
            targetPlayerThrow = GetComponent<PlayerThrow>();
        }

        if (targetPlayerThrow != null)
        {
            targetPlayerThrow.SetThrowingEnabled(isEnabled);
            Debug.Log($"[ThrowController] Throwing has been {(isEnabled ? "ENABLED" : "DISABLED")} for {targetPlayerThrow.gameObject.name}");
        }
        else
        {
            Debug.LogWarning("[ThrowController] Cannot toggle throwing because no PlayerThrow component was found!");
        }
    }

    /// <summary>
    /// Toggles the current throwing state of the target player.
    /// </summary>
    public void ToggleThrowing()
    {
        if (targetPlayerThrow != null)
        {
            SetThrowingEnabled(!targetPlayerThrow.IsThrowingEnabled());
        }
    }
}
