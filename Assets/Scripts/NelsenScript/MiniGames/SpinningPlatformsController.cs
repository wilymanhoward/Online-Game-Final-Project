using System.Collections.Generic;
using UnityEngine;

public class SpinningPlatformsController : MonoBehaviour
{
    [Header("Spinning Platforms")]
    [SerializeField] private List<Animator> spinningPlatformAnimators = new List<Animator>();

    /// <summary>
    /// Sets the "PlatformsOn" boolean parameter for all registered animators.
    /// </summary>
    /// <param name="isOn">True to turn the platforms on/spinning, false to turn them off.</param>
    public void SetSpinningPlatforms(bool isOn)
    {
        foreach (var anim in spinningPlatformAnimators)
        {
            if (anim != null)
            {
                anim.SetBool("PlatformsOn", isOn);
            }
        }
    }

    /// <summary>
    /// Deactivates spinning platforms by setting the "PlatformsOn" parameter to false.
    /// </summary>
    public void DeactivateSpinningPlatforms()
    {
        SetSpinningPlatforms(false);
    }

    /// <summary>
    /// Activates spinning platforms by setting the "PlatformsOn" parameter to true.
    /// </summary>
    public void ActivateSpinningPlatforms()
    {
        SetSpinningPlatforms(true);
    }
}
