using System.Collections.Generic;
using System.Linq;
using LudicWorlds;
using UnityEngine;
using UnityEngine.AI;


[RequireComponent(typeof(ESP32_Communicator))]
public class RobotNavigation : GameObjectStateMachine<NavigationID>
{
    [Header("Visual Feedback")]
    [SerializeField] private GameObject _destinationMarker;
    [SerializeField] private GameObject _waypointMarker;

    [Header("Navigation")]
    [SerializeField] private NavMeshAgent _virtualAgent; // The pathfinding guide
    private List<Vector3> m_waypoints = new();
    private int _currentWaypointIndex = 0;
    //private const float WAYPOINT_REACHED_DISTANCE = 0.2f;

    //[Header("NavMesh Visualization")]
    //[SerializeField] private NavMeshVisualizer m_navMeshVisualizer;

    [Header("Debug Visualization")]
    [SerializeField] private LineRenderer _pathLineRenderer;

    public bool DestinationMoved { get; set; }

    private string _instruction = "";
    public string DebugInfo { get; set; } = "";

    private bool _isTurning = false;

    public bool IsTurning
    {
        get { return _isTurning; }
        set
        {
            if (value == true && value != _isTurning)
            {
                PlayAudio(AudioID.TURNING);
            }

            _isTurning = value;
        }
    }


    private ESP32_Communicator _esp32_communicator;

    public ESP32_Communicator Com
    {
        get { return _esp32_communicator; }
    }



    private Transform _headsetTransform;

    public Transform HeadsetTransform
    {
        get { return _headsetTransform; }
    }

    public GameObject DestinationMarker
    {
        get { return _destinationMarker; }
    }

    public GameObject WaypointMarker
    {
        get { return _waypointMarker; }
    }

    public NavMeshAgent VirtualAgent
    {
        get { return _virtualAgent; }
    }

    public string Instruction
    {
        get { return _instruction; }
        set { _instruction = value; }
    }

    public float TargetTurnAccuracy { get; set; } = NavState.COARSE_TURN_ACCURACY;

    private EventBroker _eventBroker;

    protected override void InitStates()
    {
        AddState(new Init_NavState(this, NavigationID.INIT));
        AddState(new Idle_NavState(this, NavigationID.IDLE));
        AddState(new Pathfinding_NavState(this, NavigationID.PATHFINDING));
        AddState(new Deciding_NavState(this, NavigationID.DECIDING));
        AddState(new Turning_NavState(this, NavigationID.TURNING));
        AddState(new Moving_NavState(this, NavigationID.MOVING));
        AddState(new Paused_NavState(this, NavigationID.PAUSED));
        AddState(new Waypoint_NavState(this, NavigationID.WAYPOINT));
        AddState(new Abort_NavState(this, NavigationID.ABORT));

        SetState(NavigationID.INIT);
    }

    protected override void Awake()
    {
        _eventBroker = EventBroker.GetInstance();
        DestinationMoved = false;
        base.Awake(); //Inits the statemachine    
    }

    protected override void Start()
    {
        //get the VR headset transform
        var rc = GetComponent<RobotController>();

        if (rc)
        {
            _headsetTransform = rc.HeadsetTransform;
        }
        else
        {
            Debug.LogError("-> RobotNavigation::Start() - Can't find RobotController! :(");
        }

        _esp32_communicator = GetComponent<ESP32_Communicator>();

        if (_esp32_communicator == null)
        {
            Debug.LogError("-> RobotNavigation::Start() - _esp32_communicator is null :(");
        }

        base.Start(); //Inits the states
    }


    protected override void Update()
    {
        base.Update();
    }


    public void StopNavigation()
    {
        this.SetState(NavigationID.IDLE);
    }


    public void OnNavMeshSurfaceCreated()
    {
        Debug.Log("-> OnNavMeshSurfaceCreated");

        this.SetState(NavigationID.IDLE);
    }

