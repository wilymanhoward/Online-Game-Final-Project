using UnityEngine;

[RequireComponent(typeof(AudioSource))]
public class MoveRotateAudioPlayer : MonoBehaviour
{
    [Header("References")]
    [Tooltip("The MoveRotateObject to listen to. If left empty, will attempt to find one on this GameObject.")]
    [SerializeField] private MoveRotateObject targetObject;

    [Tooltip("The AudioSource to play sounds from. If left empty, will use the AudioSource on this GameObject.")]
    [SerializeField] private AudioSource audioSource;

    [Header("Audio Clips")]
    [SerializeField] private AudioClip openClip;
    [SerializeField] private AudioClip closeClip;

    [Header("Settings")]
    [Range(0f, 1f)]
    [SerializeField] private float volume = 1f;
    [Tooltip("If true, the audio clip will loop while the object is moving.")]
    [SerializeField] private bool loopAudio = true;

    private void Awake()
    {
        // Auto-resolve MoveRotateObject if not set
        if (targetObject == null)
        {
            targetObject = GetComponent<MoveRotateObject>();
        }

        // Auto-resolve AudioSource if not set
        if (audioSource == null)
        {
            audioSource = GetComponent<AudioSource>();
        }
    }

    private void OnEnable()
    {
        if (targetObject != null)
        {
            targetObject.OnActivateObject.AddListener(OnObjectActivated);
            targetObject.OnDeactivateObject.AddListener(OnObjectDeactivated);
            targetObject.OnReachedOpen.AddListener(OnMovementStopped);
            targetObject.OnReachedClosed.AddListener(OnMovementStopped);
        }
        else
        {
            Debug.LogWarning($"[MoveRotateAudioPlayer] No MoveRotateObject target assigned on {gameObject.name}.", this);
        }
    }

    private void OnDisable()
    {
        if (targetObject != null)
        {
            targetObject.OnActivateObject.RemoveListener(OnObjectActivated);
            targetObject.OnDeactivateObject.RemoveListener(OnObjectDeactivated);
            targetObject.OnReachedOpen.RemoveListener(OnMovementStopped);
            targetObject.OnReachedClosed.RemoveListener(OnMovementStopped);
        }
    }

    private void OnObjectActivated()
    {
        PlaySound(openClip);
    }

    private void OnObjectDeactivated()
    {
        PlaySound(closeClip);
    }

    private void OnMovementStopped()
    {
        StopSound();
    }

    private void PlaySound(AudioClip clip)
    {
        if (clip == null) return;
        if (audioSource == null)
        {
            Debug.LogError($"[MoveRotateAudioPlayer] No AudioSource found on {gameObject.name}.", this);
            return;
        }

        audioSource.clip = clip;
        audioSource.loop = loopAudio;
        audioSource.volume = volume;
        audioSource.Play();
    }

    private void StopSound()
    {
        if (audioSource != null && audioSource.isPlaying)
        {
            audioSource.Stop();
        }
    }
}
