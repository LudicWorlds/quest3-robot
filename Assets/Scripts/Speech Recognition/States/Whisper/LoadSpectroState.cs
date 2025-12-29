using LudicWorlds;
using Unity.InferenceEngine;

public class LoadSpectroState : WhisperState
{

    public LoadSpectroState(IStateMachine<WhisperStateID> stateMachine) : base(stateMachine, WhisperStateID.LOAD_SPECTRO, WhisperStateID.READY)
    {

    }

    public override void Enter()
    {
        //Debug.Log("-> LoadSpectroState::Enter()");
        DebugPanel.UpdateSttState("LoadLogMelSpectro");
        _stage = 0;
    }

    public override void Update()
    {
        switch (_stage)
        {
            case 0:
                LoadLogMelSpectro();
                _stage = 1;
                break;
            default:
                _stateMachine.SetState(_nextStateId);
                break;
        }
    }


    private void LoadLogMelSpectro()
    {
        Model logMelSpectroModel = ModelLoader.Load(_whisper.logMelSpectro);
        _whisper.spectrogram = new Worker(logMelSpectroModel, _backend);
    }


    public override void Exit()
    {
        base.Exit();
    }
}