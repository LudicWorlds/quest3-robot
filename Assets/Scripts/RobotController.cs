using System;
using LudicWorlds;
using Meta.XR.MRUtilityKit;
using UnityEngine;

[RequireComponent(typeof(LocationAnchorManager))]
[RequireComponent(typeof(ESP32_Communicator))]
[RequireComponent(typeof(RobotNavigator))]
[RequireComponent(typeof(MicRecorder))]

public class RobotController : GameObjectStateMachine<RobotStateID>
{
    [Header("Robot Navigator")]
    [SerializeField] private Transform _headsetTransform; // The Quest 3 headset's transform (which is mounted on top of the real robot)

    [Header("Room Awareness")]

    [SerializeField] private LineRenderer _rightRayLine; // Optional: aim ray for the right controller (destinations)
    [SerializeField] private LineRenderer _leftRayLine; // Optional: aim ray for the left controller (anchor placement)
    [SerializeField] private LayerMask _roomLayerMask = -1; // Which layers to raycast against

    [SerializeField] private Transform _rightControllerTransform;
    [SerializeField] private Transform _leftControllerTransform;
    [SerializeField] private float _rayLength = 5;

    //---

    protected EventBroker _eventBroker;

    private RobotNavigator _robotNavigator;
    private LocationAnchorManager _locationAnchorManager;
    private ESP32_Communicator _esp32_communicator;
    private MicRecorder _micRecorder;

    private Ray _ray;
    private float _stickDeadzone = 0.3f; //the righthand controller's stick is used for direct control of the robot
    private Vector2 _rightStick;
    private bool _wasStickActive = false; // Track if stick was previously used for manual control

    private const float STICK_FLICK_THRESHOLD = 0.7f; // Left stick deflection that registers as a 'flick'
    private Vector2 _leftStick;
    private bool _leftStickArmed = true; // False once a flick has fired, until the stick returns to the deadzone

    // temp vars -----------
    private string[] _locationLabels = { LocationLabel.FRIDGE,
                                 LocationLabel.SOFA,
                                 LocationLabel.TABLE };

    private int _targetLabelIndex = 0;
    private string _targetLabel = LocationLabel.FRIDGE;

    //----------------------

    // Controller feedback
    private const float HAPTIC_DURATION = 0.1f;
    private const float HAPTIC_LIGHT = 0.3f;  // selection / label change / toggles
    private const float HAPTIC_MEDIUM = 0.6f; // place anchor / set destination
    private const float HAPTIC_STRONG = 1.0f; // stop / destroy

