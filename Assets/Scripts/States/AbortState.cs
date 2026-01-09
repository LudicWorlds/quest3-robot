using LudicWorlds;
using UnityEngine;

public class AbortState : RobotState
{
    public AbortState(IStateMachine<RobotStateID> stateMachine, RobotStateID id)
        : base(stateMachine, id)
    {
    }

    public override void Enter()
    {
        Debug.Log("-> AbortState::Enter()");
        DebugPanel.UpdateRobotState("ABORT");
        _stage = 0;
        _elapsedTime = 0f;
        _nextStateId = RobotStateID.ABORT;
        _ctrl.Nav.SetState(NavigationID.ABORT);
    }

    public override void Update()
    {
        // TODO: Monitor RobotNavigation state
        // When destination reached, transition to LISTENING or SPEAKING
    }

    public override void Exit()
    {
        Debug.Log("-> AbortState::Exit()");
    }
}