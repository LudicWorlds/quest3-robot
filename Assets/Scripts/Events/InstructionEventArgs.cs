using System;

public class InstructionEventArgs : EventArgs
{
    protected string _instruction;

    public string Instruction
    {
        get { return _instruction; }
    }


    public InstructionEventArgs(string instruction)
    {
        _instruction = instruction;
    }
}