    public RobotNavigator Nav
    {
        get { return _robotNavigator; }
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

        _robotNavigator = GetComponent<RobotNavigator>();

        if (_robotNavigator == null)
        {
            Debug.LogError("[RobotCtrl] _robotNavigator is NULL :(");
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
        DrawAimRay(_rightRayLine, _rightControllerTransform);
        DrawAimRay(_leftRayLine, _leftControllerTransform);
    }


    // Layout: right hand = drive & destinations, left hand = anchor setup & safety.
    // Each controller aims with its own ray. Keep the "Touch Controller Reference" section of README.md in sync with this method.
    private void RespondToTouchControllerInput()
    {
        //-------------------------------------
        // Right controller - drive & destinations
        //-------------------------------------

        if (OVRInput.GetDown(OVRInput.Button.SecondaryIndexTrigger)) // Right Index Trigger - Set destination where the right controller points (robot starts moving)
        {
            PlaceDestinationMarkerAtRaycastHit();
            ProvideHapticFeedback(OVRInput.Controller.RTouch, HAPTIC_MEDIUM);
        }

        if (OVRInput.GetDown(OVRInput.Button.SecondaryHandTrigger)) // Right Grip - Go to the selected anchor
        {
            PlaceDestinationMarkerAtSelectedAnchor();
            ProvideHapticFeedback(OVRInput.Controller.RTouch, HAPTIC_MEDIUM);
        }

        if (OVRInput.GetDown(OVRInput.Button.Two)) // B Button - Toggle DebugPanel visibility
        {
            DebugPanel.ToggleVisibility();
            ProvideHapticFeedback(OVRInput.Controller.RTouch, HAPTIC_LIGHT);
        }

        //-------------------------------------
        // Left controller - anchor setup & safety
        //-------------------------------------

        if (OVRInput.GetDown(OVRInput.Button.PrimaryIndexTrigger)) // Left Index Trigger - Place new anchor, or move the selected one, where the left controller points
        {
            PlaceLocationAnchorAtRaycastHit();
            ProvideHapticFeedback(OVRInput.Controller.LTouch, HAPTIC_MEDIUM);
        }

        if (OVRInput.GetDown(OVRInput.Button.Three)) // X Button - STOP (halts motors, returns to STATIONARY with the mic re-armed)
        {
            EmergencyStop();
            ProvideHapticFeedback(OVRInput.Controller.LTouch, HAPTIC_STRONG);
            Debug.Log("[RobotCtrl] STOP activated via X button");
        }

        if (OVRInput.GetDown(OVRInput.Button.Four)) // Y Button - Save all anchors
        {
            _locationAnchorManager.SaveAllSpatialAnchors();
            ProvideHapticFeedback(OVRInput.Controller.LTouch, HAPTIC_LIGHT);
        }

        // Left Grip (held) + Left Thumbstick click - Destroy all anchors.
        // Deliberate two-input chord: this permanently erases the anchors from headset storage.
        if (OVRInput.Get(OVRInput.Button.PrimaryHandTrigger) && OVRInput.GetDown(OVRInput.Button.PrimaryThumbstick))
        {
            _locationAnchorManager.DestroyAllLocationAnchors();
            ProvideHapticFeedback(OVRInput.Controller.LTouch, HAPTIC_STRONG);
            Debug.Log("[RobotCtrl] Destroyed all anchors via Left Grip + Left Thumbstick click");
        }

        // Left Thumbstick flick - left/right selects previous/next anchor, up/down cycles the selected anchor's label
        RespondToLeftStickFlick();


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


    // Edge-triggered: a flick fires once when the stick passes STICK_FLICK_THRESHOLD and
    // re-arms only after it returns inside the deadzone, so holding the stick does not repeat.
    private void RespondToLeftStickFlick()
    {
        _leftStick = OVRInput.Get(OVRInput.Axis2D.PrimaryThumbstick);
        float magnitude = _leftStick.magnitude;

        if (!_leftStickArmed)
        {
            if (magnitude < _stickDeadzone)
                _leftStickArmed = true;

            return;
        }

        if (magnitude < STICK_FLICK_THRESHOLD)
            return;

        _leftStickArmed = false;

        // Dominant axis decides: horizontal = anchor selection, vertical = label
        if (Mathf.Abs(_leftStick.x) > Mathf.Abs(_leftStick.y))
        {
            if (_leftStick.x > 0)
                _locationAnchorManager.CycleToNextAnchor();
            else
                _locationAnchorManager.CycleToPreviousAnchor();
        }
        else
        {
            if (_leftStick.y > 0)
                _locationAnchorManager.CycleLabelForward();
            else
                _locationAnchorManager.CycleLabelBackward();
        }

        ProvideHapticFeedback(OVRInput.Controller.LTouch, HAPTIC_LIGHT);
    }


    // Shows where a controller is pointing: the line ends on the room surface it hits,
    // or at _rayLength if nothing is hit. Skipped when the line or transform is unassigned in the scene.
    private void DrawAimRay(LineRenderer line, Transform controller)
    {
        if (line == null || controller == null)
            return;

        Vector3 origin = controller.position;
        Vector3 end = PerformRoomRaycast(controller, out RaycastHit hit) ? hit.point : origin + controller.forward * _rayLength;

        line.positionCount = 2;
        line.SetPosition(0, origin);
        line.SetPosition(1, end);
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
        _eventBroker.CreateEventHandler(EventID.OBSTRUCTION_DETECTED);
        _eventBroker.CreateEventHandler(EventID.OBSTRUCTION_CLEARED);


        _eventBroker.Events[EventID.INSTRUCTION_READY] += OnInstructionReady;
        _eventBroker.Events[EventID.IDLE_NAV_ENTER] += OnNavIdleEnter;
        _eventBroker.Events[EventID.IDLE_NAV_EXIT] += OnNavIdleExit;
        _eventBroker.Events[EventID.OBSTRUCTION_DETECTED] += OnObstructionDetected;
        _eventBroker.Events[EventID.OBSTRUCTION_CLEARED] += OnObstructionCleared;
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
        bool hasHit = PerformRoomRaycast(_leftControllerTransform, out RaycastHit hit);

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
        bool hasHit = PerformRoomRaycast(_rightControllerTransform, out RaycastHit hit);

        if (hasHit)
        {
            Vector3 hitPoint = hit.point;
            Vector3 hitNormal = hit.normal;
            _robotNavigator.PlaceDestinationMarker(hit.point);
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
            _robotNavigator.PlaceDestinationMarker(anchorPos);

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


    private bool PerformRoomRaycast(Transform controller, out RaycastHit hit)
    {
        hit = default;

        // Called every frame by DrawAimRay, so it must be safe before the MRUK room has loaded
        if (controller == null || MRUK.Instance == null)
            return false;

        MRUKRoom room = MRUK.Instance.GetCurrentRoom();

        if (room == null)
            return false;

        _ray = new Ray(controller.position, controller.forward);

        return room.Raycast(_ray, _rayLength, out hit, out MRUKAnchor anchor);
    }


    private void EmergencyStop()
    {
        _robotNavigator.StopNavigation();
        _esp32_communicator.EmergencyStop();
    }


    protected void OnInstructionReady(object sender, EventArgs e)
    {
        InstructionEventArgs args = e as InstructionEventArgs;

        if (args != null && !string.IsNullOrEmpty(args.Instruction))
        {
            Debug.Log("[RobotCtrl] OnInstructionReady - Instruction: " + args.Instruction);

            _robotNavigator.Instruction = args.Instruction;

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
            _robotNavigator.Instruction = "";

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

    private void OnObstructionDetected(object sender, EventArgs e)
    {
        Debug.Log("[RobotCtrl] OnObstructionDetected");
    }

    private void OnObstructionCleared(object sender, EventArgs e)
    {
        Debug.Log("[RobotCtrl] OnObstructionCleared");
    }


    protected override void OnDestroy()
    {
        StopHaptics();

        if (_eventBroker != null)
        {
            _eventBroker.Events[EventID.INSTRUCTION_READY] -= OnInstructionReady;
            _eventBroker.Events[EventID.IDLE_NAV_ENTER] -= OnNavIdleEnter;
            _eventBroker.Events[EventID.IDLE_NAV_EXIT] -= OnNavIdleExit;

            _eventBroker.Events[EventID.OBSTRUCTION_DETECTED] -= OnObstructionDetected;
            _eventBroker.Events[EventID.OBSTRUCTION_CLEARED] -= OnObstructionCleared;
        }

        base.OnDestroy();
    }
}
