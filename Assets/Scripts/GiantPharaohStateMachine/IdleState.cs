using UnityEngine;

public class IdleState : IState
{
    private EnemyStateMachineController controller;

    public IdleState(EnemyStateMachineController controller)
    {
        this.controller = controller;
    }

    public void Enter()
    {
        if (controller.Animator != null)
        {
            controller.Animator.SetFloat("Speed", 0f);
            controller.Animator.CrossFadeInFixedTime("Idle", 0.15f);
        }
        controller.SetFinalMoveDirection(Vector3.zero);
        controller.SetHitboxActive(true);
        controller.SetHitWallColliderActive(false);
    }

    public void Update()
    {
        if (controller.PlayerInArena && controller.TargetPlayer != null)
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