    /// <summary>
    /// Places the target sphere at the specified position
    /// </summary>
    public void PlaceDestinationMarker(Vector3 position)
    {
        if (_destinationMarker == null) return;

        _destinationMarker.transform.position = position;
        _destinationMarker.SetActive(true);
        _waypointMarker.transform.position = position;
        _waypointMarker.SetActive(true);

        // Path calculation will happen in PathfindingState
        DestinationMoved = true;
    }


    /// <summary>
    /// Calculates the distance from robot to target sphere
    /// </summary>
    public float CalculateDistanceToWaypoint()
    {
        if (_headsetTransform == null || _waypointMarker == null || !_waypointMarker.activeInHierarchy)
            return float.MaxValue;

        //Check if this is assigned by value or by reference
        Vector3 robotPosition = _headsetTransform.position; //remember the headset is mounted on top of the robot
        Vector3 targetPos = _waypointMarker.transform.position;

        // Calculate horizontal distance (ignoring Y difference)
        robotPosition.y = 0;
        targetPos.y = 0;

        return Vector3.Distance(robotPosition, targetPos);
    }

    public float GetRobotYRotationAngle()
    {
        if (_headsetTransform == null) return 0f;

        // Return the Y-axis (yaw) rotation of the headset in degrees
        return _headsetTransform.eulerAngles.y;
    }


    // Nav Mesh Agent Methods

    public float CalculateAngleToWaypoint()
    {
        if (_headsetTransform == null || _waypointMarker == null) return 0f;

        Vector3 robotPosition = _headsetTransform.position;
        Vector3 robotForward = _headsetTransform.forward;
        Vector3 waypointPosition = _waypointMarker.transform.position;
        Vector3 directionToWaypoint = (waypointPosition - robotPosition).normalized;

        // Calculate angle in horizontal plane only
        robotForward.y = 0;
        directionToWaypoint.y = 0;
        robotForward.Normalize();
        directionToWaypoint.Normalize();

        // Calculate signed angle
        float angle = Vector3.SignedAngle(robotForward, directionToWaypoint, Vector3.up);
        return angle;
    }

    public bool TryToSetDestination()
    {
        if (_virtualAgent == null) return false;

        m_waypoints.Clear();

        // CRITICAL: Stop the agent from auto-moving while we calculate the path
        _virtualAgent.isStopped = true;

        PositionAgentOnNavMesh();
        SnapDestinationMarker();

        // Calculate path (agent won't move because isStopped = true)
        return _virtualAgent.SetDestination(_destinationMarker.transform.position);
    }


    private void PositionAgentOnNavMesh()
    {
        // Position virtual agent at headset's/robot's current position
        // but snap to the NavMesh surface Y coordinate
        Vector3 robotPosition = _headsetTransform.position;
        robotPosition.y = 0; //might want to omit if multilevel environment - if everything at floor level should be ok.

        // Find the nearest point on the NavMesh
        if (NavMesh.SamplePosition(robotPosition, out NavMeshHit hit, 2.0f, NavMesh.AllAreas))
        {
            // Use the NavMesh surface position (correct Y value)
            _virtualAgent.transform.position = hit.position;
            Debug.Log($"Agent positioned on NavMesh at Y={hit.position.y:F2}");
        }
        else
        {
            // Fallback: robot might be off NavMesh - use robot position anyway
            _virtualAgent.transform.position = robotPosition;
            Debug.LogWarning("Robot position not on NavMesh - pathfinding may fail!");
        }
    }


    private void SnapDestinationMarker()
    {
        // Snap destination marker to NavMesh as well
        Vector3 destinationPosition = _destinationMarker.transform.position;
        if (NavMesh.SamplePosition(destinationPosition, out NavMeshHit destHit, 2.0f, NavMesh.AllAreas))
        {
            _destinationMarker.transform.position = destHit.position;
            Debug.Log($"Destination snapped to NavMesh at Y={destHit.position.y:F2}");
        }
    }

