using LudicWorlds;
using UnityEngine;

public class DetectStartOfSpeechState : RecorderState
{
    private string _deviceName;
    private int _currentPos;
    private int _previousPos;
    private float _silenceThreshold;
    private int _channels;
    private int _totalSamples;

    private bool _speechDetected = false;

    public DetectStartOfSpeechState(IStateMachine<RecorderStateID> stateMachine)
        : base(stateMachine, RecorderStateID.DetectStartOfSpeech, RecorderStateID.DetectEndOfSpeech)
    {
    }

    public override void Enter()
    {
        Debug.Log("-> DetectStartOfSpeechState::Enter()");
        DebugPanel.UpdateSttState(_id.ToString());

        // Initialize variables
        _deviceName = _recorder.DeviceName;
        _silenceThreshold = _recorder.SilenceThreshold;
        _speechDetected = false;

        // Start microphone recording if not already recording
        if (!Microphone.IsRecording(_deviceName))
        {
            _recorder.LoopingAudioClip = Microphone.Start(_deviceName, true, MicRecorder.CLIP_LENGTH, MicRecorder.CLIP_FREQUENCY);
            Debug.Log("-> Microphone started with looping.");
        }
        else
        {
            Debug.Log("-> Microphone already recording!!!");
        }

        // Retrieve audio clip properties
        _channels = _recorder.LoopingAudioClip.channels;
        _totalSamples = _recorder.LoopingAudioClip.samples;

        _previousPos = 0;
        _currentPos = 0;
    }

    public override void Update()
    {
        if (!_speechDetected)
        {
            DetectSpeechStart();
        }
        else
        {
            // Transition to the next state
            _stateMachine.SetState(_nextStateId);
        }
    }

    public override void Exit()
    {
        // Microphone continues recording in the next state
    }

    private void DetectSpeechStart()
    {
        _currentPos = Microphone.GetPosition(_deviceName);

        if (_currentPos != _previousPos)
        {
            int sampleCount = (_currentPos - _previousPos + _totalSamples) % _totalSamples;
            if (sampleCount > 0)
            {
                AnalyzeSamples(_previousPos, sampleCount);
            }

            _previousPos = _currentPos;
        }
    }

    private void AnalyzeSamples(int startPosition, int sampleCount)
    {
        float[] samples = new float[sampleCount * _channels];

        if (startPosition + sampleCount <= _totalSamples)
        {
            // No wrap-around
            _recorder.LoopingAudioClip.GetData(samples, startPosition);
        }
        else
        {
            // Wrap-around
            int firstPartCount = _totalSamples - startPosition;
            int secondPartCount = sampleCount - firstPartCount;

            float[] firstPartSamples = new float[firstPartCount * _channels];
            float[] secondPartSamples = new float[secondPartCount * _channels];

            _recorder.LoopingAudioClip.GetData(firstPartSamples, startPosition);
            _recorder.LoopingAudioClip.GetData(secondPartSamples, 0);

            firstPartSamples.CopyTo(samples, 0);
            secondPartSamples.CopyTo(samples, firstPartSamples.Length);
        }

        for (int i = 0; i < samples.Length; i += _channels)
        {
            float volume = Mathf.Abs(samples[i]); // Simple volume estimation
            //DebugPanel.SetVolume(volume.ToString("F4"));
            DebugPanel.UpdateSttState(volume.ToString("F4"));

            if (volume > _silenceThreshold)
            {
                // Speech detected
                _recorder.SpeechStartPosition = (_previousPos + i / _channels) % _totalSamples;
                Debug.Log("-> Speech started at position: " + _recorder.SpeechStartPosition);

                _speechDetected = true;

                // No need to continue analyzing
                break;
            }
        }
    }
}
