using LudicWorlds;
using UnityEngine;
using Unity.InferenceEngine;



public class RunSpectroState : WhisperState
{
    /*
        Here are the typical layers/steps in such an ONNX model:

        1. Input Layer: Accepts raw audio waveform data.
        2. Pre-emphasis(optional) : Applies a filter to emphasize high frequencies.
        3. Framing: Divides the audio signal into overlapping frames.
        4. Windowing: Applies a window function to each frame to mitigate spectral leakage.
        5. Short-Time Fourier Transform(STFT) : Converts each frame from the time domain to the frequency domain.
        6. Magnitude Spectrum: Computes the magnitude of the frequency components for each frame.
        7. Mel Filter Bank: Applies a set of filters to map the frequencies to the Mel scale.
        8. Logarithm: Takes the logarithm of the Mel-scaled spectrogram to compress the dynamic range.
        9. Output Layer: Produces the final Log Mel Spectrogram.

        !!! Our model appears to have 22 layers however !!!
    */

    // Start is called before the first frame update
    public RunSpectroState(IStateMachine<WhisperStateID> stateMachine) : base(stateMachine, WhisperStateID.RUN_SPECTRO, WhisperStateID.RUN_ENCODER)
    {
    }

    public override void Enter()
    {
        Debug.Log("-> RunSpectroState::Enter()");
        DebugPanel.UpdateSttState("RUN_SPECTRO");
        _stage = 0;
    }
 
    public override void Update()
    {
        switch (_stage)
        {
            case 0:
                RunSpectro();
                _stage = 1;
                break;
            default:
                _stateMachine.SetState(_nextStateId);
                break;
        }
    }
    
    private void RunSpectro()
    {
        // Run spectrogram inference on GPU
        _whisper.spectrogram.Schedule(_whisper.AudioInput);
        // Keep the output on GPU - no CPU readback
        _whisper.SpectroOutput = _whisper.spectrogram.PeekOutput() as Tensor<float>;
    }

    public override void Exit()
    {
        // SpectroOutput will be disposed by RunEncoderState after it's done processing
        base.Exit();
    }
}
