using LudicWorlds;
using Unity.InferenceEngine;


public class LoadEncoderState : WhisperState
{

    public LoadEncoderState(IStateMachine<WhisperStateID> stateMachine) : base(stateMachine, WhisperStateID.LOAD_ENCODER, WhisperStateID.LOAD_SPECTRO)
    {

    }

    public override void Enter()
    {
        //Debug.Log("-> LoadEncoderState::Enter()");
        DebugPanel.UpdateSttState("LOAD_ENCODER");
        _stage = 0;
    }

    public override void Update()
    {
        switch (_stage)
        {
            case 0:
                LoadEncoder();
                _stage = 1; 
                break;
            default:
                _stateMachine.SetState(_nextStateId);
                break;
        }
    }

    private void LoadEncoder()
    {
        Model encoderModel = ModelLoader.Load(_whisper.audioEncoder);
        _whisper.encoder = new Worker(encoderModel, _backend);
    }

    public override void Exit()
    {
        base.Exit();
    }
}