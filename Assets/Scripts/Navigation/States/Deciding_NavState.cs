using LudicWorlds;
using UnityEngine;

public class Deciding_NavState : NavState
{
    private EventBroker _eventBroker;

    public Deciding_NavState(IStateMachine<NavigationID> stateMachine, NavigationID id) : base(stateMachine, id)
    {
        _eventBroker = EventBroker.GetInstance();
    }

    public override void Enter()
    {
        Debug.Log("-> Deciding_NavState::Enter()");
        base.Enter();
        _stage = 0;
        _elapsedTime = 0f;
        _nextStateId = NavigationID.DECIDING;
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

        DecideNextAction();
    }

    private void DecideNextAction()
    {
        // Calculate current angle and distance to waypoint
        _angleToWaypoint = _ctrl.CalculateAngleToWaypoint();
        _distanceToWaypoint = _ctrl.CalculateDistanceToWaypoint();

        DebugPanel.UpdateNavState($"Deciding - Angle: {_angleToWaypoint:F1}° Dist: {_distanceToWaypoint:F2}m");

        //OK, we let's round down (reduce) the TargetTurnAccuracy to the nearest multiple of 10
        //_ctrl.TargetTurnAccuracy = _angleToWaypoint - _angleToWaypoint % 10;

        //if (_ctrl.TargetTurnAccuracy < FINE_TURN_ACCURACY)
        //    _ctrl.TargetTurnAccuracy = FINE_TURN_ACCURACY;


        //Decision Logic
        if (Mathf.Abs(_angleToWaypoint) <= FINE_TURN_ACCURACY)
        {
            //We are more-or-less aligned/on-target, move towards the target...
            Debug.Log($"[Deciding_NavState] Aligned - moving forward");
            _stateMachine.SetState(NavigationID.MOVING);
        }
        else
        {
            // Need to turn
            Debug.Log($"[Deciding_NavState] Need to turn - Angle: {_angleToWaypoint:F1}°");
            _stateMachine.SetState(NavigationID.TURNING);
        }
    }


    public override void Exit()
    {
        Debug.Log("-> Deciding_NavState::Exit()");
    }

    public override void Dispose()
    {
        _eventBroker = null;
        base.Dispose();
    }
}
