using LudicWorlds;
using System.Text;
using Unity.Collections;
using Unity.InferenceEngine;
using UnityEngine;

public class RunDecoderState : WhisperState
{
    // Special tokens see added tokens file for details
    private const int END_OF_TEXT = 50257;
    private const int START_OF_TRANSCRIPT = 50258;
    private const int ENGLISH = 50259;
    private const int GERMAN = 50261;
    private const int FRENCH = 50265;
    private const int TRANSCRIBE = 50359; //for speech-to-text in specified language
    private const int TRANSLATE = 50358;  //for speech-to-text then translate to English
    private const int NO_TIME_STAMPS = 50363;
    private const int START_TIME = 50364;

    // This is how many tokens you want. It can be adjusted.
    private const int _maxTokens = 100;

    private int _tokenCount = 0;
    private NativeArray<int> _outputTokens;
    private string _outputString = "";

    // Pinned tensors for efficient GPU data transfer
    private NativeArray<int> _lastToken;
    private Tensor<int> _lastTokenTensor;
    private Tensor<int> _tokensTensor;

    private Awaitable _inferenceAwaitable;

    // Start is called before the first frame update
    public RunDecoderState(IStateMachine<WhisperStateID> stateMachine) : base(stateMachine, WhisperStateID.RUN_DECODER, WhisperStateID.UNDERSTAND_INSTRUCTION)
    {
    }

    public override void Enter()
    {
        Debug.Log("-> RunDecoderState::Enter()");
        DebugPanel.UpdateSttState("RUN_DECODER");
        _stage = 0;

        // Initialize output tokens array
        _outputTokens = new NativeArray<int>(_maxTokens, Allocator.Persistent);
        _outputTokens[0] = START_OF_TRANSCRIPT;
        _outputTokens[1] = ENGLISH;// GERMAN;//FRENCH;//
        _outputTokens[2] = TRANSCRIBE; //TRANSLATE;//
        _tokenCount = 3;
        _outputString = "";

        // Create and pin tensors for efficient GPU data transfer
        _tokensTensor = new Tensor<int>(new TensorShape(1, _maxTokens));
        ComputeTensorData.Pin(_tokensTensor);
        _tokensTensor.Reshape(new TensorShape(1, _tokenCount));
        _tokensTensor.dataOnBackend.Upload<int>(_outputTokens, _tokenCount);

        _lastToken = new NativeArray<int>(1, Allocator.Persistent);
        _lastToken[0] = NO_TIME_STAMPS;
        _lastTokenTensor = new Tensor<int>(new TensorShape(1, 1), new[] { NO_TIME_STAMPS });
    }


    public override void Update()
    {
        switch (_stage)
        {
            case 0:
                // Run inference step (decoder1 + decoder2)
                if (_tokenCount < _maxTokens - 1)
                {
                    _inferenceAwaitable = InferenceStep();
                    _stage = 1;
                }
                else
                {
                    // Max tokens reached
                    FinishTranscription();
                }
                break;
            case 1:
                // Wait for async inference to complete
                if (_inferenceAwaitable.IsCompleted)
                {
                    _stage = 0; // Go back to run another inference step
                }
                break;
            default:
                _stateMachine.SetState(_nextStateId);
                break;
        }
    }

