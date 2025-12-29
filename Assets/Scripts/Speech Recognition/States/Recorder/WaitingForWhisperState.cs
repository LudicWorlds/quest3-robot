using LudicWorlds;
using UnityEngine;

public class WaitingForWhisperState : RecorderState
{
    public WaitingForWhisperState(IStateMachine<RecorderStateID> stateMachine) : base(stateMachine, RecorderStateID.WaitingForWhisper, RecorderStateID.DetectStartOfSpeech)
    {

    }
    public override void Enter()
    {
        Debug.Log("-> WaitingForWhisperState::Enter()");
        DebugPanel.UpdateSttState("WaitingForWhisper");
    }

    public override void Update()
    {
        if (_recorder.RunWhisper.IsReady)
        {
            // Check if robot is in standby before transitioning to recording
            if (_recorder.IsRecording)
            {
                _stateMachine.SetState(RecorderStateID.DetectStartOfSpeech);
            }
            else
            {
                _stateMachine.SetState(RecorderStateID.Disabled);
            }
        }
    }

    public override void Exit()
    {
        _recorder.SpeechAudioClip = null;
    }
}
