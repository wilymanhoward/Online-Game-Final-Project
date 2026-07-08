using UnityEngine;

public class WalkState : IState
{
    private EnemyStateMachineController controller;

    public WalkState(EnemyStateMachineController controller)
    {
        this.controller = controller;
    }

    public void Enter()
    {
        if (controller.Animator != null)
        {
            controller.Animator.SetFloat("Speed", controller.walkSpeed);
            controller.Animator.CrossFadeInFixedTime("Walk", 0.15f);
        }
        controller.SetHitboxActive(true);
        controller.SetHitWallColliderActive(false);

        // Pre-select a random attack when we start chasing
        if (controller.PlayerInArena && controller.TargetPlayer != null)
        {
            controller.SelectRandomAttack();
        }
    }

    public void Update()
    {
        if (controller.PlayerInArena && controller.TargetPlayer != null)
        {
            // If we don't have a currently selected attack, choose one
            if (string.IsNullOrEmpty(controller.CurrentAttack.animatorStateName))
            {
                controller.SelectRandomAttack();
            }

            // Walk/Chase Player
            Vector3 targetPos = controller.TargetPlayer.transform.position;
            targetPos.y = controller.transform.position.y; // Keep level

            float dist = Vector3.Distance(controller.transform.position, controller.TargetPlayer.transform.position);

            // Rotate towards player
            Vector3 dir = (targetPos - controller.transform.position).normalized;
            if (dir != Vector3.zero)
            {
                Quaternion targetRot = Quaternion.LookRotation(dir);
                controller.transform.rotation = Quaternion.Slerp(controller.transform.rotation, targetRot, controller.rotationSpeed * Time.deltaTime);
            }

            // Check if player is in range of the currently selected random attack
            if (!string.IsNullOrEmpty(controller.CurrentAttack.animatorStateName))
            {
                if (dist <= controller.CurrentAttack.distanceToAttack)
                {
                    controller.TransitionToState(EnemyStateMachineController.GiantState.Attack);
                    return;
                }
            }

            // Move forward to close the gap to the selected attack range
            controller.SetFinalMoveDirection(controller.transform.forward * controller.walkSpeed);
        }
        else
        {
            // Walk back to Idle Point
            Vector3 targetPos = controller.idlePoint;
            targetPos.y = controller.transform.position.y;

            float distToIdle = Vector3.Distance(controller.transform.position, targetPos);

            if (distToIdle <= controller.targetReachedThreshold)
            {
                controller.ClearCurrentAttack();
                controller.TransitionToState(EnemyStateMachineController.GiantState.Idle);
                return;
            }

            // Rotate towards idle point
            Vector3 dir = (targetPos - controller.transform.position).normalized;
            if (dir != Vector3.zero)
            {
                Quaternion targetRot = Quaternion.LookRotation(dir);
                controller.transform.rotation = Quaternion.Slerp(controller.transform.rotation, targetRot, controller.rotationSpeed * Time.deltaTime);
            }

            // Move forward
            controller.SetFinalMoveDirection(controller.transform.forward * controller.walkSpeed);
        }
    }

    public void FixedUpdate()
    {
    }

    public void Exit()
    {
    }
}
