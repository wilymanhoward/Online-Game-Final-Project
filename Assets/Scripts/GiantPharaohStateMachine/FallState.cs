using UnityEngine;

public class FallState : IState
{
    private EnemyStateMachineController controller;
    private float timer;

    public FallState(EnemyStateMachineController controller)
    {
        this.controller = controller;
    }

    public void Enter()
    {
        controller.SetFinalMoveDirection(Vector3.zero);
        controller.SetHitboxActive(false); // Hitbox hidden in Fall state
        controller.SetHitWallColliderActive(true); // HitWall collider appears in back

        if (controller.Animator != null)
        {
            controller.Animator.SetFloat("Speed", 0f);
            controller.Animator.CrossFadeInFixedTime("Fall", 0.15f);
        }

        timer = controller.fallDuration;
    }

    public void Update()
    {
        if (controller.Animator != null)
        {
            AnimatorStateInfo stateInfo = controller.Animator.GetCurrentAnimatorStateInfo(0);
            bool isFall = stateInfo.IsName("Fall");
            bool inTransition = controller.Animator.IsInTransition(0);

            if (isFall && !inTransition)
            {
                if (stateInfo.normalizedTime >= 1.0f)
                {
                    controller.TransitionToState(EnemyStateMachineController.GiantState.Recovery);
                }
            }
            else if (!isFall && !inTransition)
            {
                // Fallback if not currently in Fall state and not transitioning
                timer -= Time.deltaTime;
                if (timer <= 0)
                {
                    controller.TransitionToState(EnemyStateMachineController.GiantState.Recovery);
                }
            }
        }
        else
        {
            // Fallback if animator is null
            timer -= Time.deltaTime;
            if (timer <= 0)
            {
                controller.TransitionToState(EnemyStateMachineController.GiantState.Recovery);
            }
        }
    }

    public void FixedUpdate()
    {
    }

    public void Exit()
    {
        controller.SetHitWallColliderActive(false);
    }
}
