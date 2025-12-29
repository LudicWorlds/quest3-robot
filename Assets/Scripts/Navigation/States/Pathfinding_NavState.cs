using LudicWorlds;
using UnityEngine;

public class Pathfinding_NavState : NavState
{
    //private EventBroker _eventBroker;
    private const float PATHFINDING_WAIT_TIME = 0.5f; // Wait 0.5 seconds for path calculation

    public Pathfinding_NavState(IStateMachine<NavigationID> stateMachine, NavigationID id) : base(stateMachine, id)
    {
        //_eventBroker = EventBroker.GetInstance();
    }

    public override void Enter()
    {
        Debug.Log("-> Pathfinding_NavState::Enter()");
        base.Enter();
        _stage = 0;
        _elapsedTime = 0f;
        _waitDuration = PATHFINDING_WAIT_TIME;
        _nextStateId = NavigationID.DECIDING;
        _ctrl.TargetTurnAccuracy = COARSE_TURN_ACCURACY;

        if (_ctrl.TryToSetDestination())
        {
            Debug.Log("=> Destination is set! :)");
        }
        else
        {
            Debug.Log("=> Destination NOT set! :(");
            _stateMachine.SetState(NavigationID.IDLE);
        }

        //PlayAudioEventArgs args = new PlayAudioEventArgs(AudioID.PATHFINDING);
        //_eventBroker.DispatchEvent(EventID.PLAY_AUDIO, args);

        //Make sure that the robot is stopped during pathfinding
        _ctrl.Com.SetCommandGivenAction(RobotAction.Stop);
    }

    public override void Update()
    {
        _elapsedTime += Time.deltaTime;

        switch (_stage)
        {
            case 0:
                _ctrl.VirtualAgent.isStopped = true;
                _stage = 1;
                break;

            case 1:

                if (_elapsedTime >= _waitDuration)
                {
                    // Hopefully path calculation is complete - proceed to navigation
                    DebugPanel.UpdateNavState("Path calculated - starting navigation");

                    _ctrl.AssignNavMeshPathToWaypoints();
                    _stateMachine.SetState(NavigationID.DECIDING);
                }
                else
                {
                    // Still waiting for path calculation
                    DebugPanel.UpdateNavState($"Calculating path... {_elapsedTime:F1}s");
                }

                break;
        }
    }

    public override void Exit()
    {
        Debug.Log("-> Pathfinding_NavState::Exit()");
        //VirtualAgent can move automatically again
        _ctrl.VirtualAgent.isStopped = false;
    }

    public override void Dispose()
    {
        //_eventBroker = null;
        base.Dispose();
    }
}
