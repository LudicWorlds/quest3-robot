using LudicWorlds;
using UnityEngine;

public class DisabledState : RecorderState
{
    public DisabledState(IStateMachine<RecorderStateID> stateMachine)
        : base(stateMachine, RecorderStateID.Disabled, RecorderStateID.DetectStartOfSpeech)
    {
    }

    public override void Enter()
    {
        Debug.Log("-> DisabledState::Enter() - Recording disabled while robot navigating");
        DebugPanel.UpdateSttState("REC_DISABLED");
    }

    public override void Update()
    {
        // Stay in this state until ENABLE_MIC_RECORDING event triggers transition
        // The MicRecorder event handler will call SetState() to exit this state
    }

    public override void Exit()
    {
        Debug.Log("-> DisabledState::Exit() - Recording re-enabled");
    }
}
