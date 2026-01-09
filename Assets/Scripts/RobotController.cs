using System;
using LudicWorlds;
using Meta.XR.MRUtilityKit;
using UnityEngine;

[RequireComponent(typeof(LocationAnchorManager))]
[RequireComponent(typeof(ESP32_Communicator))]
[RequireComponent(typeof(RobotNavigation))]
[RequireComponent(typeof(MicRecorder))]

public class RobotController : GameObjectStateMachine<RobotStateID>
{
    [Header("Robot Navigation")]
    [SerializeField] private Transform _headsetTransform; // The Quest 3 headset's transform (which is mounted on top of the real robot)

    [Header("Room Awareness")]

    [SerializeField] private LineRenderer _rayLine; // Optional: Line renderer to show raycast
    [SerializeField] private LayerMask _roomLayerMask = -1; // Which layers to raycast against

    [SerializeField] private Transform _rightControllerTransform;
    [SerializeField] private float _rayLength = 5;

    //---

    protected EventBroker _eventBroker;

    private RobotNavigation _robotNavigation;
    private LocationAnchorManager _locationAnchorManager;
    private ESP32_Communicator _esp32_communicator;
    private MicRecorder _micRecorder;

    private Ray _ray;
    private float _stickDeadzone = 0.3f; //the righthand controller's stick is used for direct control of the robot
    private Vector2 _rightStick;
    private bool _wasStickActive = false; // Track if stick was previously used for manual control

    // temp vars -----------
    private string[] _locationLabels = { LocationLabel.FRIDGE,
                                 LocationLabel.SOFA,
                                 LocationLabel.TABLE };

    private int _targetLabelIndex = 0;
    private string _targetLabel = LocationLabel.FRIDGE;

    //----------------------

    // Controller feedback
    private const float HAPTIC_DURATION = 0.1f;

    public RobotNavigation Nav
    {
        get { return _robotNavigation; }
    }

    public Transform HeadsetTransform
    {
        get { return _headsetTransform; }
    }

    public LocationAnchorManager LocAnchorMgr
    {
        get { return _locationAnchorManager; }
    }


    protected override void Awake()
    {
        // Keep display active for robot operation
        Screen.sleepTimeout = SleepTimeout.NeverSleep;
        Application.runInBackground = true;

        InitEvents();

        _robotNavigation = GetComponent<RobotNavigation>();

        if (_robotNavigation == null)
        {
            Debug.LogError("[RobotCtrl] _robotNavigation is NULL :(");
        }


        _locationAnchorManager = GetComponent<LocationAnchorManager>();

        if (_locationAnchorManager == null)
        {
            Debug.LogError("[RobotCtrl] _locationAnchorManager is NULL :(");
        }


        _esp32_communicator = GetComponent<ESP32_Communicator>();

        if (_esp32_communicator == null)
        {
            Debug.LogError("[RobotCtrl] _esp32_communicator is NULL :(");
        }

        _micRecorder = GetComponent<MicRecorder>();
        if (_micRecorder == null)
        {
            Debug.LogError("[RobotCtrl] _micRecorder is NULL :(");
        }

        base.Awake(); // Initialize state machine
    }

    protected override void InitStates()
    {
        base.InitStates();

        AddState(new InitRobotState(this, RobotStateID.INIT));
        AddState(new StationaryState(this, RobotStateID.STATIONARY));
        AddState(new MovingState(this, RobotStateID.MOVING));
        AddState(new AbortState(this, RobotStateID.ABORT));

        SetState(RobotStateID.INIT);
    }

    protected override void Start()
    {
        Debug.Log("[RobotCtrl] Starting...");
        base.Start(); // Initialize states
    }


    protected override void Update()
    {
        base.Update(); // Run state machine update
        RespondToTouchControllerInput();
    }


