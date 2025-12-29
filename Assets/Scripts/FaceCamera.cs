using UnityEngine;

/// <summary>
/// Makes a GameObject (typically text) always face the camera/user
/// Useful for labels and UI elements in VR that should always be readable
/// </summary>
public class FaceCamera : MonoBehaviour
{
    [Header("Target Camera")]
    [SerializeField] private Transform m_cameraTransform;

    [Header("Billboard Settings")]
    [Tooltip("If true, only rotates around Y axis (keeps text upright)")]
    [SerializeField] private bool m_lockYAxis = true;

    [Tooltip("If true, text faces away from camera (for opposite orientation)")]
    [SerializeField] private bool m_reverse = false;

    private void Start()
    {
        // Auto-find the camera if not assigned
        if (m_cameraTransform == null)
        {
            // Try to find OVRCameraRig's CenterEyeAnchor
            GameObject centerEye = GameObject.Find("CenterEyeAnchor");
            if (centerEye != null)
            {
                m_cameraTransform = centerEye.transform;
                Debug.Log("FaceCamera: Found CenterEyeAnchor");
            }
            else
            {
                // Fallback to main camera
                Camera mainCam = Camera.main;
                if (mainCam != null)
                {
                    m_cameraTransform = mainCam.transform;
                    Debug.Log("FaceCamera: Using Main Camera");
                }
                else
                {
                    Debug.LogWarning("FaceCamera: No camera found! This component won't work.");
                }
            }
        }
    }

    private void LateUpdate()
    {
        if (m_cameraTransform == null) return;

        // Calculate direction to camera
        Vector3 directionToCamera = transform.position - m_cameraTransform.position;

        // Reverse direction if needed
        if (m_reverse)
        {
            directionToCamera = -directionToCamera;
        }

        if (m_lockYAxis)
        {
            // Keep text upright by only rotating around Y axis
            directionToCamera.y = 0f;
        }

        // Only rotate if there's a valid direction
        if (directionToCamera.sqrMagnitude > 0.0001f)
        {
            Quaternion targetRotation = Quaternion.LookRotation(directionToCamera);
            transform.rotation = targetRotation;
        }
    }
}
