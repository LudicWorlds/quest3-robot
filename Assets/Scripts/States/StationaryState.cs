using LudicWorlds;
using UnityEngine;
using System;

public class StationaryState : RobotState
{
    private EventBroker _eventBroker;
    private AudioController _audioController;

    public StationaryState(IStateMachine<RobotStateID> stateMachine, RobotStateID id)
        : base(stateMachine, id)
    {
        _eventBroker = EventBroker.GetInstance();
    }

    public override void Enter()
    {
        Debug.Log("-> StationaryState::Enter()");
        DebugPanel.UpdateRobotState("STATIONARY");
        _stage = 0;
        _elapsedTime = 0f;
        _nextStateId = RobotStateID.MOVING;

        // Get AudioController reference
        _audioController = _ctrl.GetComponent<AudioController>();

        // Subscribe to audio events
        _eventBroker.Events[EventID.AUDIO_STARTED] += OnAudioStarted;
        _eventBroker.Events[EventID.AUDIO_FINISHED] += OnAudioFinished;

        // Check if audio is currently playing before enabling mic
        if (_audioController != null && !_audioController.IsPlaying)
        {
            Debug.Log("[StationaryState] No audio playing - enabling mic recording");
            _eventBroker.DispatchEvent(EventID.ENABLE_MIC_RECORDING);
        }
        else
        {
            Debug.Log("[StationaryState] Audio is playing - mic recording will be enabled when audio finishes");
        }
    }

    public override void Update()
    {
        // Robot is stationary, waiting for voice commands or navigation instruction
    }

    public override void Exit()
    {
        Debug.Log("-> StationaryState::Exit()");

        // Unsubscribe from audio events
        _eventBroker.Events[EventID.AUDIO_STARTED] -= OnAudioStarted;
        _eventBroker.Events[EventID.AUDIO_FINISHED] -= OnAudioFinished;

        // Always disable mic recording when leaving stationary state (robot is about to move)
        Debug.Log("[StationaryState] Exiting - disabling mic recording");
        _eventBroker.DispatchEvent(EventID.DISABLE_MIC_RECORDING);
    }

    private void OnAudioStarted(object sender, EventArgs e)
    {
        Debug.Log("[StationaryState] Audio started - disabling mic recording");
        _eventBroker.DispatchEvent(EventID.DISABLE_MIC_RECORDING);
    }

    private void OnAudioFinished(object sender, EventArgs e)
    {
        Debug.Log("[StationaryState] Audio finished - enabling mic recording");
        _eventBroker.DispatchEvent(EventID.ENABLE_MIC_RECORDING);
    }

    public override void Dispose()
    {
        _eventBroker = null;
        _audioController = null;
        base.Dispose();
    }
}
