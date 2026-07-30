using AI.Base;

public class State
{
    public int StateType
    {
        get; protected set;
    }
    public IAgent Agent
    {
        get; set;
    }
    //状态机
    protected StateMachine m_stateMachine;
    public void SetStateMachine(StateMachine m)
    {
        m_stateMachine = m;
    }
    public virtual void Enter() { }
    public virtual State Execute() { return null; }
    public virtual void Exit() { }
}


