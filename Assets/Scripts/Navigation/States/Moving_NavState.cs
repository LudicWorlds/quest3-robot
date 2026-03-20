using LudicWorlds;
using UnityEngine;

public class Moving_NavState : NavState
{
    private EventBroker _eventBroker;

    public Moving_NavState(IStateMachine<NavigationID> stateMachine, NavigationID id) : base(stateMachine, id)
    {
        _eventBroker = EventBroker.GetInstance();
    }

    public override void Enter()
    {
        Debug.Log("-> Moving_NavState::Enter()");
        base.Enter();
        _nextStateId = NavigationID.PAUSED;

        _ctrl.PlayAudio(AudioID.MOVING);

        _ctrl.IsTurning = false;

        //Move Forward
        _ctrl.Com.SetCommandGivenAction(RobotAction.Forward);
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

        // Check if waypoint reached
        if (IsWaypointReached())
        {
            _stateMachine.SetState(NavigationID.WAYPOINT);
            return;
        }

        // Calculate angle and distance
        _angleToWaypoint = _ctrl.CalculateAngleToWaypoint();
        _distanceToWaypoint = _ctrl.CalculateDistanceToWaypoint();

        // Check if we've gone off course significantly
        if (Mathf.Abs(_angleToWaypoint) > OFF_COURSE_ANGLE)
        {
            Debug.Log($"[MovingAction] Off course - Angle: {_angleToWaypoint:F1}° - pausing to re-evaluate");
            _stateMachine.SetState(NavigationID.PAUSED);
            return;
        }

        DebugPanel.UpdateNavState($"Moving forward - Dist: {_distanceToWaypoint:F2}m");
    }

    public override void Exit()
    {
        Debug.Log("-> Moving_NavState::Exit()");
        _ctrl.Com.SetCommandGivenAction(RobotAction.Stop);

        if (_stateMachine.NextState.ID == NavigationID.ABORT)
        {
            _ctrl.DebugInfo = "- Moving_NavState\n";
            _ctrl.DebugInfo += $"- _angleToWaypoint: {_angleToWaypoint}\n";
            _ctrl.DebugInfo += $"- _distanceToWaypoint: {_distanceToWaypoint}\n";
        }
    }

    public override void Dispose()
    {
        _eventBroker = null;
        base.Dispose();
    }
}
