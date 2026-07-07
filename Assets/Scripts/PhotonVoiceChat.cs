using UnityEngine;
using Photon.Pun;

[RequireComponent(typeof(PhotonView))]
[RequireComponent(typeof(AudioSource))]
public class PhotonVoiceChat : MonoBehaviourPun
{
    private const int SAMPLE_RATE = 8000;   // 8kHz for optimized bandwidth
    private const int SEND_CHUNK_SIZE = 256; // 256 samples per packet (32ms)
    private const int CLIP_DURATION = 3;     // 3 seconds circular playback buffer

    private const int PLAYBACK_START_THRESHOLD = 800;
    private const int UNDERFLOW_THRESHOLD = 240;
    private const int LATENCY_LIMIT = 2400;

    private string micDevice;
    private AudioClip micClip;
    private int lastSamplePos = 0;
    private int recordingSampleRate = SAMPLE_RATE;
    
    // Accumulator for local mic samples
    private System.Collections.Generic.List<float> micAccumulator = new System.Collections.Generic.List<float>();

    // Receiver circular audio clip settings
    private AudioClip playbackClip;
    private int playbackWritePos = 0;
    private bool isPlaying = false;
    private AudioSource audioSource;

    // Toggle Voice Chat states
    private bool isMicEnabled = true;

    private bool wasMine = false;
    private bool isInitialized = false;

    void Start()
    {
        audioSource = GetComponent<AudioSource>();
        
        audioSource.spatialBlend = 0.0f; // 2D (Global Voice Chat)
        audioSource.loop = true;
        audioSource.playOnAwake = false;
        audioSource.rolloffMode = AudioRolloffMode.Logarithmic;
        audioSource.minDistance = 1.5f;
        audioSource.maxDistance = 25f;
    }

    void Update()
    {
        if (!PhotonNetwork.IsConnectedAndReady || !PhotonNetwork.InRoom)
        {
            return;
        }

        bool currentIsMine = photonView.IsMine;
        if (!isInitialized || currentIsMine != wasMine)
        {
            InitializeVoice(currentIsMine);
            wasMine = currentIsMine;
            isInitialized = true;
        }

        if (currentIsMine)
        {
            // Toggle Mic when pressing R
            if (Input.GetKeyDown(KeyCode.R))
            {
                isMicEnabled = !isMicEnabled;
                Debug.Log("Voice Chat Enabled Status: " + isMicEnabled);
                if (!isMicEnabled)
                {
                    // Clear the local accumulator when muting
                    micAccumulator.Clear();
                }
            }

            if (micClip == null) return;

            int currentPos = Microphone.GetPosition(micDevice);
            int newSamplesCount = currentPos - lastSamplePos;
            if (newSamplesCount < 0)
            {
                newSamplesCount = recordingSampleRate - lastSamplePos + currentPos;
            }

            if (newSamplesCount > 0)
            {
                float[] samples = new float[newSamplesCount];

                // Circular read of microphone buffer using recordingSampleRate
                int firstPartLength = recordingSampleRate - lastSamplePos;
                if (firstPartLength < newSamplesCount)
                {
                    float[] part1 = new float[firstPartLength];
                    micClip.GetData(part1, lastSamplePos);
                    float[] part2 = new float[newSamplesCount - firstPartLength];
                    micClip.GetData(part2, 0);
                    
                    System.Array.Copy(part1, 0, samples, 0, part1.Length);
                    System.Array.Copy(part2, 0, samples, part1.Length, part2.Length);
                }
                else
                {
                    micClip.GetData(samples, lastSamplePos);
                }

                lastSamplePos = currentPos;

                // Only accumulate and transmit samples if the microphone is not muted
                if (isMicEnabled)
                {
                    micAccumulator.AddRange(samples);

                    while (micAccumulator.Count >= SEND_CHUNK_SIZE)
                    {
                        float[] chunk = new float[SEND_CHUNK_SIZE];
                        micAccumulator.CopyTo(0, chunk, 0, SEND_CHUNK_SIZE);
                        micAccumulator.RemoveRange(0, SEND_CHUNK_SIZE);

                        byte[] compressed = CompressMuLaw(chunk);
                        photonView.RPC("ReceiveVoiceData", RpcTarget.Others, compressed, recordingSampleRate);
                    }
                }
            }
        }
        else
        {
            if (isPlaying && audioSource.isPlaying && playbackClip != null)
            {
                int playPos = audioSource.timeSamples;
                int senderSampleRate = playbackClip.frequency;
                int bufferLength = senderSampleRate * CLIP_DURATION;
                
                int distance = playbackWritePos - playPos;
                if (distance < 0)
                {
                    distance += bufferLength;
                }

                int playStartThreshold = senderSampleRate / 10;
                int underflowThreshold = senderSampleRate * 30 / 1000;
                int latencyLimit = senderSampleRate * 300 / 1000;

                if (distance < underflowThreshold)
                {
                    audioSource.Pause();
                    isPlaying = false;
                }
                else if (distance > latencyLimit)
                {
                    audioSource.timeSamples = (playbackWritePos - playStartThreshold + bufferLength) % bufferLength;
                }
            }
        }
    }

