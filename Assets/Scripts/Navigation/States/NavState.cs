using LudicWorlds;
using UnityEngine;

public class NavState : GameObjectState<NavigationID>
{ //Navigation State

    public const float COARSE_TURN_ACCURACY = 37.5f; //Start off wide....
    public const float FINE_TURN_ACCURACY = 8f; //Then, narrow down to this.
    public const float OFF_COURSE_ANGLE = 20f;

    public const float TURN_INCREMENT = 10f;

    public const float WAYPOINT_RADIUS = 0.10f;
    public const float MOVEMENT_THRESHOLD = 0.15f;

    protected RobotNavigation _ctrl;
    protected int _stage = 0;

    protected float _angleToWaypoint = 0f;
    protected float _distanceToWaypoint = 0f;

    protected float _elapsedTime = 0f;
    protected float _waitDuration = 1f;

    protected NavigationID _nextStateId;

    private EventBroker _eventBroker;

    public NavState(IStateMachine<NavigationID> stateMachine, NavigationID id) : base(stateMachine, id)
    {
        _eventBroker = EventBroker.GetInstance();
        _ctrl = stateMachine as RobotNavigation;
        _stage = 0;
    }

    public override void Enter() { }

    public override void Update() { }

    public override void Exit() { }

    protected bool IsWaypointReached()
    {
        _distanceToWaypoint = _ctrl.CalculateDistanceToWaypoint();

        // Check if we've reached the current waypoint
        if (_distanceToWaypoint <= WAYPOINT_RADIUS)
        {
            DebugPanel.UpdateNavState("Waypoint reached!");
            return true;
        }

        return false;
    }

    protected bool IsWaitOver()
    {
        _elapsedTime += Time.deltaTime;
        return _elapsedTime > _waitDuration;
    }

    public override void Dispose()
    {
        _ctrl = null;
        base.Dispose();
    }
}