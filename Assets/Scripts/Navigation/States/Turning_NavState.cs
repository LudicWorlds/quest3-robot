using LudicWorlds;
using UnityEngine;

public class Turning_NavState : NavState
{
    private int _direction = 0; //0 = unassigned, 1 = clockwise (right), -1 = anticlickwise (left)
    private float _startingAngle = 0f; // Robot's Y rotation angle when we entered this state

    // Stuck detection
    private const float STUCK_CHECK_INTERVAL = 2.0f;
    private const float MIN_ROTATION_CHANGE = 1f; // Minimum degrees of rotation expected in interval
    private float _lastStuckCheckTime = 0f;
    private float _lastStuckCheckAngle = 0f;

    // Rotational velocity tracking for anticipatory braking
    private const float VELOCITY_SMOOTHING_TIME = 0.1f; // seconds - the headset's frame-to-frame yaw is noisy
    private float _prevAngle = 0f;
    private float _rotationalVelocity = 0f; // degrees per second

    public Turning_NavState(IStateMachine<NavigationID> stateMachine, NavigationID id) : base(stateMachine, id)
    {
    }

    public override void Enter()
    {
 //--       Debug.Log("-> Turning_NavState::Enter()");
        base.Enter();
        _elapsedTime = 0f;
        _direction = 0; //no direction assigned
        _nextStateId = NavigationID.PAUSING;

        // Record starting Y rotation of headset/robot for rotation tracking
        _startingAngle = _ctrl.GetRobotYRotationAngle();

        // Initialize stuck detection
        _lastStuckCheckTime = 0f;
        _lastStuckCheckAngle = _startingAngle;

        // Initialize rotational velocity tracking
        _prevAngle = _startingAngle;
        _rotationalVelocity = 0f;

        _ctrl.IsTurning = true;
    }

    public override void Update()
    {
        if (_ctrl.DestinationMoved)
        {
            // Destination marker moved - recalculate path
            _ctrl.DestinationMoved = false;
            _stateMachine.SetState(NavigationID.PATHFINDING);
            return;
        }

        // Check if waypoint reached (in case we were very close)
        if (IsWaypointReached())
        {
            _stateMachine.SetState(NavigationID.WAYPOINT);
            return;
        }

        HandleTurning();
    }


    public override void Exit()
    {
//--        Debug.Log("-> Turning_NavState::Exit()");
        _ctrl.Com.SetCommandGivenAction(RobotAction.Stop);

        // A normal stop: measure how far the robot keeps rotating after it, so the next turn
        // stops in the right place. Pausing_NavState completes the measurement once settled.
        if (_stateMachine.NextState.ID == NavigationID.PAUSING)
        {
            _ctrl.BeginTurnStopMeasurement(_ctrl.GetRobotYRotationAngle(), _rotationalVelocity);
        }


        /*
        //if (_stateMachine.NextState.ID == NavigationID.ABORT)
       //{
            float currentAngle = _ctrl.GetRobotYRotationAngle();
            float degreesRotated = Mathf.Abs(Mathf.DeltaAngle(_startingAngle, currentAngle));
            _ctrl.DebugInfo = "- Turning_NavState\n";
            _ctrl.DebugInfo += $"- _angleToWaypoint: {_angleToWaypoint}\n";
            _ctrl.DebugInfo += $"- _startingAngle: {_startingAngle:F1}°\n";
            _ctrl.DebugInfo += $"- currentAngle: {currentAngle:F1}°\n";
            _ctrl.DebugInfo += $"- degreesRotated: {degreesRotated:F1}°\n";
            _ctrl.DebugInfo += $"- _elapsedTime: {_elapsedTime}\n\n";
        //}
        */
    }