    [PunRPC]
    void ReceiveVoiceData(byte[] data, int senderSampleRate)
    {
        if (photonView.IsMine) return;

        int bufferLength = senderSampleRate * CLIP_DURATION;

        // If the playbackClip frequency doesn't match the sender's sample rate, recreate it!
        if (playbackClip == null || playbackClip.frequency != senderSampleRate)
        {
            playbackClip = AudioClip.Create("PlaybackVoice", bufferLength, 1, senderSampleRate, false);
            audioSource.clip = playbackClip;
            playbackWritePos = 0;
            isPlaying = false;
            
            float[] silence = new float[bufferLength];
            playbackClip.SetData(silence, 0);
            Debug.Log($"[PhotonVoiceChat] Created playback buffer matching sender sample rate: {senderSampleRate} Hz");
        }

        float[] decompressed = DecompressMuLaw(data);

        int firstPartLength = bufferLength - playbackWritePos;
        if (firstPartLength < decompressed.Length)
        {
            float[] part1 = new float[firstPartLength];
            System.Array.Copy(decompressed, 0, part1, 0, firstPartLength);
            playbackClip.SetData(part1, playbackWritePos);

            float[] part2 = new float[decompressed.Length - firstPartLength];
            System.Array.Copy(decompressed, firstPartLength, part2, 0, part2.Length);
            playbackClip.SetData(part2, 0);
        }
        else
        {
            playbackClip.SetData(decompressed, playbackWritePos);
        }

        playbackWritePos = (playbackWritePos + decompressed.Length) % bufferLength;

        int playStartThreshold = senderSampleRate / 10;

        if (!isPlaying)
        {
            int playPos = audioSource.timeSamples;
            int distance = playbackWritePos - playPos;
            if (distance < 0) distance += bufferLength;

            if (distance >= playStartThreshold)
            {
                audioSource.Play();
                isPlaying = true;
            }
        }
    }

    private void InitializeVoice(bool isLocal)
    {
        Debug.Log($"[PhotonVoiceChat] Initializing voice for {gameObject.name}. IsLocal = {isLocal}");
        
        if (isLocal)
        {
            // Stop any ongoing playback
            if (audioSource != null)
            {
                audioSource.Stop();
                audioSource.clip = null;
            }
            isPlaying = false;
            playbackClip = null;

            // Start microphone
            if (Microphone.devices.Length > 0)
            {
                micDevice = Microphone.devices[0];
                micClip = Microphone.Start(micDevice, true, 1, SAMPLE_RATE);
                recordingSampleRate = micClip != null ? micClip.frequency : SAMPLE_RATE;
                lastSamplePos = 0;
                micAccumulator.Clear();
                Debug.Log($"[PhotonVoiceChat] Started microphone '{micDevice}' for local player. Actual Sample Rate: {recordingSampleRate}");
            }
            else
            {
                Debug.LogWarning("[PhotonVoiceChat] No microphone found!");
            }
        }
        else
        {
            // Stop microphone if it was running
            if (micDevice != null && Microphone.IsRecording(micDevice))
            {
                Microphone.End(micDevice);
                Debug.Log($"[PhotonVoiceChat] Stopped microphone for remote player.");
            }
            micClip = null;

            // Setup playback clip
            playbackClip = AudioClip.Create("PlaybackVoice", SAMPLE_RATE * CLIP_DURATION, 1, SAMPLE_RATE, false);
            if (audioSource != null)
            {
                audioSource.clip = playbackClip;
                audioSource.spatialBlend = 0.0f; // 2D (Global Voice Chat)
                audioSource.loop = true;
                audioSource.playOnAwake = false;
            }
            playbackWritePos = 0;
            isPlaying = false;

            float[] silence = new float[SAMPLE_RATE * CLIP_DURATION];
            playbackClip.SetData(silence, 0);
            Debug.Log("[PhotonVoiceChat] Configured playback buffer for remote player.");
        }
    }

    // Optional: Draw a small GUI overlay on screen for the local player to see their voice chat state
    void OnGUI()
    {
        if (photonView.IsMine)
        {
            GUIStyle style = new GUIStyle(GUI.skin.label);
            style.fontSize = 20;
            style.fontStyle = FontStyle.Bold;
            
            if (isMicEnabled)
            {
                GUI.color = Color.green;
                GUI.Label(new Rect(20, 20, 300, 40), "🎤 Voice Chat: ON (Press R to Mute)", style);
            }
            else
            {
                GUI.color = Color.red;
                GUI.Label(new Rect(20, 20, 300, 40), "🔇 Voice Chat: MUTED (Press R to Talk)", style);
            }
        }
    }

    #region ITU-T G.711 Mu-Law Compression (16-bit to 8-bit)

    private byte[] CompressMuLaw(float[] samples)
    {
        byte[] compressed = new byte[samples.Length];
        const int BIAS = 0x84;
        const int CLIP = 32635;

        for (int i = 0; i < samples.Length; i++)
        {
            short sample16 = (short)(Mathf.Clamp(samples[i], -1f, 1f) * 32767f);
            
            int sign = (sample16 >> 8) & 0x80;
            if (sample16 < 0)
            {
                sample16 = (short)-sample16;
                sign = 0x80;
            }
            else
            {
                sign = 0x00;
            }

            if (sample16 > CLIP) sample16 = CLIP;
            sample16 += BIAS;

            int exponent = 7;
            for (int shift = 0x4000; (sample16 & shift) == 0; shift >>= 1)
            {
                exponent--;
            }

            int fraction = (sample16 >> (exponent + 3)) & 0x0F;
            compressed[i] = (byte)~(sign | (exponent << 4) | fraction);
        }
        return compressed;
    }

    private float[] DecompressMuLaw(byte[] bytes)
    {
        float[] decompressed = new float[bytes.Length];
        for (int i = 0; i < bytes.Length; i++)
        {
            byte muLaw = (byte)~bytes[i];
            int sign = muLaw & 0x80;
            int exponent = (muLaw >> 4) & 0x07;
            int fraction = muLaw & 0x0F;
            
            int number = ((fraction << 3) | 0x84) << exponent;
            number -= 0x84;
            
            short sample16 = (short)(sign != 0 ? -number : number);
            decompressed[i] = sample16 / 32767f;
        }
        return decompressed;
    }

    #endregion
}
