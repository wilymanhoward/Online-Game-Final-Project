using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Cel_MainMenu : MonoBehaviour
{
    [Header("Menu Objects")]
    [Tooltip("Put everything that needs to disappear here (Title, Buttons, Stone Block)")]
    public GameObject[] objectsToHide;

    [Header("Camera Movement")]
    public Transform mainCamera;
    public Transform targetCameraPosition;
    public float cameraMoveSpeed = 2f;

    private bool isMovingCamera = false;

    public void OnPlayClicked()
    {
        // Hide all specified objects
        foreach (GameObject obj in objectsToHide)
        {
            if (obj != null)
                obj.SetActive(false);
        }

        // Start moving camera to the target position
        isMovingCamera = true;
    }

    public void OnExitClicked()
    {
        Application.Quit();
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#endif
    }

    void Update()
    {
        if (isMovingCamera && mainCamera != null && targetCameraPosition != null)
        {
            // Smoothly move the camera to the target position and rotation
            mainCamera.position = Vector3.Lerp(mainCamera.position, targetCameraPosition.position, Time.deltaTime * cameraMoveSpeed);
            mainCamera.rotation = Quaternion.Slerp(mainCamera.rotation, targetCameraPosition.rotation, Time.deltaTime * cameraMoveSpeed);
            
            // Stop moving if close enough
            if (Vector3.Distance(mainCamera.position, targetCameraPosition.position) < 0.01f)
            {
                mainCamera.position = targetCameraPosition.position;
                mainCamera.rotation = targetCameraPosition.rotation;
                isMovingCamera = false;
            }
        }
    }
}
