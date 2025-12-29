using LudicWorlds;


public class UnderstandInstructionState : WhisperState
{
    protected EventBroker _eventBroker;

    public UnderstandInstructionState(IStateMachine<WhisperStateID> stateMachine) : base(stateMachine, WhisperStateID.UNDERSTAND_INSTRUCTION, WhisperStateID.READY)
    {
        _eventBroker = EventBroker.GetInstance();
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
        string transcription = _whisper.Transcription.ToLower();
        string instruction = "";

        if (transcription.Contains("rid") || transcription.Contains("each") || transcription.Contains("french")) //i.e. "fridge" or "bridge" or "reach"
        {
            instruction = LocationLabel.FRIDGE;
        }
        else if (transcription.Contains("afe") || transcription.Contains("ofa") || transcription.Contains("so far")) //"sofa" or "safe"
        {
            instruction = LocationLabel.SOFA;
        }
        else if(transcription.Contains("able")) //"table"
        {
            instruction = LocationLabel.TABLE;
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
