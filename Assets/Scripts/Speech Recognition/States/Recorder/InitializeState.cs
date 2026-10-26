using LudicWorlds;
using UnityEngine;
#if UNITY_ANDROID && !UNITY_EDITOR
using UnityEngine.Android;
#endif


public class InitializeState : RecorderState
{
    private int _micInitAttempts = 0;
    private bool _micPermissionRequested = false; // RECORD_AUDIO is requested at most once per visit to this state

    public InitializeState(IStateMachine<RecorderStateID> stateMachine) : base(stateMachine, RecorderStateID.Initialize, RecorderStateID.WaitingForWhisper)
    {

    }


    public override void Enter()
    {
        Debug.Log("-> InitializeState::Enter()");
        DebugPanel.UpdateSttState(_id.ToString());
        _micInitAttempts = 0;
        _micPermissionRequested = false;
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
                // Microphone.devices is empty on Android until the RECORD_AUDIO runtime grant exists,
                // so make sure we hold it before looking for a device.
                if (HasMicPermission())
                {
                    _stage = 2;
                }
                else
                {
                    RequestMicPermission();
                }
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

    private bool HasMicPermission()
    {
#if UNITY_ANDROID && !UNITY_EDITOR
        return Permission.HasUserAuthorizedPermission(Permission.Microphone);
#else
        return true;
#endif
    }


    private void RequestMicPermission()
    {
#if UNITY_ANDROID && !UNITY_EDITOR
        if (_micPermissionRequested)
        {
            return;
        }

        _micPermissionRequested = true;
        Debug.Log($"-> Requesting {Permission.Microphone} permission");
        DebugPanel.UpdateSttState("Initialize - Requesting Mic Permission");

        var callbacks = new PermissionCallbacks();
        callbacks.PermissionGranted += permission =>
        {
            Debug.Log($"-> {permission} granted");
            DebugPanel.UpdateSttState("Initialize - Mic Permission Granted");
        };
        callbacks.PermissionDenied += permission =>
        {
            // Android stops showing the dialog after repeated denials; the user must then enable it manually.
            Debug.LogWarning($"-> {permission} DENIED - enable it in Settings > Apps > Quest3Robot > Permissions");
            DebugPanel.UpdateSttState("Mic Permission DENIED - enable in Settings > Apps");
        };

        Permission.RequestUserPermission(Permission.Microphone, callbacks);
#endif
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
