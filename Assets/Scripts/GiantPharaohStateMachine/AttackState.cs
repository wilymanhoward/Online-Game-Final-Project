using UnityEngine;
using System.Collections;

public class AttackState : IState
{
    private EnemyStateMachineController controller;
    private Coroutine attackCoroutine;
    private float elapsed;
    private float totalDuration;

    public AttackState(EnemyStateMachineController controller)
    {
        this.controller = controller;
    }

    public void Enter()
    {
        controller.SetFinalMoveDirection(Vector3.zero);
        controller.SetHitboxActive(true);
        controller.SetHitWallColliderActive(false);

        totalDuration = controller.GetAttackDuration(controller.CurrentAttack.animatorStateName);
        elapsed = 0f;

        // Initialize object states
        UpdateAttackObjects(0f);

        // Run the specific attack routine
        attackCoroutine = controller.StartCoroutine(ExecuteAttackRoutine());
    }

    public void Update()
    {
        elapsed += Time.deltaTime;
        float progress = Mathf.Clamp01(elapsed / totalDuration);
        UpdateAttackObjects(progress);
    }

    public void FixedUpdate()
    {
    }

    public void Exit()
    {
        if (attackCoroutine != null)
        {
            controller.StopCoroutine(attackCoroutine);
            attackCoroutine = null;
        }

        // Force disable all configured game objects on exit
        if (controller.CurrentAttack.objectsToActivate != null)
        {
            foreach (var trigger in controller.CurrentAttack.objectsToActivate)
            {
                if (trigger.gameObjectToActivate != null)
                {
                    trigger.gameObjectToActivate.SetActive(false);
                }
            }
        }

        controller.ResetAttackStates();
    }

    private void UpdateAttackObjects(float progress)
    {
        if (controller.CurrentAttack.objectsToActivate != null)
        {
            foreach (var trigger in controller.CurrentAttack.objectsToActivate)
            {
                if (trigger.gameObjectToActivate != null)
                {
                    bool active = progress >= trigger.startPercentage && progress <= trigger.endPercentage;
                    trigger.gameObjectToActivate.SetActive(active);
                }
            }
        }
    }

    private IEnumerator ExecuteAttackRoutine()
    {
        string stateName = controller.CurrentAttack.animatorStateName;

        if (stateName.Contains("Stomp") || stateName.Contains("stomp"))
        {
            yield return controller.ExecuteStompAttackRoutine();
        }
        else if (stateName.Contains("Jump") || stateName.Contains("jump"))
        {
            yield return controller.ExecuteJumpAttackRoutine();
        }
        else
        {
            // Default fallback generic attack
            if (controller.Animator != null)
            {
                controller.Animator.CrossFadeInFixedTime(stateName, 0.15f);
            }
            yield return new WaitForSeconds(1.5f);
        }

        // Go back to Walk state
        controller.TransitionToState(EnemyStateMachineController.GiantState.Walk);
    }
}
