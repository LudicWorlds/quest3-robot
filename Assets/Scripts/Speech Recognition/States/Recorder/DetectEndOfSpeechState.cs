using LudicWorlds;
using UnityEngine;

public class DetectEndOfSpeechState : RecorderState
{
    private const float MIN_SILENCE_DURATION = 1.75f; // Minimum silence duration in seconds
    private const float MAX_SPEECH_DURATION = 20.0f; // Maximum speech duration

    private string _deviceName;
    private int _currentPos;
    private int _previousPos;
    private float _silenceThreshold;
    private int _channels;
    private int _totalSamples;

    private float _speechStartTime;

    private bool _isSilent = false;
    private float _silenceStartTime = 0f;

    public DetectEndOfSpeechState(IStateMachine<RecorderStateID> stateMachine)
        : base(stateMachine, RecorderStateID.DetectEndOfSpeech, RecorderStateID.SendToWhisper)
    {
    }

    public override void Enter()
    {
        Debug.Log("-> DetectEndOfSpeechState::Enter()");
        DebugPanel.UpdateSttState(_id.ToString());

        // Initialize variables
        _deviceName = _recorder.DeviceName;
        _silenceThreshold = _recorder.SilenceThreshold;
        _channels = _recorder.LoopingAudioClip.channels;

        _totalSamples = _recorder.LoopingAudioClip.samples;

        _isSilent = false;

        _speechStartTime = Time.time;

        _currentPos = Microphone.GetPosition(_deviceName);
        _previousPos = _currentPos;
    }

    public override void Update()
    {
        // Check for maximum speech duration
        float speechDuration = Time.time - _speechStartTime;
        if (speechDuration >= MAX_SPEECH_DURATION)
        {
            Debug.Log("-> Maximum speech duration reached.");
            _recorder.SpeechEndPosition = Microphone.GetPosition(_deviceName);
            Microphone.End(_deviceName);
            _stateMachine.SetState(_nextStateId);
            return;
        }

        ProcessRecentAudio();
    }

    public override void Exit()
    {
        // Ensure microphone is stopped
        Microphone.End(_deviceName);
    }

    private void ProcessRecentAudio()
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

            if (volume < _silenceThreshold)
            {
                if (!_isSilent)
                {
                    // Silence just started
                    _isSilent = true;
                    _silenceStartTime = Time.time;
                }
                else
                {
                    // Continuing silence
                    float silenceDuration = Time.time - _silenceStartTime;
                    if (silenceDuration >= MIN_SILENCE_DURATION)
                    {
                        // Prolonged silence detected, speech has ended
                        _recorder.SpeechEndPosition = (_previousPos + i / _channels) % _totalSamples;
                        Debug.Log("-> Speech ended at position: " + _recorder.SpeechEndPosition);

                        // Stop recording
                        Microphone.End(_deviceName);

                        // Transition to next state
                        _stateMachine.SetState(_nextStateId);
                        return;
                    }
                }
            }
            else
            {
                // Not silent
                _isSilent = false;
            }
        }
    }
}

