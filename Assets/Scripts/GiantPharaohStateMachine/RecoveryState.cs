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
        if (controller.Animator != null)
        {
            AnimatorStateInfo stateInfo = controller.Animator.GetCurrentAnimatorStateInfo(0);
            bool isRecovery = stateInfo.IsName("Recovery");
            bool inTransition = controller.Animator.IsInTransition(0);

            if (isRecovery && !inTransition)
            {
                if (stateInfo.normalizedTime >= 1.0f)
                {
                    controller.TransitionToState(EnemyStateMachineController.GiantState.Walk);
                }
            }
            else if (!isRecovery && !inTransition)
            {
                // Fallback if not currently in recovery state and not transitioning (e.g., animation config issues)
                timer -= Time.deltaTime;
                if (timer <= 0)
                {
                    controller.TransitionToState(EnemyStateMachineController.GiantState.Walk);
                }
            }
        }
        else
        {
            // Fallback if animator is null
            timer -= Time.deltaTime;
            if (timer <= 0)
            {
                controller.TransitionToState(EnemyStateMachineController.GiantState.Walk);
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
