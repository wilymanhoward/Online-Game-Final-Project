using UnityEngine;

public class GiantPharaohAI : EnemyStateMachineController
{
    // Inherits all states, movement, combat parameters, event-triggers, and procedural bone animations
    // from EnemyStateMachineController. Keeping this subclass preserves the component type references
    // in Unity scenes and existing scripts (like FirstPersonController) without breaking GUID/file links.
}
