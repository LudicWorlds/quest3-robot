using LudicWorlds;
using Unity.InferenceEngine;

public class LoadDecodersState : WhisperState
{

    public LoadDecodersState(IStateMachine<WhisperStateID> stateMachine) : base(stateMachine, WhisperStateID.LOAD_DECODER, WhisperStateID.LOAD_ENCODER)
    {

    }

    public override void Enter()
    {
        //Debug.Log("-> LoadDecoderState::Enter()");
        DebugPanel.UpdateSttState("LOAD_DECODER");
        _stage = 0;
    }

    public override void Update()
    {
        switch (_stage)
        {
            case 0:
                CreateDecoders();
                _stage = 1;
                break;
            case 1:
                InitArgmax();
                _stage = 2;
                break;
            default:
                _stateMachine.SetState( _nextStateId );
                break;
        }
    }


    private void CreateDecoders()
    {
        Model decoder1Model = ModelLoader.Load(_whisper.audioDecoder1);
        Model decoder2Model = ModelLoader.Load(_whisper.audioDecoder2);

        _whisper.decoder1 = new Worker(decoder1Model, _backend);
        _whisper.decoder2 = new Worker(decoder2Model, _backend);
    }


    private void InitArgmax() 
    {
        // Define the functional graph of the model.
        var graph = new FunctionalGraph();

        var input = graph.AddInput(DataType.Float, new DynamicTensorShape(1, 1, 51865));
        var amax = Functional.ArgMax(input, -1, false);
        var selectTokenModel = graph.Compile(amax);

        _whisper.argmax = new Worker(selectTokenModel, BackendType.GPUCompute);
    }


    public override void Exit()
    {
        base.Exit();
    }
}
