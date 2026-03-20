using LudicWorlds;
using UnityEngine;


public class InitializeState : RecorderState
{
    private int _micInitAttempts = 0;

    public InitializeState(IStateMachine<RecorderStateID> stateMachine) : base(stateMachine, RecorderStateID.Initialize, RecorderStateID.WaitingForWhisper)
    {

    }


    public override void Enter()
    {
        Debug.Log("-> InitializeState::Enter()");
        DebugPanel.UpdateSttState(_id.ToString());
        _micInitAttempts = 0;
        _stage = 0;
    }

    public override void Update()
    {
       switch(_stage)
       {
            case 0:
                //Debug.Log("* Hold down the Left Trigger to Record.");
                //Debug.Log("* Release the Left Trigger to stop Recording.");
                //Debug.Log("* Press the Right Trigger to Transcribe.");
                _stage = 1;
                break;
            case 1:
                //GetAudioSource();
                _stage = 2;
                break;
            case 2:
                if(GetMicrophone())
                {
                    _stage = 3;
                }
                break;
            case 3:
                _stateMachine.SetState(_nextStateId);
                break;

       }
    }

    private bool GetMicrophone()
    {
        _micInitAttempts++;

        if (Microphone.devices.Length > 0)
        {
            _recorder.DeviceName = Microphone.devices[0];

            foreach (string device in Microphone.devices)
            {
                Debug.Log(device);

#if UNITY_EDITOR

                if (device.ToUpper().Contains("OCULUS"))
                {
                    _recorder.DeviceName = device;
                }

#endif
            }

            // On the MetaQuest 3 we should find: "Android audio input" device
            Debug.Log($"-> Microphones: {Microphone.devices.Length}, Acquired after {_micInitAttempts} attempts.");
            DebugPanel.UpdateSttState("Microphone Name: " + _recorder.DeviceName);
            return true;
        }
        else
        {
            DebugPanel.UpdateSttState("Initialize - No Microphone");
            //Debug.LogError("-> No Microphone found! :(");
            return false;
        }
    }


    public override void Exit() { }
}
