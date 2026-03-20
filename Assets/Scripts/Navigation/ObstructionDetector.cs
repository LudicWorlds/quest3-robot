using System;
using Meta.XR;
using UnityEngine;
using LudicWorlds;

/// <summary>
/// Uses the Quest 3 depth sensor to detect obstructions in the robot's forward path.
/// Casts rays ahead of the robot and fires events when obstructions are detected or cleared.
/// Attach to the same GameObject as RobotController.
/// </summary>
public class ObstructionDetector : MonoBehaviour
{
    [Header("Depth Sensing")]
    [SerializeField] private EnvironmentRaycastManager _raycastManager;

    [Header("Detection Settings")]
    [SerializeField] private float _detectionDistance = 0.25f; // Obstruction threshold in metres
    [SerializeField] private float _clearanceMargin = 0.05f; // Extra distance required to clear obstruction
    [SerializeField] private float _raycastInterval = 0.1f; // How often to check (seconds)
    [SerializeField] private int _numRays = 3; // Number of rays to cast (centre + spread)
    [SerializeField] private float _spreadAngle = 15f; // Horizontal spread angle in degrees

    [Header("References")]
    [SerializeField] private Transform _headsetTransform;

    private EventBroker _eventBroker;
    private float _lastRaycastTime;
    private bool _obstructionDetected;
    private bool _wasObstructed; // Track previous state for edge detection
    private bool _enabled = true;

    public bool ObstructionDetected => _obstructionDetected;

    public bool DetectionEnabled
    {
        get => _enabled;
        set => _enabled = value;
    }

    private void Start()
    {
        _eventBroker = EventBroker.GetInstance();

        if (_raycastManager == null)
        {
            Debug.LogWarning("[ObstructionDetector] EnvironmentRaycastManager not assigned. Obstruction detection disabled.");
        }

        if (_headsetTransform == null)
        {
            var rc = GetComponent<RobotController>();
            if (rc != null)
            {
                _headsetTransform = rc.HeadsetTransform;
            }
        }
    }

    private void Update()
    {
        if (!_enabled || _raycastManager == null || _headsetTransform == null)
            return;

        if (Time.time - _lastRaycastTime < _raycastInterval)
            return;

        _lastRaycastTime = Time.time;
        _obstructionDetected = CheckForObstructions();

        // Fire events only on transitions
        if (_obstructionDetected && !_wasObstructed)
        {
            _eventBroker.DispatchEvent(EventID.OBSTRUCTION_DETECTED, EventArgs.Empty);
        }
        else if (!_obstructionDetected && _wasObstructed)
        {
            _eventBroker.DispatchEvent(EventID.OBSTRUCTION_CLEARED, EventArgs.Empty);
        }

        _wasObstructed = _obstructionDetected;
    }

    private bool CheckForObstructions()
    {
        Vector3 origin = _headsetTransform.position;
        Vector3 forward = _headsetTransform.forward;

        // Project forward direction onto horizontal plane
        forward.y = 0;
        forward.Normalize();

        // Use hysteresis: once obstructed, require a greater distance to clear
        float threshold = _wasObstructed
            ? _detectionDistance + _clearanceMargin
            : _detectionDistance;

        for (int i = 0; i < _numRays; i++)
        {
            // Calculate ray direction with horizontal spread
            float angle = 0f;
            if (_numRays > 1)
            {
                float t = (float)i / (_numRays - 1); // 0 to 1
                angle = Mathf.Lerp(-_spreadAngle, _spreadAngle, t);
            }

            Vector3 direction = Quaternion.AngleAxis(angle, Vector3.up) * forward;
            var ray = new Ray(origin, direction);

            if (_raycastManager.Raycast(ray, out EnvironmentRaycastHit hit, threshold))
            {
                if (hit.status == EnvironmentRaycastHitStatus.Hit)
                {
                    float distance = Vector3.Distance(origin, hit.point);

                    if (distance < threshold)
                    {
                        Debug.Log($"[ObstructionDetector] Obstruction at {distance:F2}m (ray {i}, angle {angle:F0})");
                        DebugPanel.UpdateNavState($"Obstruction: {distance:F2}m");
                        return true;
                    }
                }
            }
        }

        return false;
    }

}
