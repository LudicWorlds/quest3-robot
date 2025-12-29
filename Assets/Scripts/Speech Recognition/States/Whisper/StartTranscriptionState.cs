using LudicWorlds;
using Unity.InferenceEngine;
using UnityEngine;

public class StartTranscriptionState : WhisperState
{
    // Maximum size of audioClip (30s at 16kHz)
    const int maxSamples = 30 * 16000;

    // Start is called before the first frame update

    public StartTranscriptionState(IStateMachine<WhisperStateID> stateMachine) : base(stateMachine, WhisperStateID.START_TRANSCRIPTION, WhisperStateID.RUN_SPECTRO)
    {
    }

    public override void Enter()
    {
        Debug.Log("-> StartTranscriptionState::Enter()");
        DebugPanel.UpdateSttState("START_TRANSCRIPTION");
        _whisper.SpeechText.color = Color.gray;
        _whisper.SpeechText.text = "Transcribing ...";
        _whisper.Transcription = "";

        _stage = 0;
    }

    private void LoadAudio()
    {
        if (_whisper.AudioClip.frequency != 16000)
        {
            Debug.Log($"The audio clip should have _frequency 16kHz. It has _frequency {_whisper.AudioClip.frequency / 1000f}kHz");
            return;
        }

        _whisper.NumSamples = _whisper.AudioClip.samples;

        if (_whisper.NumSamples > maxSamples)
        {
            Debug.Log($"The AudioClip is too long. It must be less than 30 seconds. This clip is {_whisper.NumSamples / _whisper.AudioClip.frequency} seconds.");
            return;
        }

        var data = new float[maxSamples];
        _whisper.NumSamples = maxSamples;

        //We will get a warning here if data.length is larger than audio length but that is OK
        _whisper.AudioClip.GetData(data, 0);
        _whisper.AudioInput = new Tensor<float>(new TensorShape(1, _whisper.NumSamples), data);
    }

    public override void Update()
    {
        switch (_stage)
        {
            case 0:
                LoadAudio();
                _stage = 1;
                break;
            default:
                _stateMachine.SetState(_nextStateId);
                break;
        }
    }
}