    public void AssignNavMeshPathToWaypoints()
    {
        // path.corners[0] is typically the current robot position(where the path starts).
        // Starting at index 0 means the robot will try to navigate to where it already is.
        if (_virtualAgent.hasPath && _virtualAgent.path.status == NavMeshPathStatus.PathComplete)
        {
            m_waypoints = _virtualAgent.path.corners.ToList();

            // Skip first waypoint if it's at/near the robot's current position
            if (m_waypoints.Count > 1)
            {
                _currentWaypointIndex = 1; // Start at second waypoint
                Debug.Log($"=> COMPLETE path with {m_waypoints.Count} corners. Starting at waypoint 1.");
            }
            else
            {
                _currentWaypointIndex = 0;
                Debug.Log($"=> COMPLETE Path has only 1 corner (destination)");
            }
        }
        else if (_virtualAgent.path.status == NavMeshPathStatus.PathPartial)
        {
            m_waypoints = _virtualAgent.path.corners.ToList();

            if (m_waypoints.Count > 1)
            {
                _currentWaypointIndex = 1;
                Debug.LogWarning($"=> PARTIAL path with {m_waypoints.Count} corners. May not reach destination!");
            }
            else
            {
                _currentWaypointIndex = 0;
                Debug.LogWarning($"=> PARTIAL path with only 1 corner");
            }
        }
        else
        {
            // no path, so we will set the waypoint array with only 1 element - containing the final destination. 
            m_waypoints.Clear();
            _currentWaypointIndex = 1;

            Vector3 robotPosition = _headsetTransform.position;
            robotPosition.y = 0;

            m_waypoints.Add(robotPosition);                             //element 0
            m_waypoints.Add(_destinationMarker.transform.position);    //element 1

            Debug.Log("=> There is NO path! :(");
        }

        // Update target sphere to first waypoint 
        UpdateCurrentWaypoint();

        VisualizePathWaypoints();


        // DEBUG: Log all waypoints
        for (int i = 0; i < m_waypoints.Count; i++)
        {
            Debug.Log($"  Waypoint [{i}]: {m_waypoints[i]}");
        }
    }

    private void UpdateCurrentWaypoint()
    {
        if (m_waypoints != null && _currentWaypointIndex < m_waypoints.Count)
        {
            // Move waypoint marker to current waypoint
            _waypointMarker.transform.position = m_waypoints[_currentWaypointIndex];
        }
    }


    // Call this in your MovingToTargetState when waypoint is reached
    public bool AdvanceToNextWaypoint()
    {
        _currentWaypointIndex++;

        if (_currentWaypointIndex < m_waypoints.Count)
        {
            UpdateCurrentWaypoint();
            return true; // More waypoints exist
        }

        return false; // Path complete
    }


    private void VisualizePathWaypoints()
    {
        if (_pathLineRenderer == null || m_waypoints == null || m_waypoints.Count == 0)
        {
            if (_pathLineRenderer != null)
                _pathLineRenderer.enabled = false;
            return;
        }

        _pathLineRenderer.positionCount = m_waypoints.Count;
        _pathLineRenderer.SetPositions(m_waypoints.ToArray());
        _pathLineRenderer.enabled = true;

        // Only check agent status if agent has a valid path
        if (_virtualAgent != null && _virtualAgent.hasPath)
        {
            if (_virtualAgent.path.status == NavMeshPathStatus.PathComplete)
            {
                _pathLineRenderer.startColor = Color.green;
                _pathLineRenderer.endColor = Color.green;
            }
            else if (_virtualAgent.path.status == NavMeshPathStatus.PathPartial)
            {
                _pathLineRenderer.startColor = Color.orange;
                _pathLineRenderer.endColor = Color.orange;
            }
        }
        else
        {
            // No path or direct navigation - use red
            _pathLineRenderer.startColor = Color.red;
            _pathLineRenderer.endColor = Color.red;
        }
    }

    public void PlayAudio(string audioId)
    {
        PlayAudioEventArgs args = new PlayAudioEventArgs(audioId);
        _eventBroker.DispatchEvent(EventID.PLAY_AUDIO, args);
    }

    private void OnDestroy()
    {
        _eventBroker = null;
    }

}
