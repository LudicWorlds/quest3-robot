using System;
using LudicWorlds;
using UnityEngine;

public class InitRobotState : RobotState
{
    private EventBroker _eventBroker;
    private bool _spatialAnchorsLoaded = false;

    public InitRobotState(IStateMachine<RobotStateID> stateMachine, RobotStateID id)
        : base(stateMachine, id)
    {
        _eventBroker = EventBroker.GetInstance();
    }

    public override void Enter()
    {
        Debug.Log("-> InitRobotState::Enter()");
        DebugPanel.UpdateRobotState("INIT - Loading spatial anchors...");
        _stage = 0;
        _elapsedTime = 0f;
        _nextStateId = RobotStateID.STATIONARY;
        _spatialAnchorsLoaded = false;

        // Subscribe to spatial anchors loaded event
        _eventBroker.Events[EventID.SPATIAL_ANCHORS_LOADED] += OnSpatialAnchorsLoaded;

        // Trigger loading of spatial anchors
        _ = _ctrl.StartCoroutine(_ctrl.LocAnchorMgr.LoadAllSpatialAnchors());
    }

    public override void Update()
    {
        // Wait for spatial anchors to be loaded
        if (_spatialAnchorsLoaded)
        {
            Debug.Log("-> InitRobotState: Spatial anchors loaded, transitioning to STATIONARY");
            _stateMachine.SetState(_nextStateId);
        }
    }

    public override void Exit()
    {
        Debug.Log("-> InitRobotState::Exit()");

        // Unsubscribe from event
        _eventBroker.Events[EventID.SPATIAL_ANCHORS_LOADED] -= OnSpatialAnchorsLoaded;
    }

    private void OnSpatialAnchorsLoaded(object sender, EventArgs e)
    {
        Debug.Log("-> InitRobotState: Received SPATIAL_ANCHORS_LOADED event");
        _spatialAnchorsLoaded = true;
    }

    public override void Dispose()
    {
        _eventBroker = null;
        base.Dispose();
    }
}
