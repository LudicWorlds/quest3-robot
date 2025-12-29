using LudicWorlds;
using UnityEngine;

public class Abort_NavState : NavState
{
    private EventBroker _eventBroker;

    public Abort_NavState(IStateMachine<NavigationID> stateMachine, NavigationID id) : base(stateMachine, id)
    {
        _eventBroker = EventBroker.GetInstance();
    }


    public override void Enter()
    {
        Debug.Log("-> Abort_NavState::Enter()");
        base.Enter();
        _stage = 0;
        _elapsedTime = 0f;
        _nextStateId = NavigationID.ABORT;

        PlayAudioEventArgs args = new PlayAudioEventArgs(AudioID.ABORT);
        _eventBroker.DispatchEvent(EventID.PLAY_AUDIO, args);

        _ctrl.Com.SetCommandGivenAction(RobotAction.Stop);

        Debug.Log(_ctrl.DebugInfo);
    }


    public override void Update()
    {
        //base.Update();
    }


    public override void Exit()
    {
        Debug.Log("-> Abort_NavState::Exit()");
    }


    public override void Dispose()
    {
        _eventBroker = null;
        base.Dispose();
    }
}
