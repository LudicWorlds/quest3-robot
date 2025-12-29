using LudicWorlds;
using UnityEngine;

public class Waypoint_NavState : NavState
{
    private EventBroker _eventBroker;

    public Waypoint_NavState(IStateMachine<NavigationID> stateMachine, NavigationID id) : base(stateMachine, id)
    {
        _eventBroker = EventBroker.GetInstance();
    }

    public override void Enter()
    {
        Debug.Log("-> Waypoint_NavState::Enter()");
        base.Enter();
        _stage = 0;
        _elapsedTime = 0f;
        _nextStateId = NavigationID.DECIDING;

        // Stop all motors
        _ctrl.Com.SetCommandGivenAction(RobotAction.Stop);
    }

    public override void Update()
    {
        if (_ctrl.DestinationMoved)
        { // The Target Sphere has just been placed at a new position
            _ctrl.DestinationMoved = false;
            _stateMachine.SetState(NavigationID.PATHFINDING);
            return;
        }

        // Wait for pause duration
        _elapsedTime += Time.deltaTime;
      
        if (_elapsedTime >= 0.5f)
        {
            DetermineNextStateOnReachingWaypoint();
        }
    }

    private void DetermineNextStateOnReachingWaypoint()
    {
        _ctrl.Com.SetCommandGivenAction(RobotAction.Stop);

        if (!_ctrl.AdvanceToNextWaypoint())
        {
            // Final destination reached
            PlayAudioEventArgs args = new PlayAudioEventArgs(AudioID.DESTINATION_REACHED);
            _eventBroker.DispatchEvent(EventID.PLAY_AUDIO, args);

            _stateMachine.SetState(NavigationID.IDLE);
        }
        else
        {
            // Go to next waypoint - decide what action to take
            PlayAudioEventArgs args = new PlayAudioEventArgs(AudioID.WAYPOINT);
            _eventBroker.DispatchEvent(EventID.PLAY_AUDIO, args);

            _stateMachine.SetState(NavigationID.DECIDING);
        }
    }

    public override void Exit()
    {
        Debug.Log("-> Waypoint_NavState::Exit()");
    }

    public override void Dispose()
    {
        _eventBroker = null;
        base.Dispose();
    }
}
