using LudicWorlds;
using UnityEngine;
using System;
using UnityEngine.InputSystem;

public class SendToWhisperState : RecorderState
{
    private int _totalSamples = 0;
    private int _channels = 1;
    private int _frequency = 16000;
    private int _speechStartPos = 0;
    private int _speechEndPos = 0;

    public SendToWhisperState(IStateMachine<RecorderStateID> stateMachine) : base(stateMachine, RecorderStateID.SendToWhisper, RecorderStateID.WaitingForWhisper)
    {

    }
    public override void Enter()
    {
        Debug.Log("-> SendToWhisperState::Enter()");
        DebugPanel.UpdateSttState(_id.ToString());
        _stage = 0;

        _totalSamples = _recorder.LoopingAudioClip.samples;
        _channels = _recorder.LoopingAudioClip.channels;
        _frequency = _recorder.LoopingAudioClip.frequency;
        _speechStartPos = _recorder.SpeechStartPosition;
        _speechEndPos = _recorder.SpeechEndPosition;

        _recorder.SpeechAudioClip = ExtractSpeechSegment();

        if(_recorder.SpeechAudioClip == null)
        {
            Debug.Log("-> _recorder.SpeechAudioClip == null! :(");
        }


        if(_recorder.PlaybackRecordedAudio)
        {
            _recorder.PlayExtractedSpeech();
        }

        _recorder.TranscribeUsingWhisper();
    }

    public override void Update() 
    {
        _stateMachine.SetState( _nextStateId );
    }
    public override void Exit()
    { 
        _recorder.LoopingAudioClip = null;
    }

    public override void OnInputAction(string input = "")
    {
        Debug.Log("-> OnInputAction");
        _recorder.PlayExtractedSpeech();
    }

    private AudioClip ExtractSpeechSegment()
    {
        // Calculate the length of the speech segment
        int speechLength;
        if (_speechEndPos >= _speechStartPos)
        {
            // Normal case: speech is contained within one loop of the audio clip
            speechLength = _speechEndPos - _speechStartPos;
        }
        else
        {
            // Loopback case: speech starts near the end and continues from the beginning of the loop
            speechLength = (_totalSamples - _speechStartPos) + _speechEndPos;
        }

        // Create an array to hold the extracted samples
        float[] speechSamples = new float[speechLength * _channels];

        // Extract samples from the looping audio clip
        if (_speechEndPos >= _speechStartPos)
        {
            // Normal case
            _recorder.LoopingAudioClip.GetData(speechSamples, _speechStartPos);
        }
        else
        {
            // Loopback case
            // Part 1: From SpeechStartPosition to the end of the clip
            int firstPartLength = _totalSamples - _speechStartPos;
            float[] firstPartSamples = new float[firstPartLength * _channels];
            _recorder.LoopingAudioClip.GetData(firstPartSamples, _speechStartPos);
            Array.Copy(firstPartSamples, 0, speechSamples, 0, firstPartSamples.Length);

            // Part 2: From the start of the clip to SpeechEndPosition
            float[] secondPartSamples = new float[_speechEndPos * _channels];
            _recorder.LoopingAudioClip.GetData(secondPartSamples, 0);
            Array.Copy(secondPartSamples, 0, speechSamples, firstPartSamples.Length, secondPartSamples.Length);
        }

        // Create a new AudioClip with the extracted speech samples
        AudioClip speechClip = AudioClip.Create("ExtractedSpeech", speechLength, _channels, _frequency, false);
        speechClip.SetData(speechSamples, 0);

        Debug.Log($"-> Clip secs: {speechClip.length}, samp: {speechClip.samples}, chan: {speechClip.channels}, freq: {speechClip.frequency}");

        return speechClip;
    }
}
