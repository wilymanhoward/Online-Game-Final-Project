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

    private bool inputsDisabled = false;
    private bool inputsDisabledExceptLook = false;
    private bool inputsDisabledExceptInteract = false;

    public void SetInputsDisabled(bool disabled)
    {
        inputsDisabled = disabled;
        if (disabled)
        {
            OnWalk?.Invoke(Vector2.zero);
            OnLook?.Invoke(Vector2.zero);
            OnSprintCanceled?.Invoke();
            OnAimCanceled?.Invoke();
            OnThrowCanceled?.Invoke();
        }
    }

    public void SetInputsDisabledExceptLook(bool disabled)
    {
        inputsDisabledExceptLook = disabled;
        if (disabled)
        {
            OnWalk?.Invoke(Vector2.zero);
            OnSprintCanceled?.Invoke();
            OnAimCanceled?.Invoke();
            OnThrowCanceled?.Invoke();
        }
    }

    public void SetInputsDisabledExceptInteract(bool disabled)
    {
        inputsDisabledExceptInteract = disabled;
        if (disabled)
        {
            OnWalk?.Invoke(Vector2.zero);
            OnLook?.Invoke(Vector2.zero);
            OnSprintCanceled?.Invoke();
            OnAimCanceled?.Invoke();
            OnThrowCanceled?.Invoke();
        }
    }

    private void OnEnable()
    {
        inputsDisabled = false;
        inputsDisabledExceptLook = false;
        inputsDisabledExceptInteract = false;

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
        if (inputsDisabled || inputsDisabledExceptLook || inputsDisabledExceptInteract) return;
        OnWalk?.Invoke(context.ReadValue<Vector2>());
    }

    void PlayerInput.IMovementActions.OnLook(InputAction.CallbackContext context)
    {
        if (inputsDisabled || inputsDisabledExceptInteract) return;
        OnLook?.Invoke(context.ReadValue<Vector2>());
    }

    void PlayerInput.IMovementActions.OnJump(InputAction.CallbackContext context)
    {
        if (inputsDisabled || inputsDisabledExceptLook || inputsDisabledExceptInteract) return;
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
        if (inputsDisabled || inputsDisabledExceptLook || inputsDisabledExceptInteract) return;
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
        if (inputsDisabled || inputsDisabledExceptLook) return;
        if (context.phase == InputActionPhase.Performed)
        {
            OnInteract?.Invoke();
        }
    }

    void PlayerInput.IInteractActions.OnAim(InputAction.CallbackContext context)
    {
        if (inputsDisabled || inputsDisabledExceptLook || inputsDisabledExceptInteract) return;
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
        if (inputsDisabled || inputsDisabledExceptLook || inputsDisabledExceptInteract) return;
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
