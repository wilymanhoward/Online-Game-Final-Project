using UnityEngine;
using Photon.Pun;

public class SmoothPhotonTransformView : MonoBehaviourPun, IPunObservable
{
    [SerializeField] private bool syncPosition = true;
    [SerializeField] private bool syncRotation = true;
    [SerializeField] private float positionLerpSpeed = 18f; // Higher values reduce lag, lower values increase smoothness
    [SerializeField] private float rotationLerpSpeed = 18f;

    private Vector3 latestPosition;
    private Quaternion latestRotation;
    private bool firstTake = true;

    private void Awake()
    {
        latestPosition = transform.position;
        latestRotation = transform.rotation;
    }

    private void OnEnable()
    {
        firstTake = true;
    }

    public void OnPhotonSerializeView(PhotonStream stream, PhotonMessageInfo info)
    {
        if (stream.IsWriting)
        {
            if (syncPosition)
            {
                stream.SendNext(transform.position);
            }
            if (syncRotation)
            {
                stream.SendNext(transform.rotation);
            }
        }
        else
        {
            if (syncPosition)
            {
                latestPosition = (Vector3)stream.ReceiveNext();
            }
            if (syncRotation)
            {
                latestRotation = (Quaternion)stream.ReceiveNext();
            }

            if (firstTake)
            {
                firstTake = false;
                if (syncPosition) transform.position = latestPosition;
                if (syncRotation) transform.rotation = latestRotation;
            }
        }
    }

    private void Update()
    {
        // Only interpolate for remote players
        if (PhotonNetwork.IsConnected && !photonView.IsMine)
        {
            if (syncPosition)
            {
                // Smoothly interpolate position using Lerp
                transform.position = Vector3.Lerp(transform.position, latestPosition, Time.deltaTime * positionLerpSpeed);
            }
            if (syncRotation)
            {
                // Smoothly interpolate rotation using Slerp
                transform.rotation = Quaternion.Slerp(transform.rotation, latestRotation, Time.deltaTime * rotationLerpSpeed);
            }
        }
    }
}
