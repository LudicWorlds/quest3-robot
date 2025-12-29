using LudicWorlds;
using Unity.InferenceEngine;

public class WhisperState : GameObjectState<WhisperStateID>
{
    protected RunWhisper _whisper;
    protected int _stage = 0; // 0: Loading, 1: Processing
    protected WhisperStateID _nextStateId;

    protected const BackendType _backend = BackendType.GPUCompute;

    public WhisperState(IStateMachine<WhisperStateID> stateMachine, WhisperStateID id, WhisperStateID nextStateId) : base(stateMachine, id)
    {
        _whisper = stateMachine as RunWhisper;
        this._nextStateId = nextStateId;
    }

    public override void Enter() { }
    public override void Update() { }
    public override void Exit() { }
}
