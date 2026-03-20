using System;
using LudicWorlds;
using UnityEngine;

public class Obstructed_NavState : NavState
{
    private EventBroker _eventBroker;

    public Obstructed_NavState(IStateMachine<NavigationID> stateMachine, NavigationID id) : base(stateMachine, id)
    {
        _eventBroker = EventBroker.GetInstance();
    }

    public override void Enter()
    {
        Debug.Log("-> Obstructed_NavState::Enter()");
        base.Enter();
        _nextStateId = NavigationID.DECIDING;

        _eventBroker.Events[EventID.OBSTRUCTION_CLEARED] += OnObstructionCleared;

        // Stop all motors
        _ctrl.Com.SetCommandGivenAction(RobotAction.Stop);

        _ctrl.PlayAudio(AudioID.OBSTRUCTED);

        DebugPanel.UpdateNavState("OBSTRUCTED - Waiting for clear path");
    }

    public override void Update()
    {
    }

    public override void Exit()
    {
        Debug.Log("-> Obstructed_NavState::Exit()");
        _eventBroker.Events[EventID.OBSTRUCTION_CLEARED] -= OnObstructionCleared;
    }

    private void OnObstructionCleared(object sender, EventArgs e)
    {
        Debug.Log("[Obstructed_NavState] Obstruction cleared - transitioning to DECIDING");
        _stateMachine.SetState(NavigationID.DECIDING);
    }

    public override void Dispose()
    {
        _eventBroker = null;
        base.Dispose();
    }
}
