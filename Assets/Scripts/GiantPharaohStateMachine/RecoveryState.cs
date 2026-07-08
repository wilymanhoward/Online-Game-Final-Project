using UnityEngine;

public class RecoveryState : IState
{
    private EnemyStateMachineController controller;
    private float timer;

    public RecoveryState(EnemyStateMachineController controller)
    {
        this.controller = controller;
    }

    public void Enter()
    {
        controller.SetFinalMoveDirection(Vector3.zero);
        controller.SetHitboxActive(false); // Hitbox hidden in Recovery state
        controller.SetHitWallColliderActive(false);

        if (controller.Animator != null)
        {
            controller.Animator.CrossFadeInFixedTime("Recovery", 0.15f);
        }

        timer = controller.recoveryDuration;
    }

    public void Update()
    {
        timer -= Time.deltaTime;
        if (timer <= 0)
        {
            controller.TransitionToState(EnemyStateMachineController.GiantState.Walk);
        }
    }

    public void FixedUpdate()
    {
    }

    public void Exit()
    {
    }
}
