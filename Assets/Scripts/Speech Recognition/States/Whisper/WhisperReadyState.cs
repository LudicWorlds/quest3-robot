using LudicWorlds;
using UnityEngine;

public class WhisperReadyState : WhisperState
{
    // Start is called before the first frame update
    public WhisperReadyState(IStateMachine<WhisperStateID> stateMachine) : base(stateMachine, WhisperStateID.READY, WhisperStateID.READY)
    {

    }

    public override void Enter()
    {
        Debug.Log("-> WhisperReadyState::Enter()");
        DebugPanel.UpdateSttState("READY");
        _whisper.IsReady = true;
    }
}
