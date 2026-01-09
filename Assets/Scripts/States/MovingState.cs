using LudicWorlds;
using UnityEngine;

public class MovingState : RobotState
{
    public MovingState(IStateMachine<RobotStateID> stateMachine, RobotStateID id)
        : base(stateMachine, id)
    {
    }

    public override void Enter()
    {
        Debug.Log("-> MovingState::Enter()");
        DebugPanel.UpdateRobotState("MOVING");
        _stage = 0;
        _elapsedTime = 0f;
        _nextStateId = RobotStateID.STATIONARY;
    }

    public override void Update()
    {
        // TODO: Monitor RobotNavigation state
        // When destination reached, transition to LISTENING or SPEAKING
    }

    public override void Exit()
    {
        Debug.Log("-> MovingState::Exit()");
    }
}
