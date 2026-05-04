using LudicWorlds;
using Unity.InferenceEngine;
using UnityEngine;


public class UnderstandInstructionState : WhisperState
{
    protected EventBroker _eventBroker;

    private float[] _similarityScores = new float[3];
    //0 -> Fridge, 1 -> Sofa, 2 -> Table
    private string[] _locationLabels = new string[3];



    public UnderstandInstructionState(IStateMachine<WhisperStateID> stateMachine) : base(stateMachine, WhisperStateID.UNDERSTAND_INSTRUCTION, WhisperStateID.READY)
    {
        _eventBroker = EventBroker.GetInstance();

        _locationLabels[0] = LocationLabel.FRIDGE;
        _locationLabels[1] = LocationLabel.SOFA;
        _locationLabels[2] = LocationLabel.TABLE;
    }

    public override void Enter()
    {
        //Debug.Log("-> DetermineInstructionState::Enter()");
        DebugPanel.UpdateSttState(WhisperStateID.UNDERSTAND_INSTRUCTION.ToString());
        _stage = 0;
        _whisper.Instruction = "";
        DetermineInstruction();
    }

    public override void Update()
    {
        _stateMachine.SetState(_nextStateId);
    }


    private void DetermineInstruction()
    {
        if (_whisper.MiniLM == null)
        {
            Debug.LogError("-> UnderstandInstructionState::DetermineInstruction() - _whisper.MiniLM = null! :(");
            return;
        }


        string transcription = _whisper.Transcription.ToLower();
        string instruction = "";

        var transcription_tokens = _whisper.MiniLM.GetTokens(transcription);

        using Tensor<float> transcription_embedding = _whisper.MiniLM.GetEmbedding(transcription_tokens);

        //Get Sentence Similarity Scores
        for (int i = 0; i < _similarityScores.Length; i++)
        {
            _similarityScores[i] = _whisper.MiniLM.GetDotScore(i, transcription_embedding);
        }

        //Find best and second-best. Reference phrases share the same "Go to X" frame, so a misheard
        //transcription often scores similarly against all locations — require a margin to reject ties.
        int maxIndex = 0;
        int secondIndex = -1;
        for (int i = 1; i < _similarityScores.Length; i++)
        {
            if (_similarityScores[i] > _similarityScores[maxIndex])
            {
                secondIndex = maxIndex;
                maxIndex = i;
            }
            else if (secondIndex == -1 || _similarityScores[i] > _similarityScores[secondIndex])
            {
                secondIndex = i;
            }
        }

        float highestScore = _similarityScores[maxIndex];
        float margin = highestScore - _similarityScores[secondIndex];

        if (highestScore > 0.4f && margin > 0.05f)
        {
            instruction = _locationLabels[maxIndex];

            Debug.Log("====> Loc: " + instruction + ", SS Score=" + highestScore);
        }
        else
        {
            //We can't determine a high enough 'Sentence Similarity', so let's use a hard-coded sub-string comparision as a fallback...

            if (transcription.Contains("rid") || transcription.Contains("each") || transcription.Contains("french")) //i.e. "fridge" or "bridge" or "reach"
            {
                instruction = LocationLabel.FRIDGE;
            }
            else if (transcription.Contains("afe") || transcription.Contains("ofa") || transcription.Contains("so far")) //"sofa" or "safe"
            {
                instruction = LocationLabel.SOFA;
            }
            else if (transcription.Contains("able")) //"table"
            {
                instruction = LocationLabel.TABLE;
            }

            if (instruction.Length > 0)
            {
                Debug.Log("[DetermineInstruction] Loc: " + instruction);
            }
        }

        _whisper.Instruction = instruction;

        InstructionEventArgs args = new InstructionEventArgs(instruction);
        _eventBroker.DispatchEvent(EventID.INSTRUCTION_READY, args);
    }


    public override void Exit()
    {
        base.Exit();
    }

    public override void Dispose()
    {
        _eventBroker = null;
        base.Dispose();
    }
}
