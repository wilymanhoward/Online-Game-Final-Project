using UnityEngine;

public class PlayerFootsteps : MonoBehaviour
{
    public enum FootstepPlayMode
    {
        AutoDetect,
        Intervals,
        Looping
    }

    [Header("Audio Setup")]
    [Tooltip("The audio clip to play. If left empty, will load freesound_community-concrete-footsteps-6752 from Resources.")]
    [SerializeField] private AudioClip footstepClip;
    
    [Tooltip("The AudioSource to use. If left empty, one will be created dynamically.")]
    [SerializeField] private AudioSource audioSource;

    [Header("Play Settings")]
    [SerializeField] private FootstepPlayMode playMode = FootstepPlayMode.AutoDetect;
    
    [Range(0f, 1f)]
    [SerializeField] private float volume = 0.8f;
    
    [Tooltip("Randomize pitch slightly to make footsteps sound more natural.")]
    [SerializeField] private bool randomizePitch = true;
    [Range(0f, 0.2f)]
    [SerializeField] private float pitchVariation = 0.08f;

    [Header("Interval Settings (Only used in Intervals mode)")]
    [SerializeField] private float walkStepInterval = 0.42f;
    [SerializeField] private float runStepInterval = 0.28f;
    [SerializeField] private float velocityThreshold = 0.8f;

    private FirstPersonController firstPersonController;
    private CharacterController characterController;

    private Vector3 lastPosition;
    private float stepTimer = 0f;
    private FootstepPlayMode detectedMode = FootstepPlayMode.Intervals;
    private float originalPitch = 1f;

    private void Awake()
    {
        firstPersonController = GetComponent<FirstPersonController>();
        characterController = GetComponent<CharacterController>();

        if (audioSource == null)
        {
            audioSource = gameObject.AddComponent<AudioSource>();
            audioSource.spatialBlend = 1.0f; // 3D sound so other players can hear it
            audioSource.minDistance = 1.5f;
            audioSource.maxDistance = 15f;
            audioSource.rolloffMode = AudioRolloffMode.Logarithmic;
            audioSource.playOnAwake = false;
        }

        originalPitch = audioSource.pitch;
        lastPosition = transform.position;
    }

    private void Start()
    {
        // Load default clip if not assigned
        if (footstepClip == null)
        {
            footstepClip = Resources.Load<AudioClip>("freesound_community-concrete-footsteps-6752");
            if (footstepClip == null)
            {
                Debug.LogError("[PlayerFootsteps] Could not load 'freesound_community-concrete-footsteps-6752' from Resources. Please place it in Assets/Resources/ or assign it manually.");
            }
        }

        // Auto-detect mode based on clip length
        if (footstepClip != null && playMode == FootstepPlayMode.AutoDetect)
        {
            detectedMode = (footstepClip.length > 1.5f) ? FootstepPlayMode.Looping : FootstepPlayMode.Intervals;
        }
        else
        {
            detectedMode = playMode;
        }
    }

    private void Update()
    {
        if (footstepClip == null) return;

        // Calculate velocity (works for both local and remote players)
        Vector3 currentPosition = transform.position;
        Vector3 displacement = currentPosition - lastPosition;
        displacement.y = 0f; // Only consider horizontal movement
        float speed = Time.deltaTime > 0f ? displacement.magnitude / Time.deltaTime : 0f;
        lastPosition = currentPosition;

        bool isGrounded = IsGroundedCheck();
        bool isMoving = speed > velocityThreshold;

        if (detectedMode == FootstepPlayMode.Looping)
        {
            HandleLoopingMode(isMoving && isGrounded);
        }
        else
        {
            HandleIntervalMode(isMoving && isGrounded, speed);
        }
    }

    private bool IsGroundedCheck()
    {
        // For local player, use CharacterController.isGrounded
        if (firstPersonController != null && firstPersonController.IsLocalPlayer)
        {
            return characterController != null && characterController.isGrounded;
        }
        
        // For remote players, perform a short raycast down
        return Physics.Raycast(transform.position + Vector3.up * 0.1f, Vector3.down, 0.25f);
    }

    private void HandleLoopingMode(bool shouldPlay)
    {
        if (shouldPlay)
        {
            if (!audioSource.isPlaying || audioSource.clip != footstepClip)
            {
                audioSource.clip = footstepClip;
                audioSource.loop = true;
                audioSource.volume = volume;
                audioSource.Play();
            }
        }
        else
        {
            if (audioSource.isPlaying && audioSource.clip == footstepClip)
            {
                audioSource.Stop();
            }
        }
    }

    private void HandleIntervalMode(bool shouldPlay, float speed)
    {
        if (shouldPlay)
        {
            stepTimer += Time.deltaTime;

            // Determine interval based on movement speed
            float walkSpeed = firstPersonController != null ? firstPersonController.moveSpeed : 5.0f;
            float runSpeed = firstPersonController != null ? firstPersonController.runSpeed : 8.5f;
            float speedPercent = Mathf.InverseLerp(walkSpeed * 0.5f, runSpeed, speed);
            float currentInterval = Mathf.Lerp(walkStepInterval, runStepInterval, speedPercent);

            if (stepTimer >= currentInterval)
            {
                PlaySingleFootstep();
                stepTimer = 0f;
            }
        }
        else
        {
            // Reset timer when not moving or not grounded so footsteps start instantly when starting to move
            stepTimer = walkStepInterval; 
        }
    }

    private void PlaySingleFootstep()
    {
        if (randomizePitch)
        {
            audioSource.pitch = originalPitch + Random.Range(-pitchVariation, pitchVariation);
        }
        
        audioSource.PlayOneShot(footstepClip, volume);
    }
}
