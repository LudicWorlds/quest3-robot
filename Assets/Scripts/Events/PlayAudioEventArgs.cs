using System;
using UnityEngine;

public class PlayAudioEventArgs : EventArgs
{
    private string _audioID = "";
    private AudioClip _audioClip = null;
    private bool _canInterrupt = true;


    public string AudioID
    {
        get { return _audioID; }
    }

    public AudioClip AudioClip
    {
        get { return _audioClip; }
    }

    public bool CanInterrupt
    {
        get { return _canInterrupt; }
    }


    public PlayAudioEventArgs(string instruction, bool canInterrupt = true)
    {
        _audioID = instruction;
        _canInterrupt = canInterrupt;
    }

    public PlayAudioEventArgs(AudioClip audioClip, bool canInterrupt = true)
    {
        _audioClip = audioClip;
        _canInterrupt = canInterrupt;
    }
}
