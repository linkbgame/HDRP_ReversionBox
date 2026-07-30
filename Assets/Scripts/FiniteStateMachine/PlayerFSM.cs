using System.Collections;
using System.Collections.Generic;
using AI.Base;
using UnityEngine;

enum EPlayerStateType
{
    PlaneIdle,
    PlaneMove
}
class PlaneIdleState : State
{
    private IPlayerAgent Player => (IPlayerAgent)Agent;
    public PlaneIdleState()
    {
        StateType = (int)EPlayerStateType.PlaneIdle;
    }
    public override State Execute()
    {
        if (!Player.ReadPlaneMoveInput(out var dir))
        {
            return this;
        }

        Player.RequestPlaneMove(dir);
        return m_stateMachine.Transition((int)EPlayerStateType.PlaneMove);
    }
}
class PlaneMoveState : State
{
    private IPlayerAgent Player => (IPlayerAgent)Agent;
    public PlaneMoveState()
    {
        StateType = (int)EPlayerStateType.PlaneMove;
    }
    public override void Enter()
    {
        Player.ExecutePlaneMove();
    }  
    public override State Execute()
    {
        if (Player.IsMoving)
        {
            return this;
        }
        return m_stateMachine.Transition((int)EPlayerStateType.PlaneIdle);
    }
}

public class PlayerFSM : PlayerAgent
{
    private StateMachine m_FSM;
    private void Start()
    {
        m_FSM = new StateMachine(this);
        m_FSM.AddState(new PlaneIdleState());
        m_FSM.AddState(new PlaneMoveState());
        m_FSM.SetDefaultState((int)EPlayerStateType.PlaneIdle);
    }
    private void Update()
    {
        m_FSM.Update();
    }
    //思路1 作为基类，实现移动玩法？
    //思路2 玩家拿到其他功能模块的引用，接下来状态通过代理人获取这些模块的引用，来实现状态的功能
    //思路3 PlaneIdleState如果把平面移动输入层放进去，检测到输入并输出移动向量，那么在FSM的框架下，该如何让PlaneMoveState拿到这个移动向量来实现移动功能（状态之间的通信？参数传递？事件通知？）
    //当下实现思路：状态机只负责宏观调用某个状态会执行的方法，方法具体细节实现交给继承了核心功能父类，子类继承父类然后创建状态机来管理方法调用，父类提供方法接口
}
