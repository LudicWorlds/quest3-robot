using UnityEngine;
using LudicWorlds;
using System;


[RequireComponent(typeof(AudioController))]
public class MicRecorder : GameObjectStateMachine<RecorderStateID>
{
    public const int CLIP_LENGTH = 20;
    public const int CLIP_FREQUENCY = 16000;

    [SerializeField, Range(0.0f, 0.1f)] public float SilenceThreshold = 0.02f; // Threshold to consider as silence

    public RunWhisper RunWhisper; // This is the reference to the RunWhisper script

    public bool PlaybackRecordedAudio = true;

    public int SpeechStartPosition { get; set; }
    public int SpeechEndPosition { get; set; }

    public AudioClip LoopingAudioClip { get; set; }
    public AudioClip SpeechAudioClip { get; set; }

    //public AudioSource Source { get; set; } //Audio Source is attached to camera

    public string DeviceName { get; set; }  //holds the name of the detected Microphone device
    private bool isRecording;

    private AudioController _audioController;
    private EventBroker _eventBroker;

    public bool IsRecording { get; private set; }



    protected override void Awake()
    {
        _audioController = GetComponent<AudioController>();
        if (_audioController == null)
        {
            Debug.LogError("[MicRecorder] _audioController is NULL :(");
        }

        // Initialize event system
        _eventBroker = EventBroker.GetInstance();

        // Subscribe to microphone recording events
        _eventBroker.Events[EventID.ENABLE_MIC_RECORDING] += OnEnableMicRecording;
        _eventBroker.Events[EventID.DISABLE_MIC_RECORDING] += OnDisableMicRecording;

        // Assume not recording until we hear otherwise
        IsRecording = false;

        base.Awake(); // <- Init StateMachine
    }

    protected override void Start()
    {
        base.Start(); // <- Init States
    }

    protected override void InitStates()
    {
        base.InitStates();

        AddState(new InitializeState(this));
        AddState(new WaitingForWhisperState(this));
        AddState(new DisabledState(this));
        AddState(new DetectStartOfSpeechState(this));
        AddState(new DetectEndOfSpeechState(this));
        AddState(new SendToWhisperState(this));

        SetState(RecorderStateID.Initialize);
    }


    // Update is called once per frame
    protected override void Update()
    {
        base.Update();
    }

    public void TryToPlayRecording()
    {
        (_currentState as RecorderState).OnInputAction(); //PlayRecording
    }

    public void PlayExtractedSpeech()
    {
        if (SpeechAudioClip != null)
        {
            Debug.Log("-> PlayExtractedSpeech()");

            PlayAudioEventArgs args = new PlayAudioEventArgs(SpeechAudioClip);
            _eventBroker.DispatchEvent(EventID.PLAY_AUDIO, args);
        }
        else
        {
            Debug.Log("-> PlayExtractedSpeech() -  clip is NULL! :(");
        }
    }

    private string PrintAudioClipDetail(AudioClip clip)
    {
        string details = "clip secs: " + LoopingAudioClip.length + ", samp: " + LoopingAudioClip.samples + ", chan: " + LoopingAudioClip.channels + ", freq: " + LoopingAudioClip.frequency;
        return details;
    }

    public void TranscribeUsingWhisper()
    {
        RunWhisper.Transcribe(SpeechAudioClip);
    }

    private void OnEnableMicRecording(object sender, EventArgs e)
    {
        Debug.Log("[MicRecorder] RobotNavigation entered IDLE - recording enabled");
        IsRecording = true;

        // If in DISABLED state, transition to DetectStartOfSpeech to start listening
        if (_currentState.ID == RecorderStateID.Disabled)
        {
            SetState(RecorderStateID.DetectStartOfSpeech);
        }
    }

    private void OnDisableMicRecording(object sender, EventArgs e)
    {
        Debug.Log("[MicRecorder] RobotNavigation exited IDLE - recording disabled");
        IsRecording = false;

        // If currently listening for speech or processing, transition to DISABLED
        if (_currentState.ID == RecorderStateID.DetectStartOfSpeech ||
            _currentState.ID == RecorderStateID.DetectEndOfSpeech)
        {
            Debug.Log("[MicRecorder] Transitioning to Disabled state due to navigation start");
            SetState(RecorderStateID.Disabled);
        }
    }

    protected override void OnDestroy()
    {
        // Unsubscribe from events
        if (_eventBroker != null)
        {
            _eventBroker.Events[EventID.ENABLE_MIC_RECORDING] -= OnEnableMicRecording;
            _eventBroker.Events[EventID.DISABLE_MIC_RECORDING] -= OnDisableMicRecording;
        }

        base.OnDestroy();
    }
}