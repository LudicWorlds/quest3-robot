using LudicWorlds;

public class RobotState : GameObjectState<RobotStateID>
{
    protected RobotController _ctrl;
    protected int _stage = 0;
    protected float _elapsedTime = 0f;
    protected RobotStateID _nextStateId;

    public RobotState(IStateMachine<RobotStateID> stateMachine, RobotStateID id) : base(stateMachine, id)
    {
        _ctrl = stateMachine as RobotController;
        _stage = 0;
    }

    public override void Enter() { }

    public override void Update() { }

    public override void Exit() { }

    public override void Dispose()
    {
        _ctrl = null;
        base.Dispose();
    }
}
