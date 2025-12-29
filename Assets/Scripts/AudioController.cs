using System;
using UnityEngine;

public class AudioController : MonoBehaviour
{
    [Header("Audio")]
    [SerializeField] private AudioSource _audioSource;
    [SerializeField] private AudioClip _audio_TargetSet;
    [SerializeField] private AudioClip _audio_Turning;
    [SerializeField] private AudioClip _audio_Moving;
    [SerializeField] private AudioClip _audio_Waypoint;
    [SerializeField] private AudioClip _audio_DestinationReached;
    [SerializeField] private AudioClip _audio_GoingToFridge;
    [SerializeField] private AudioClip _audio_GoingToSofa;
    [SerializeField] private AudioClip _audio_GoingToTable;
    [SerializeField] private AudioClip _audio_IDontUnderstand;
    [SerializeField] private AudioClip _audio_Abort;
    [SerializeField] private AudioClip _audio_Pathfinding;
    [SerializeField] private AudioClip _audio_Right;
    [SerializeField] private AudioClip _audio_Left;
    [SerializeField] private AudioClip _audio_Pause;
    [SerializeField] private AudioClip _audio_Stuck;

    private EventBroker _eventBroker;
    private bool _wasPlaying = false;

    /// <summary>
    /// Returns true if audio is currently playing
    /// </summary>
    public bool IsPlaying
    {
        get { return _audioSource != null && _audioSource.isPlaying; }
    }

    private void Awake()
    {
        if (_audioSource == null)
        {
            Debug.LogWarning("[AudioCtrl] Can't find the AudioSource! :(");
        }

        InitEvents();
    }

    private void Update()
    {
        // Check for state transitions between playing and not playing
        if (_audioSource != null)
        {
            bool isPlayingNow = _audioSource.isPlaying;

            // Transition from silent to playing
            if (isPlayingNow && !_wasPlaying)
            {
                Debug.Log("[AudioCtrl] AUDIO_STARTED - AudioSource started playing");
                _eventBroker.DispatchEvent(EventID.AUDIO_STARTED);
            }
            // Transition from playing to silent
            else if (!isPlayingNow && _wasPlaying)
            {
                Debug.Log("[AudioCtrl] AUDIO_FINISHED - AudioSource finished all audio");
                _eventBroker.DispatchEvent(EventID.AUDIO_FINISHED);
            }

            _wasPlaying = isPlayingNow;
        }
    }


    private void InitEvents()
    {
        _eventBroker = EventBroker.GetInstance();

        // Create audio-related event handlers
        _eventBroker.CreateEventHandler(EventID.PLAY_AUDIO);
        _eventBroker.CreateEventHandler(EventID.AUDIO_STARTED);
        _eventBroker.CreateEventHandler(EventID.AUDIO_FINISHED);

        // Subscribe to PLAY_AUDIO event
        _eventBroker.Events[EventID.PLAY_AUDIO] += OnPlayAudio;
    }


    /// <summary>
    /// Audio Methods
    /// </summary>
    private void PlayAudioClip(AudioClip clip, bool canInterrupt = true)
    {
        if (_audioSource != null && clip != null)
        {
            if (canInterrupt || !_audioSource.isPlaying)
            {
                _audioSource.PlayOneShot(clip, 1.0f);
                // State transition events now handled in Update()
            }
        }
    }

    private void Play_TargetSet()
    {
        PlayAudioClip(_audio_TargetSet, true);
    }


    private void Play_Turning()
    {
        PlayAudioClip(_audio_Turning, false);
    }

    private void Play_Moving()
    {
        PlayAudioClip(_audio_Moving, false);
    }

    private void Play_Waypoint()
    {
        PlayAudioClip(_audio_Waypoint, true);
    }

    private void Play_DestinationReached()
    {
        PlayAudioClip(_audio_DestinationReached, true);
    }

    private void Play_GoingToFridge()
    {
        PlayAudioClip(_audio_GoingToFridge, true);
    }

    private void Play_GoingToSofa()
    {
        PlayAudioClip(_audio_GoingToSofa, true);
    }

    private void Play_GoingToTable()
    {
        PlayAudioClip(_audio_GoingToTable, true);
    }

    private void Play_IDontUnderstand()
    {
        PlayAudioClip(_audio_IDontUnderstand, true);
    }

    private void Play_Abort()
    {
        PlayAudioClip(_audio_Abort, true);
    }

    private void Play_Pathfinding()
    {
        PlayAudioClip(_audio_Pathfinding, true);
    }

    private void Play_Right()
    {
        PlayAudioClip(_audio_Right, true);
    }

    private void Play_Left()
    {
        PlayAudioClip(_audio_Left, true);
    }

    private void Play_Pause()
    {
        PlayAudioClip(_audio_Pause, true);
    }

    private void Play_Stuck()
    {
        PlayAudioClip(_audio_Stuck, true);
    }


    protected void OnPlayAudio(object sender, EventArgs e)
    {
        PlayAudioEventArgs args = e as PlayAudioEventArgs;

        if (args != null)
        {
            if(args.AudioClip)
            {
                // When playing a direct AudioClip
                PlayAudioClip(args.AudioClip, args.CanInterrupt);
            }
            else if(!string.IsNullOrEmpty(args.AudioID))
            {
                string audio_id = args.AudioID;

                switch (audio_id)
                {
                    case AudioID.TARGET_SET:
                        Play_TargetSet();
                        break;

                    case AudioID.TURNING:
                        Play_Turning();
                        break;

                    case AudioID.MOVING:
                        Play_Moving();
                        break;

                    case AudioID.WAYPOINT:
                        Play_Waypoint();
                        break;

                    case AudioID.DESTINATION_REACHED:
                        Play_DestinationReached();
                        break;

                    case AudioID.GOING_TO_FRIDGE:
                        Play_GoingToFridge();
                        break;

                    case AudioID.GOING_TO_SOFA:
                        Play_GoingToSofa();
                        break;

                    case AudioID.GOING_TO_TABLE:
                        Play_GoingToTable();
                        break;

                    case AudioID.I_DONT_UNDERSTAND:
                        Play_IDontUnderstand();
                        break;

                    case AudioID.ABORT:
                        Play_Abort();
                        break;
                    case AudioID.PATHFINDING:
                        Play_Pathfinding();
                        break;
                    case AudioID.LEFT:
                        Play_Left();
                        break;
                    case AudioID.RIGHT:
                        Play_Right();
                        break;
                    case AudioID.PAUSE:
                        Play_Pause();
                        break;
                    case AudioID.STUCK:
                        Play_Stuck();
                        break;

                }
            }



        }
    }

    private void OnDestroy()
    {
        _eventBroker.Events[EventID.PLAY_AUDIO] -= OnPlayAudio;
        _eventBroker = null;
    }
}
