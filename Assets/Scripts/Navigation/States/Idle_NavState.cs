using LudicWorlds;
using UnityEngine;

public class Idle_NavState : NavState
{
    private EventBroker _eventBroker;

    public Idle_NavState(IStateMachine<NavigationID> stateMachine, NavigationID id) : base(stateMachine, id)
    {
        _eventBroker = EventBroker.GetInstance();
    }

    public override void Enter()
    {
        Debug.Log("-> Idle_NavState::Enter()");
        base.Enter();
        _stage = 0;
        _elapsedTime = 0f;
        _nextStateId = NavigationID.PATHFINDING;

        // Dispatch NAV_IDLE_ENTER event
        _eventBroker.DispatchEvent(EventID.IDLE_NAV_ENTER);
    }

    public override void Update()
    {
        //base.Update();

        if(_ctrl.DestinationMoved)
        { // The Target Sphere has just been placed at a new position
            _ctrl.DestinationMoved = false;
            _stateMachine.SetState(NavigationID.PATHFINDING);
        }
    }


    private void PlayLocationSpecificAudio()
    {
        string audio_id = "";

        // Play audio based on the label
        switch (_ctrl.Instruction)
        {
            case LocationLabel.FRIDGE:
                audio_id = AudioID.GOING_TO_FRIDGE;
                break;
            case LocationLabel.SOFA:
                audio_id = AudioID.GOING_TO_SOFA;
                break;
            case LocationLabel.TABLE:
                audio_id = AudioID.GOING_TO_TABLE;
                break;
            default:
                Debug.Log($"[RobotCtrl] No specific audio for instruction: {_ctrl.Instruction}");
                break;
        }

        PlayAudioEventArgs args = new PlayAudioEventArgs(audio_id);
        _eventBroker.DispatchEvent(EventID.PLAY_AUDIO, args);
    }


    public override void Exit()
    {
        Debug.Log("-> Idle_NavState::Exit()");

        // Dispatch NAV_IDLE_EXIT event
        _eventBroker.DispatchEvent(EventID.IDLE_NAV_EXIT);

        //we are most probably exiting the NavIdle state
        //because we have received a verbal instruction from the user

        //play the audio relating to this last instruction - the MicRecorder shouldn't be listening right now!
        PlayLocationSpecificAudio();

    }

    public override void Dispose()
    {
        _eventBroker = null;
        base.Dispose();
    }
}