    private void HandleTurning()
    {
        // Update turn time (for debugging/tracking)
        _elapsedTime += Time.deltaTime;

        // Calculate angle to waypoint
        _angleToWaypoint = _ctrl.CalculateAngleToWaypoint();

        float currentAngle = _ctrl.GetRobotYRotationAngle();

        // Track rotational velocity (degrees per second), smoothed over ~VELOCITY_SMOOTHING_TIME
        if (Time.deltaTime > 0f)
        {
            float deltaAngle = Mathf.Abs(Mathf.DeltaAngle(_prevAngle, currentAngle));
            float frameVelocity = deltaAngle / Time.deltaTime;
            float blend = 1f - Mathf.Exp(-Time.deltaTime / VELOCITY_SMOOTHING_TIME);
            _rotationalVelocity = Mathf.Lerp(_rotationalVelocity, frameVelocity, blend);
            _prevAngle = currentAngle;
        }

        StuckDetection(currentAngle);

        //-----------------
        //Turn in one go: keep turning until the robot would come to rest on the target if we
        //stopped now. The stopping angle is the current velocity x the stopping time that
        //RobotNavigator learns from previous turns (see BeginTurnStopMeasurement).
        //Pausing_NavState then lets the robot settle and Deciding_NavState re-evaluates, which
        //gives a small correction turn if we're still outside FINE_TURN_ACCURACY.
        //-----------------
        float predictedStopAngle = _rotationalVelocity * _ctrl.TurnStopTime;

        if (Mathf.Abs(_angleToWaypoint) <= predictedStopAngle)
        {
//--            Debug.Log($"[TurningState] Stopping {_angleToWaypoint:F1} deg short after {_elapsedTime:F2}s (velocity: {_rotationalVelocity:F1} deg/s)");
            _elapsedTime = 0f;
            _stateMachine.SetState(NavigationID.PAUSING);
        }
        else
        { //Not there yet - keep turning
            if (_angleToWaypoint > 0)
            {//Try to turn right

                if(_direction == 0)
                {
                    _direction = 1;
                    //_ctrl.PlayAudio(AudioID.RIGHT);
                    _ctrl.Com.SetCommandGivenAction(RobotAction.TurnRight);
                }
                else if(_direction == -1)
                {
                    //Robot was turning left, which means we need to change direction (possible overshoot)
                    //Let's pause first!
                    _stateMachine.SetState(NavigationID.PAUSING);
                    return;
                }

//--                DebugPanel.UpdateNavState($"Turning right - Angle: {_angleToWaypoint:F1} deg, stop at: {predictedStopAngle:F1} deg");
            }
            else
            {//Try to turn left

                if (_direction == 0)
                {
                    _direction = -1;
                    //_ctrl.PlayAudio(AudioID.LEFT);
                    _ctrl.Com.SetCommandGivenAction(RobotAction.TurnLeft);
                }
                else if (_direction == 1)
                {
                    //Robot was turning right, which means we need to change direction (possible overshoot)
                    //Let's pause first!
                    _stateMachine.SetState(NavigationID.PAUSING);
                    return;
                }

//--                DebugPanel.UpdateNavState($"Turning left - Angle: {_angleToWaypoint:F1} deg, stop at: {predictedStopAngle:F1} deg");
            }
        }
    }

    private void StuckDetection(float currentAngle)
    {
        // Stuck detection: Check if robot is actually rotating
        if (_elapsedTime - _lastStuckCheckTime >= STUCK_CHECK_INTERVAL)
        {
            float rotationSinceLastCheck = Mathf.Abs(Mathf.DeltaAngle(_lastStuckCheckAngle, currentAngle));

            if (rotationSinceLastCheck < MIN_ROTATION_CHANGE && _direction != 0)
            {
                // Robot appears stuck - resend the turn command
//--                Debug.Log($"[TurningState] Stuck detected! Rotated only {rotationSinceLastCheck:F2}° in {STUCK_CHECK_INTERVAL}s - resending command");
                _ctrl.PlayAudio(AudioID.STUCK);

                if (_direction == 1)
                    _ctrl.Com.SetCommandGivenAction(RobotAction.TurnRight);
                else if (_direction == -1)
                    _ctrl.Com.SetCommandGivenAction(RobotAction.TurnLeft);
            }

            // Update stuck check tracking
            _lastStuckCheckTime = _elapsedTime;
            _lastStuckCheckAngle = currentAngle;
        }
    }



    public override void Dispose()
    {
        base.Dispose();
    }
}
