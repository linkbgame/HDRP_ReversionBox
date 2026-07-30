using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public enum EProjectorStateType
{
    ProjectorIdle,
    ProjectorMove,
    ProjectorRotate
}
[System.Serializable]
public class StateDebugEntry
{
    public EProjectorStateType stateType;   // Inspector 可选
    public string stateName;                // 给你看字符串
}

class ProjectorIdleState : State
{
    private IProjectorAgent Projector => (IProjectorAgent)Agent;
    public ProjectorIdleState()
    {
        StateType = (int)EProjectorStateType.ProjectorIdle;
    }
    public override State Execute()
    {
        if (!Projector.IsActive)
        {
            return this;
        }
        if (!Projector.ReadInput(out var dir))
        {
            //Debug.LogWarning("<color=yellow><b>当前是投影器待机状态</b></color>");         
            return this;  
        }

        return m_stateMachine.Transition((int)Projector.RequestInput(dir));
    }
}

class ProjectorMoveState : State
{
    private IProjectorAgent Projector => (IProjectorAgent)Agent;
    public ProjectorMoveState()
    {
        StateType = (int)EProjectorStateType.ProjectorMove;
    }
    public override void Enter()
    {
        Projector.ExecuteMove();
    }
    public override State Execute()
    {
        Projector.ExecuteMove();
        if (Projector.IsMoving)
        {
            //Debug.LogWarning("<color=cyan><b>当前是投影器移动状态</b></color>");
            return this;
        }
        return m_stateMachine.Transition((int)EProjectorStateType.ProjectorIdle);
    }
}
class ProjectorRotateState : State
{
    private IProjectorAgent Projector => (IProjectorAgent)Agent;
    public ProjectorRotateState()
    {
        StateType = (int)EProjectorStateType.ProjectorRotate;
    }
    public override void Enter()
    {
        Projector.ExecuteRotate();
    }
    public override State Execute()
    {
        Projector.ExecuteRotate();
        if (Projector.IsMoving)
        {
            //Debug.LogWarning("<color=green><b>当前是投影器移动状态</b></color>");
            return this;
        }
        return m_stateMachine.Transition((int)EProjectorStateType.ProjectorIdle);
    }
}
public class ProjectorFSM : ProjectorAgent
{
    [SerializeField] private List<StateDebugEntry> stateDebug = new();
    [SerializeField] private string currentStateName;

    private StateMachine m_FSM;
    private void Start()
    {
        OnStart();
        m_FSM = new StateMachine(this);
        m_FSM.AddState(new ProjectorIdleState());
        m_FSM.AddState(new ProjectorMoveState());
        m_FSM.AddState(new ProjectorRotateState());
        m_FSM.SetDefaultState((int)EProjectorStateType.ProjectorIdle);
    }
    private void Update()
    {
        m_FSM.Update();   
        int i = m_FSM.ReturnCurrentStateInt();
        currentStateName = ((EProjectorStateType)i).ToString();
    }

    private void BuildStateDebugList()
    {
        stateDebug.Clear();
        foreach (EProjectorStateType e in System.Enum.GetValues(typeof(EProjectorStateType)))
        {
            stateDebug.Add(new StateDebugEntry
            {
                stateType = e,
                stateName = e.ToString()
            });
        }
    }
}
