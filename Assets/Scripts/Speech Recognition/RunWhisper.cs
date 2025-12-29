using System.Collections.Generic;
using UnityEngine;
using Unity.InferenceEngine;
using TMPro;
using LudicWorlds;

// https://huggingface.co/unity/sentis-whisper-tiny/blob/main/RunWhisper.cs

public class RunWhisper : GameObjectStateMachine<WhisperStateID>
{
    public ModelAsset audioDecoder1;
    public ModelAsset audioDecoder2;
    public ModelAsset audioEncoder;
    public ModelAsset logMelSpectro;

    public TextAsset vocabJson; //?????

    public Worker decoder1 { get; set; }
    public Worker decoder2 { get; set; }
    public Worker encoder { get; set; }
    public Worker spectrogram { get; set; }
    public Worker argmax { get; set; }

    // Link your audioclip here. Format must be 16Hz mono non-compressed.
    private AudioClip audioClip;
    public AudioClip AudioClip { get { return audioClip; } }
    public Tensor<float> AudioInput { get; set; }


    public int NumSamples { get; set; }
    //public float[] Data { get; set; }
    public string[] Tokens { get; set; }

    // Used for special character decoding;
    public int[] WhiteSpaceCharacters { get; set; }

    public Tensor<float> SpectroOutput { get; set; } // GPU Tensor
    public Tensor<float> EncodedAudio { get; set; }  // GPU Tensor

    public TMP_Text SpeechText;

    public bool IsReady { get; set; }
    public string Transcription { get; set; }
    public string Instruction { get; set; }

    protected override void Awake()
    {
        IsReady = false;
        WhiteSpaceCharacters = new int[256];
        SetupWhiteSpaceShifts();
        SetTokens();
        base.Awake();
    }

    protected override void Start()
    {
        base.Start(); // <- Init States
    }

    protected override void InitStates()
    {
        base.InitStates();
        AddState(new LoadDecodersState(this));
        AddState(new LoadEncoderState(this));
        AddState(new LoadSpectroState(this));
        AddState(new WhisperReadyState(this));

        AddState(new StartTranscriptionState(this));
        AddState(new RunSpectroState(this));
        AddState(new RunEncoderState(this));
        AddState(new RunDecoderState(this));
        AddState(new UnderstandInstructionState(this));

        SetState(WhisperStateID.LOAD_DECODER);
    }


    private void SetTokens()
    {
        var vocab = Newtonsoft.Json.JsonConvert.DeserializeObject<Dictionary<string, int>>(vocabJson.text);
        Tokens = new string[vocab.Count];

        foreach (var item in vocab)
        {
            Tokens[item.Value] = item.Key;
        }
    }


    public void Transcribe( AudioClip clip )
    {
        Debug.Log("-> SentisWisper::Transcribe() ...");

        IsReady = false;
        audioClip = clip;

        SetState( WhisperStateID.START_TRANSCRIPTION );
    }


    protected override void Update()
    {
        base.Update();
    }


    void SetupWhiteSpaceShifts()
    {
        for (int i = 0, n = 0; i < 256; i++)
        {
            if (IsWhiteSpace((char)i)) WhiteSpaceCharacters[n++] = i;
        }
    }

    bool IsWhiteSpace(char c)
    {
        return !(('!' <= c && c <= '~') || ('�' <= c && c <= '�') || ('�' <= c && c <= '�'));
    }

    protected override void OnDestroy()
    {
        base.OnDestroy();

        // Dispose workers
        decoder1?.Dispose();
        decoder2?.Dispose();
        encoder?.Dispose();
        spectrogram?.Dispose();
        argmax?.Dispose();

        // Dispose tensors
        // Note: SpectroOutput and EncodedAudio are disposed in state Exit() methods
        // but we dispose them here too in case OnDestroy is called during transcription
        AudioInput?.Dispose();
        SpectroOutput?.Dispose();
        EncodedAudio?.Dispose();
    }
}