    private void RespondToTouchControllerInput()
    {
        // Quest 3 Controller Input
        if (OVRInput.GetDown(OVRInput.Button.One)) //A Button - Cycle forward through anchors
        {
            _locationAnchorManager.CycleToNextAnchor();
        }

        if (OVRInput.GetDown(OVRInput.Button.Two)) //B button - Cycle through through anchor labels
        {
            _locationAnchorManager.CycleLabelForward();
        }

        if (OVRInput.GetDown(OVRInput.Button.PrimaryIndexTrigger)) //Left Index Trigger - Place destination marker at selected anchor
        {
            // Place destination Marker at the Selected Anchor
            PlaceDestinationMarkerAtSelectedAnchor();
        }


        if (OVRInput.GetDown(OVRInput.Button.PrimaryHandTrigger)) //Left Grip Trigger - Save all anchors
        {
            _locationAnchorManager.SaveAllSpatialAnchors();
        }

        if (OVRInput.GetDown(OVRInput.Button.SecondaryIndexTrigger)) //Right controller trigger (on press)
        {
            PlaceDestinationMarkerAtRaycastHit();
        }

        if (OVRInput.GetDown(OVRInput.Button.SecondaryHandTrigger)) // Grip trigger - Place or Move anchor
        {
            PlaceLocationAnchorAtRaycastHit();
        }

        if (OVRInput.GetDown(OVRInput.Button.Three)) //X Button (Left Controller) 
        {
            SetState(RobotStateID.ABORT);

            /*
            CycleToNextLocationLabel();

            bool anchorFound = _locationAnchorManager.SelectNearestAnchorWithLabel(_targetLabel, _headsetTransform.position);

            if (anchorFound)
            {
                Debug.Log("[RobotCtrl] Selected an anchor with label: " + _targetLabel + " :)");
            }
            else
            {
                Debug.Log("[RobotCtrl] Can't find an anchor with label: " + _targetLabel + " :(");
            }
            */
        }


        if (OVRInput.GetDown(OVRInput.Button.Four)) //Y Button (Left Controller)
        {
            //Toggle DebugPanel visibility
            DebugPanel.ToggleVisibility();
        }

        // Left controller thumbstick click - Destroy all anchors
        if (OVRInput.GetDown(OVRInput.Button.PrimaryThumbstick))
        {
            _locationAnchorManager.DestroyAllLocationAnchors();
        }

        // Right controller thumbstick click - Emergency Stop
        if (OVRInput.GetDown(OVRInput.Button.SecondaryThumbstick))
        {
            EmergencyStop();
            Debug.Log("[RobotCtrl] Emergency Stop activated via thumbstick click!");
        }


        //-------------------------------------
        // Direct Control
        //-------------------------------------

        // Right controller stick input
        _rightStick = OVRInput.Get(OVRInput.Axis2D.SecondaryThumbstick);

        // Check if stick is currently active (outside deadzone)
        //bool stickActive = Mathf.Abs(_rightStick.x) > _stickDeadzone || Mathf.Abs(_rightStick.y) > _stickDeadzone;

        // Forward/Backward (Y-axis)
        if (_rightStick.y > _stickDeadzone)
        {
            _esp32_communicator.SetCommandGivenAction(RobotAction.Forward);
            DebugPanel.UpdateRobotState("[RobotCtrl] Right Stick - Forward");
            _wasStickActive = true;
            // ProvideHapticFeedback(OVRInput.Controller.LTouch, 0.5f);

        }
        else if (_rightStick.y < -_stickDeadzone)
        {
            _esp32_communicator.SetCommandGivenAction(RobotAction.Backward);
            DebugPanel.UpdateRobotState("[RobotCtrl] Right Stick - Backward");
            _wasStickActive = true;
        }
        // Left/Right turning (X-axis)
        else if (_rightStick.x < -_stickDeadzone)
        {
            _esp32_communicator.SetCommandGivenAction(RobotAction.TurnLeft);
            DebugPanel.UpdateRobotState("[RobotCtrl] Right Stick - Turn Left");
            _wasStickActive = true;
        }
        else if (_rightStick.x > _stickDeadzone)
        {
            _esp32_communicator.SetCommandGivenAction(RobotAction.TurnRight);
            DebugPanel.UpdateRobotState("[RobotCtrl] Right Stick - Turn Right");
            _wasStickActive = true;
        }
        else if (_wasStickActive)
        {
            // Stick just returned to neutral - send stop ONCE, then allow autonomous control
            _esp32_communicator.SetCommandGivenAction(RobotAction.Stop);
            DebugPanel.UpdateRobotState("------");
            _wasStickActive = false;
        }
        // If _wasStickActive is false and stick is neutral, don't send anything - let state machine control robot

        // Debug info - press both grips to show connection status
        /*
        if (OVRInput.Get(OVRInput.Button.PrimaryHandTrigger) &&
            OVRInput.Get(OVRInput.Button.SecondaryHandTrigger))
        {
            Debug.Log($"Connection Status: {(isConnected ? "Connected" : "Not Connected")} to {esp32IP}:{udpPort}");
        }
        */
    }

    public virtual void InitEvents()
    {
        _eventBroker = EventBroker.GetInstance();

        //Create Events
        _eventBroker.CreateEventHandler(EventID.INSTRUCTION_READY);
        _eventBroker.CreateEventHandler(EventID.IDLE_NAV_ENTER);
        _eventBroker.CreateEventHandler(EventID.IDLE_NAV_EXIT);
        _eventBroker.CreateEventHandler(EventID.ENABLE_MIC_RECORDING);
        _eventBroker.CreateEventHandler(EventID.DISABLE_MIC_RECORDING);


        _eventBroker.Events[EventID.INSTRUCTION_READY] += OnInstructionReady;
        _eventBroker.Events[EventID.IDLE_NAV_ENTER] += OnNavIdleEnter;
        _eventBroker.Events[EventID.IDLE_NAV_EXIT] += OnNavIdleExit;
    }


