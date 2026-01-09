using LudicWorlds;
using System.Collections;
using UnityEngine;
using Unity.InferenceEngine;

public class RunEncoderState : WhisperState
{
    //ref: https://docs.unity3d.com/Packages/com.unity.sentis@2.0/manual/split-inference-over-multiple-frames.html

    private IEnumerator _schedule;
    private int _layerCount = 0;

    // Start is called before the first frame update
    public RunEncoderState(IStateMachine<WhisperStateID> stateMachine) : base(stateMachine, WhisperStateID.RUN_ENCODER, WhisperStateID.RUN_DECODER)
    {
    }

    public override void Enter()
    {
        Debug.Log("-> RunEncoderState::Enter()");
        DebugPanel.UpdateSttState("RUN_ENCODER");
        _stage = 0;
        _schedule = null;
        _layerCount = 0;
    }

    public override void Update()
    {
        switch (_stage)
        {
            case 0:
                StartModel();
                break;
            case 1:
                ExecuteLayer();
                break;
            case 2:
                ReadOutput();
                break;
            default:
                _stateMachine.SetState(_nextStateId);
                break;
        }
    }

    private void StartModel()
    {
        _schedule = _whisper.encoder.ScheduleIterable( _whisper.SpectroOutput );
        _stage = 1;
    }

    private void ExecuteLayer()
    {
        _layerCount++;

        if (!_schedule.MoveNext())
        {
            _stage = 2;
        }
    }

    private void ReadOutput()
    {
        Debug.Log("-> ReadOutput() - Number of layers: " + _layerCount);
        _whisper.EncodedAudio = _whisper.encoder.PeekOutput() as Tensor<float>;
        _stage = 3;
    }

    public override void Exit()
    {
        // We can dispose SpectroOutput now since encoder has finished processing it
        _whisper.SpectroOutput?.Dispose();
        base.Exit();
    }
}
