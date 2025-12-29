using LudicWorlds;
using UnityEngine;

public class Init_NavState : NavState
{
    public Init_NavState(IStateMachine<NavigationID> stateMachine, NavigationID id) : base(stateMachine, id)
    {
    }

    public override void Enter()
    {
        Debug.Log("-> Init_NavState::Enter()");
        base.Enter();
        _stage = 0;
        _elapsedTime = 0f;
        _nextStateId = NavigationID.IDLE;
    }

    public override void Update()
    {
        //base.Update();

        //This state does nothing, we simply wait until everything has been initialized, before moving to the NAV_IDLE state.
    }

    public override void Exit()
    {
        Debug.Log("-> Init_NavState::Exit()");
    }
}