    private void CycleToNextLocationLabel()
    {
        _targetLabelIndex++;

        if (_targetLabelIndex >= _locationLabels.Length)
            _targetLabelIndex = 0;

        _targetLabel = _locationLabels[_targetLabelIndex];
    }

    public void SetCommandGivenAction(RobotAction action)
    {
        _esp32_communicator.SetCommandGivenAction(action);
    }


    public void PlaceLocationAnchorAtRaycastHit()
    {
        bool hasHit = PerformRoomRaycast(out RaycastHit hit);

        if (hasHit)
        {
            Vector3 hitPoint = hit.point;
            Vector3 hitNormal = hit.normal;

            // Create rotation that aligns prefab's local Z-axis (up) with the surface normal
            Quaternion rotation = Quaternion.FromToRotation(Vector3.up, hitNormal);

            _locationAnchorManager.PlaceLocationAnchor(hitPoint, rotation);
        }
    }


    public void PlaceDestinationMarkerAtRaycastHit()
    {
        bool hasHit = PerformRoomRaycast(out RaycastHit hit);

        if (hasHit)
        {
            Vector3 hitPoint = hit.point;
            Vector3 hitNormal = hit.normal;
            _robotNavigation.PlaceDestinationMarker(hit.point);
            //           Debug.Log("hitPoint X:" + hit.point.x + " Y:" + hitPoint.y + " Z: " + hitPoint.z);
        }
    }


    public void PlaceDestinationMarkerAtSelectedAnchor()
    {
        var (anchor, label) = _locationAnchorManager.GetSelectedAnchor();

        if (anchor != null)
        {
            Vector3 anchorPos = anchor.transform.position;

            //If the navigation is in standy-by mode - moving the marker will imediately set off
            //the (state-based) navigation process
            _robotNavigation.PlaceDestinationMarker(anchorPos);

        }
    }


    private void ProvideHapticFeedback(OVRInput.Controller controller, float amplitude)
    {
        // Haptic feedback for button press confirmation
        OVRInput.SetControllerVibration(1f, amplitude, controller);

        // Schedule haptic stop
        Invoke(nameof(StopHaptics), HAPTIC_DURATION);
    }


    private void StopHaptics()
    {
        OVRInput.SetControllerVibration(0f, 0f, OVRInput.Controller.LTouch);
        OVRInput.SetControllerVibration(0f, 0f, OVRInput.Controller.RTouch);
    }


    private bool PerformRoomRaycast(out RaycastHit hit)
    {
//        Debug.Log("PerformRoomRaycast()");

        _ray = new Ray(_rightControllerTransform.position, _rightControllerTransform.forward);

        MRUKRoom room = MRUK.Instance.GetCurrentRoom();

        return room.Raycast(_ray, _rayLength, out hit, out MRUKAnchor anchor);
    }


    private void EmergencyStop()
    {
        _robotNavigation.StopNavigation();
        _esp32_communicator.EmergencyStop();
    }


    protected void OnInstructionReady(object sender, EventArgs e)
    {
        InstructionEventArgs args = e as InstructionEventArgs;

        if (args != null && !string.IsNullOrEmpty(args.Instruction))
        {
            Debug.Log("[RobotCtrl] OnInstructionReady - Instruction: " + args.Instruction);

            _robotNavigation.Instruction = args.Instruction;

            bool anchorFound = _locationAnchorManager.SelectNearestAnchorWithLabel(args.Instruction, _headsetTransform.position
                                );

            if (anchorFound)
            {
                PlaceDestinationMarkerAtSelectedAnchor();
            }
            else
            {
                Debug.LogWarning($"[RobotCtrl] No anchor found with label: {args.Instruction}");
            }
        }
        else
        {
            Debug.Log("[RobotCtrl] OnInstructionReady - I don't understand!");
            _robotNavigation.Instruction = "";

            _eventBroker.DispatchEvent(EventID.PLAY_AUDIO, new PlayAudioEventArgs(AudioID.I_DONT_UNDERSTAND));
        }
    }

    private void OnNavIdleEnter(object sender, EventArgs e)
    {
        Debug.Log("[RobotCtrl] OnNavIdleEnter - Transitioning to STATIONARY");
        SetState(RobotStateID.STATIONARY);
    }

    private void OnNavIdleExit(object sender, EventArgs e)
    {
        Debug.Log("[RobotCtrl] OnNavIdleExit - Transitioning to MOVING");
        SetState(RobotStateID.MOVING);
    }

    private void OnDestroy()
    {
        StopHaptics();

        if (_eventBroker != null)
        {
            _eventBroker.Events[EventID.INSTRUCTION_READY] -= OnInstructionReady;
            _eventBroker.Events[EventID.IDLE_NAV_ENTER] -= OnNavIdleEnter;
            _eventBroker.Events[EventID.IDLE_NAV_EXIT] -= OnNavIdleExit;
        }
    }
}
