using System;
using UnityEngine;
using UnityEngine.InputSystem;

[CreateAssetMenu(fileName = "InputReader", menuName = "Input/Input Reader")]
public class InputReader : ScriptableObject, PlayerInput.IMovementActions, PlayerInput.IInteractActions
{
    private PlayerInput playerInput;

    public event Action<Vector2> OnWalk;
    public event Action<Vector2> OnLook;
    public event Action OnJump;
    public event Action OnJumpCanceled;
    public event Action OnSprint;
    public event Action OnSprintCanceled;
    public event Action OnInteract;
    public event Action OnAim;
    public event Action OnAimCanceled;
    public event Action OnThrow;
    public event Action OnThrowCanceled;

    private void OnEnable()
    {
        if (playerInput != null)
        {
            playerInput.Disable();
        }
        playerInput = new PlayerInput();
        playerInput.Movement.SetCallbacks(this);
        playerInput.Interact.SetCallbacks(this);
        playerInput.Enable();
    }

    private void OnDisable()
    {
        if (playerInput != null)
        {
            playerInput.Disable();
        }
    }

    void PlayerInput.IMovementActions.OnWalk(InputAction.CallbackContext context)
    {
        OnWalk?.Invoke(context.ReadValue<Vector2>());
    }

    void PlayerInput.IMovementActions.OnLook(InputAction.CallbackContext context)
    {
        OnLook?.Invoke(context.ReadValue<Vector2>());
    }

    void PlayerInput.IMovementActions.OnJump(InputAction.CallbackContext context)
    {
        if (context.phase == InputActionPhase.Performed)
        {
            OnJump?.Invoke();
        }
        else if (context.phase == InputActionPhase.Canceled)
        {
            OnJumpCanceled?.Invoke();
        }
    }

    void PlayerInput.IMovementActions.OnSprint(InputAction.CallbackContext context)
    {
        if (context.phase == InputActionPhase.Performed)
        {
            OnSprint?.Invoke();
        }
        else if (context.phase == InputActionPhase.Canceled)
        {
            OnSprintCanceled?.Invoke();
        }
    }

    void PlayerInput.IInteractActions.OnInteract(InputAction.CallbackContext context)
    {
        if (context.phase == InputActionPhase.Performed)
        {
            OnInteract?.Invoke();
        }
    }

    void PlayerInput.IInteractActions.OnAim(InputAction.CallbackContext context)
    {
        if (context.phase == InputActionPhase.Performed)
        {
            OnAim?.Invoke();
        }
        else if (context.phase == InputActionPhase.Canceled)
        {
            OnAimCanceled?.Invoke();
        }
    }

    void PlayerInput.IInteractActions.OnThrow(InputAction.CallbackContext context)
    {
        if (context.phase == InputActionPhase.Performed)
        {
            OnThrow?.Invoke();
        }
        else if (context.phase == InputActionPhase.Canceled)
        {
            OnThrowCanceled?.Invoke();
        }
    }
}
