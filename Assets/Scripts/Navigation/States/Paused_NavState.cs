using LudicWorlds;
using UnityEngine;

public class Paused_NavState : NavState
{
    private EventBroker _eventBroker;
    private const float CHECK_INTERVAL = 0.25f; // Check rotation every 0.25 seconds
    private const float MIN_ROTATION_CHANGE = 1f; // Robot must rotate less than 1 degree to be settled
    private const float ADDITIONAL_WAIT = 0.25f; // Wait an additional 0.25 seconds after settled

    private float _lastCheckTime = 0f;
    private float _lastCheckAngle = 0f;
    private bool _isSettled = false;
    private float _settledTime = 0f;

    public Paused_NavState(IStateMachine<NavigationID> stateMachine, NavigationID id) : base(stateMachine, id)
    {
        _eventBroker = EventBroker.GetInstance();
    }

    public override void Enter()
    {
        Debug.Log("-> Paused_NavState::Enter()");
        base.Enter();
        _elapsedTime = 0f;
        _nextStateId = NavigationID.DECIDING;

        // Initialize settling detection
        _lastCheckTime = 0f;
        _lastCheckAngle = _ctrl.GetRobotYRotationAngle();
        _isSettled = false;
        _settledTime = 0f;

       //PlayAudioEventArgs args = new PlayAudioEventArgs(AudioID.PAUSE);
        //_eventBroker.DispatchEvent(EventID.PLAY_AUDIO, args);

        // Stop all motors
        _ctrl.Com.SetCommandGivenAction(RobotAction.Stop);
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

        // Check if waypoint reached (in case we reached it during pause)
        if (IsWaypointReached())
        {
            _stateMachine.SetState(NavigationID.WAYPOINT);
            return;
        }

        _elapsedTime += Time.deltaTime;

        // Check if it's time to measure rotation
        if (_elapsedTime - _lastCheckTime >= CHECK_INTERVAL)
        {
            float currentAngle = _ctrl.GetRobotYRotationAngle();
            float rotationSinceLastCheck = Mathf.Abs(Mathf.DeltaAngle(_lastCheckAngle, currentAngle));

            if (rotationSinceLastCheck < MIN_ROTATION_CHANGE)
            {
                // Robot has settled (rotating less than 1 degree)
                if (!_isSettled)
                {
                    _isSettled = true;
                    _settledTime = _elapsedTime;
                    Debug.Log($"[Paused_NavState] Robot settled (rotated only {rotationSinceLastCheck:F2}° in {CHECK_INTERVAL}s)");
                }
            }
            else
            {
                // Robot still moving - reset settled state
                _isSettled = false;
                Debug.Log($"[Paused_NavState] Robot still settling (rotated {rotationSinceLastCheck:F2}° in {CHECK_INTERVAL}s)");
            }

            // Update tracking variables
            _lastCheckTime = _elapsedTime;
            _lastCheckAngle = currentAngle;
        }

        // Update debug display
        if (_isSettled)
        {
            float waitRemaining = ADDITIONAL_WAIT - (_elapsedTime - _settledTime);
            DebugPanel.UpdateNavState($"Paused - settled, waiting {waitRemaining:F2}s");
        }
        else
        {
            DebugPanel.UpdateNavState($"Paused - settling ({_elapsedTime:F2}s)");
        }

        // Transition after additional wait period
        if (_isSettled && (_elapsedTime - _settledTime >= ADDITIONAL_WAIT))
        {
            Debug.Log($"[Paused_NavState] Settling complete after {_elapsedTime:F2}s - transitioning to DECIDING");
            _stateMachine.SetState(NavigationID.DECIDING);
        }
    }

    public override void Exit()
    {
        Debug.Log("-> Paused_NavState::Exit()");
    }

    public override void Dispose()
    {
        _eventBroker = null;
        base.Dispose();
    }
}
