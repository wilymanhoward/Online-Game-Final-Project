using UnityEngine;

public class HurtState : IState
{
    private EnemyStateMachineController controller;
    private float timer;

    public HurtState(EnemyStateMachineController controller)
    {
        this.controller = controller;
    }

    public void Enter()
    {
        controller.SetFinalMoveDirection(Vector3.zero);
        controller.SetHitboxActive(true);
        controller.SetHitWallColliderActive(false);

        if (controller.Animator != null)
        {
            controller.Animator.SetFloat("Speed", 0f);
            controller.Animator.CrossFadeInFixedTime("Hurt", 0.15f);
        }

        timer = controller.hurtDuration;
    }

    public void Update()
    {
        timer -= Time.deltaTime;
        if (timer <= 0)
        {
            if (controller.PlayerInArena && controller.TargetPlayer != null)
            {
                controller.TransitionToState(EnemyStateMachineController.GiantState.Walk);
            }
            else
            {
                controller.TransitionToState(EnemyStateMachineController.GiantState.Idle);
            }
        }
    }

    public void FixedUpdate()
    {
    }

    public void Exit()
    {
    }
}
