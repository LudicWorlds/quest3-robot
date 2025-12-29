using LudicWorlds;
using UnityEngine;
using UnityEngine.InputSystem;



public abstract class RecorderState : GameObjectState<RecorderStateID>
{
    protected MicRecorder _recorder;
    protected int _stage = 0; // 0: Loading, 1: Processing
    protected RecorderStateID _nextStateId;

    public RecorderState(IStateMachine<RecorderStateID> stateMachine, RecorderStateID id, RecorderStateID nextStateId) : base(stateMachine, id)
    {
        _recorder = stateMachine as MicRecorder;
        this._nextStateId = nextStateId;
    }

    public virtual void OnInputAction(string input = "") { }

    protected float[] GetSamples(int startPosition, int endPosition)
    {
        int samplesLength = endPosition - startPosition;
        float[] samples = new float[samplesLength];
        bool success = _recorder.LoopingAudioClip.GetData(samples, startPosition);

        if(!success)
        {
            Debug.LogError("-> GetSamples() - GetData not successfull! :(");
        }

        return samples;
    }
}

