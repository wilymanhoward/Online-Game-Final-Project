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
        timer -= Time.deltaTime;
        if (timer <= 0)
        {
            controller.TransitionToState(EnemyStateMachineController.GiantState.Recovery);
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