    private async Awaitable InferenceStep()
    {
        var decoder1 = _whisper.decoder1;
        var decoder2 = _whisper.decoder2;


        // Step 1: decoder1 processes all tokens and outputs key-value pairs
        decoder1.SetInput("input_ids", _tokensTensor);
        decoder1.SetInput("encoder_hidden_states", _whisper.EncodedAudio);
        decoder1.Schedule();

        // Step 2: Get key-value pairs from decoder1
        var past_key_values_0_decoder_key = decoder1.PeekOutput("present.0.decoder.key") as Tensor<float>;
        var past_key_values_0_decoder_value = decoder1.PeekOutput("present.0.decoder.value") as Tensor<float>;
        var past_key_values_1_decoder_key = decoder1.PeekOutput("present.1.decoder.key") as Tensor<float>;
        var past_key_values_1_decoder_value = decoder1.PeekOutput("present.1.decoder.value") as Tensor<float>;
        var past_key_values_2_decoder_key = decoder1.PeekOutput("present.2.decoder.key") as Tensor<float>;
        var past_key_values_2_decoder_value = decoder1.PeekOutput("present.2.decoder.value") as Tensor<float>;
        var past_key_values_3_decoder_key = decoder1.PeekOutput("present.3.decoder.key") as Tensor<float>;
        var past_key_values_3_decoder_value = decoder1.PeekOutput("present.3.decoder.value") as Tensor<float>;

        var past_key_values_0_encoder_key = decoder1.PeekOutput("present.0.encoder.key") as Tensor<float>;
        var past_key_values_0_encoder_value = decoder1.PeekOutput("present.0.encoder.value") as Tensor<float>;
        var past_key_values_1_encoder_key = decoder1.PeekOutput("present.1.encoder.key") as Tensor<float>;
        var past_key_values_1_encoder_value = decoder1.PeekOutput("present.1.encoder.value") as Tensor<float>;
        var past_key_values_2_encoder_key = decoder1.PeekOutput("present.2.encoder.key") as Tensor<float>;
        var past_key_values_2_encoder_value = decoder1.PeekOutput("present.2.encoder.value") as Tensor<float>;
        var past_key_values_3_encoder_key = decoder1.PeekOutput("present.3.encoder.key") as Tensor<float>;
        var past_key_values_3_encoder_value = decoder1.PeekOutput("present.3.encoder.value") as Tensor<float>;

        // Step 3: decoder2 processes last token + key-value pairs
        decoder2.SetInput("input_ids", _lastTokenTensor);
        decoder2.SetInput("past_key_values.0.decoder.key", past_key_values_0_decoder_key);
        decoder2.SetInput("past_key_values.0.decoder.value", past_key_values_0_decoder_value);
        decoder2.SetInput("past_key_values.1.decoder.key", past_key_values_1_decoder_key);
        decoder2.SetInput("past_key_values.1.decoder.value", past_key_values_1_decoder_value);
        decoder2.SetInput("past_key_values.2.decoder.key", past_key_values_2_decoder_key);
        decoder2.SetInput("past_key_values.2.decoder.value", past_key_values_2_decoder_value);
        decoder2.SetInput("past_key_values.3.decoder.key", past_key_values_3_decoder_key);
        decoder2.SetInput("past_key_values.3.decoder.value", past_key_values_3_decoder_value);

        decoder2.SetInput("past_key_values.0.encoder.key", past_key_values_0_encoder_key);
        decoder2.SetInput("past_key_values.0.encoder.value", past_key_values_0_encoder_value);
        decoder2.SetInput("past_key_values.1.encoder.key", past_key_values_1_encoder_key);
        decoder2.SetInput("past_key_values.1.encoder.value", past_key_values_1_encoder_value);
        decoder2.SetInput("past_key_values.2.encoder.key", past_key_values_2_encoder_key);
        decoder2.SetInput("past_key_values.2.encoder.value", past_key_values_2_encoder_value);
        decoder2.SetInput("past_key_values.3.encoder.key", past_key_values_3_encoder_key);
        decoder2.SetInput("past_key_values.3.encoder.value", past_key_values_3_encoder_value);

        decoder2.Schedule();

        // Step 4: Get logits and find the token with highest probability
        var logits = _whisper.decoder2.PeekOutput("logits") as Tensor<float>;
        _whisper.argmax.Schedule(logits);

        // Step 5: Async readback of the token
        using var tokenTensor = await _whisper.argmax.PeekOutput().ReadbackAndCloneAsync() as Tensor<int>;
        int index = tokenTensor[0];

        // Step 6: Update token arrays
        _outputTokens[_tokenCount] = _lastToken[0];
        _lastToken[0] = index;
        _tokenCount++;
        _tokensTensor.Reshape(new TensorShape(1, _tokenCount));
        _tokensTensor.dataOnBackend.Upload<int>(_outputTokens, _tokenCount);
        _lastTokenTensor.dataOnBackend.Upload<int>(_lastToken, 1);

        // Step 7: Process the new token
        ProcessToken(index);
    }

    private void ProcessToken(int tokenID)
    {
        if (tokenID == END_OF_TEXT)
        {
            FinishTranscription();
        }
        else if (tokenID >= _whisper.Tokens.Length)
        {
            _outputString += $"(time={(tokenID - START_TIME) * 0.02f})";
            Debug.Log(_outputString);
        }
        else
        {
            _outputString += GetUnicodeText(_whisper.Tokens[tokenID]);
            Debug.Log("-> " + _outputString);
        }
    }

    private void FinishTranscription()
    {
        Debug.Log("*WHISPER*: " + _outputString);
        _stage = 3; // Move to final stage

        if (_whisper.SpeechText != null)
        {
            _whisper.SpeechText.color = new Color(1f, 0.6133823f, 0f);
            _whisper.SpeechText.text = _outputString;
            _whisper.Transcription = _outputString;
        }
        else
        {
            Debug.LogError("-> RunDecoderState::Update() - SpeechText is NULL! :(");
        }
    }

    // Translates encoded special characters to Unicode
    string GetUnicodeText(string text)
    {
        var bytes = Encoding.GetEncoding("ISO-8859-1").GetBytes(ShiftCharacterDown(text));
        return Encoding.UTF8.GetString(bytes);
    }

    string ShiftCharacterDown(string text)
    {
        string outText = "";
        foreach (char letter in text)
        {
            outText += ((int)letter <= 256) ? letter :
                (char)_whisper.WhiteSpaceCharacters[(int)(letter - 256)];
        }
        return outText;
    }

    public override void Exit()
    {
        // Dispose of all tensors and native arrays
        _tokensTensor?.Dispose();
        _lastTokenTensor?.Dispose();
        _whisper.EncodedAudio?.Dispose();

        if (_outputTokens.IsCreated)
            _outputTokens.Dispose();
        if (_lastToken.IsCreated)
            _lastToken.Dispose();

        base.Exit();
    }
}
