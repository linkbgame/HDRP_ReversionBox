using AI.Base;
using System.Collections.Generic;
using UnityEngine;

public class StateMachine
{
    private IAgent m_agent;
    private Dictionary<int, State> m_states = new Dictionary<int, State>();
    private State m_currentState;
    public StateMachine(IAgent agent)
    {
        m_agent = agent;
    }
    public void AddState(State s)
    {
        s.Agent = m_agent;
        s.SetStateMachine(this);
        m_states[s.StateType] = s;
    }
    public State Transition(int t)
    {
        if (m_states.TryGetValue(t, out var s))
        {
            return s;
        }
        return m_currentState;
    }
    public void SetDefaultState(int t)
    {
        if (m_states.TryGetValue(t, out m_currentState))
        {
            m_currentState.Enter();
        }
    }
    public int ReturnCurrentStateInt()
    {
        return m_currentState.StateType;
    }
    public void Update()
    {
        if (m_currentState == null)
        {
            return;
        }
        State nextState = m_currentState.Execute();
        if (nextState != m_currentState)
        {
            m_currentState.Exit();
            m_currentState = nextState;
            if (m_currentState != null)
            {
                m_currentState.Enter();
            }
        }
    }
}
