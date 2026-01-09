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

    private float _turnIncrement = TURN_INCREMENT;

    public Turning_NavState(IStateMachine<NavigationID> stateMachine, NavigationID id) : base(stateMachine, id)
    {
    }

    public override void Enter()
    {
        Debug.Log("-> Turning_NavState::Enter()");
        base.Enter();
        _elapsedTime = 0f;
        _direction = 0; //no direction assigned
        _nextStateId = NavigationID.PAUSED;

        // Record starting Y rotation of headset/robot for rotation tracking
        _startingAngle = _ctrl.GetRobotYRotationAngle();

        // Initialize stuck detection
        _lastStuckCheckTime = 0f;
        _lastStuckCheckAngle = _startingAngle;

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
        Debug.Log("-> Turning_NavState::Exit()");
        _ctrl.Com.SetCommandGivenAction(RobotAction.Stop);

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
    }


    private void HandleTurning()
    {
        // Update turn time (for debugging/tracking)
        _elapsedTime += Time.deltaTime;

        // Calculate angle to waypoint
        _angleToWaypoint = _ctrl.CalculateAngleToWaypoint();

        // Use absolute value for progressive turn increment logic
        float absAngle = Mathf.Abs(_angleToWaypoint);



        if (absAngle < 15f)
        {
            _turnIncrement = 5f;
        }
        else if (absAngle < 25f)
        {
            _turnIncrement = 10f;
        }
        else if (absAngle < 35f)
        {
            _turnIncrement = 15f;
        }
        else
        {
            _turnIncrement = 20f;
        }



            // Calculate how many degrees we've rotated since entering this state
            float currentAngle = _ctrl.GetRobotYRotationAngle();
        float degreesRotated = Mathf.Abs(Mathf.DeltaAngle(_startingAngle, currentAngle));

        StuckDetection(currentAngle);

        // Check if we've rotated by TURN_INCREMENT degrees - pause to re-evaluate
        if (degreesRotated >= _turnIncrement)
        {
            Debug.Log($"[TurningAction] Rotated {degreesRotated:F1}° (target: {_turnIncrement}°) - pausing to re-evaluate");
            _stateMachine.SetState(NavigationID.PAUSED);
            return;
        }

        //-----------------
        //I want to turn in small increments, with 'pauses' in between (to prevent overshooting)
        //Hence, we exit after rotating TURN_INCREMENT degrees, then pause to re-evaluate
        //We also check if we're aligned with TargetTurnAccuracy for early exit
        //-----------------

        // Check if we are within our current TargetTurnAccuracy
        if (Mathf.Abs(_angleToWaypoint) <= FINE_TURN_ACCURACY)
        {
            Debug.Log($"[TurningState] Aligned after {_elapsedTime:F2}s of turning");
            _elapsedTime = 0f;
            _stateMachine.SetState(NavigationID.PAUSED);
        }
        else
        { //We are NOT yet within our current TargetTurnAccuracy - keep turning
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
                    _stateMachine.SetState(NavigationID.PAUSED);
                    return;
                }

                DebugPanel.UpdateNavState($"Turning right - Angle: {_angleToWaypoint:F1}� Rotated: {degreesRotated:F1}°/{_turnIncrement}°");
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
                    _stateMachine.SetState(NavigationID.PAUSED);
                    return;
                }

                DebugPanel.UpdateNavState($"Turning left - Angle: {_angleToWaypoint:F1}� Rotated: {degreesRotated:F1}°/{_turnIncrement}°");
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
                Debug.Log($"[TurningState] Stuck detected! Rotated only {rotationSinceLastCheck:F2}° in {STUCK_CHECK_INTERVAL}s - resending command");
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
