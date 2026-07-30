using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using AI.Base;

public interface IProjectorAgent : IAgent
{
    Transform transform{ get; }
    bool IsMoving { get; }
    bool IsActive { get; }
    void InitializeProjector(IMergeController mergeController, int startIndex, bool flowsRight, RaySearch ray, Vector3 normal, Vector3 startPos);
    bool ReadInput(out int dir);
    EProjectorStateType RequestInput(int dir);
    void ExecuteMove();
    void ExecuteRotate();
    void EnableInput(bool merge);
}

public class ProjectorAgent : MonoBehaviour, IProjectorAgent
{
    [Header("Public引用")]
    public Transform pivot;
    public Transform lineRef1, lineRef2;
    [Space]
    [Header("投影移动参数")]
    public float moveSpeed = 3;
    public float rotateSpeed = 2;
    public float cooldown = 1.5f;//MARKER 防止融合后连按瞬间复原
    public float rotationLerp;

    private IMergeController mergeController;
    private RaySearch m_search;
    private Animator m_animator;
    private Quaternion m_startRot;
    private Quaternion m_endRot;
    private Vector3 m_targetNormal;
    private Vector3 m_savedNormal;
    private Vector3 m_originPos;
    private Vector3 m_targetPos;
    private int m_currentIndex;
    private float m_activationTime;
    private bool m_hasPendingInput;
    private bool m_isMoving;
    private bool m_isActive;//该脚本于场景中一直激活，它FSM在Update接受输入，得拦住它不能够执行后面逻辑
    private bool m_canAcceptInput;//避免融合之后PA和WM两边对于DecalProjector组件的移动操控进行打架

    public bool isListOrderGoingRight;
    public bool IsMoving => m_isMoving;
    public bool IsActive => m_isActive;


    public void OnStart()
    {
        m_animator = GetComponentInChildren<Animator>();
    }

    public bool ReadInput(out int dir)
    {
        dir = 0;
        if (!m_canAcceptInput)
        {
            return false;
        }
        if (Input.GetKeyDown(KeyCode.Space))
        {
            dir = 3;//MARKER 复原硬编码返回 3，意思是切换为 3D
            return true;
        }
        float rawInput = Input.GetAxisRaw("Horizontal");
        if (Mathf.Abs(rawInput) > 0.1f)
        {
            dir = (rawInput > 0) == isListOrderGoingRight ? 1 : -1;
            return true;
        }
        return false;
    }
    public EProjectorStateType RequestInput(int dir)
    {
        m_hasPendingInput = true;
        if (dir == 1 || dir == -1)
        {
            return HandleMoveInput(dir);
        }
        else//执行复原逻辑
        {
            HandleRevertTo3D();
            return EProjectorStateType.ProjectorIdle;
        }
    }
    public void ExecuteMove()
    {
        if (!m_isMoving)
        {
            return;
        }
        MoveToWallTargetPoint();
    }
    public void ExecuteRotate()
    {
        if (!m_isMoving)
        {
            return;
        }
        RotateToTargetPoint();
    }

    public void InitializeProjector(IMergeController mergeController, int startIndex, bool flowsRight, RaySearch ray, Vector3 normal, Vector3 startPos)
    {
        this.mergeController = mergeController;
        m_search = ray;
        m_currentIndex = startIndex;
        isListOrderGoingRight = flowsRight;
        transform.position = startPos;
        transform.forward = normal;
        m_savedNormal = normal; 

        m_isActive = true;

        //在网格模式下，originPos 和 targetPos代表“当前所在的格子”和“想要去的格子”
        //初始时，让它们都等于当前位置，防止 Update 里计算距离报错
        m_originPos = startPos;
        m_targetPos = startPos;

        if (m_animator != null)
        {
            m_animator.speed = 0;
            m_animator.SetFloat("axis", 0);
            m_animator.SetTrigger("reset"); 
        }

        m_activationTime = Time.time;
    }

    public void EnableInput(bool merge)
    {
        m_canAcceptInput = merge;
    }

    #region 内部功能逻辑
    private EProjectorStateType HandleMoveInput(int dir)
    {
        m_animator.SetFloat("axis", dir);
        m_animator.speed = 1;

        int nextIndexCheck = m_currentIndex + dir;
        if (nextIndexCheck >= m_search.faceCentersXZ.Count) nextIndexCheck = 0;
        if (nextIndexCheck < 0) nextIndexCheck = m_search.faceCentersXZ.Count - 1;

        Vector2 nextXZ = m_search.faceCentersXZ[nextIndexCheck];
        Vector3 potentialTarget = new Vector3(nextXZ.x, transform.position.y, nextXZ.y);
        Vector3 dirToTarget = (potentialTarget - transform.position).normalized;

        float dot = Vector3.Dot(dirToTarget, transform.right);

        //TODO 每帧运动前先在有效输入层，调用播放音效
        if (GameManager.Instance != null)
        {
            GameManager.Instance.PlaySFX(GameSFX.ProjectorMove);
        } 

        if (Mathf.Abs(dot) > 0.98f)
        {
            m_targetPos = potentialTarget;
            m_currentIndex = nextIndexCheck;
            m_isMoving = true;
            return EProjectorStateType.ProjectorMove;
        }
        else
        {
            m_targetPos = potentialTarget;
            m_currentIndex = nextIndexCheck;
            m_isMoving = true;
            StartGridRotation(dir);
            return EProjectorStateType.ProjectorRotate;
        }  
    }

    private void StartGridRotation(float inputAxis)
    {
        Vector3 sideDirection = transform.right * -inputAxis;
        Vector3 checkOrigin = transform.position + (transform.forward * 0.1f) + (Vector3.up * 0.5f);

        bool isConcave = Physics.Raycast(checkOrigin, sideDirection, out _, 0.7f);
        m_targetNormal = isConcave ? -sideDirection : sideDirection;

        bool foundPivot = LineLineIntersection(
            out Vector3 pivotCandidate,
            transform.position,
            transform.forward,
            m_targetPos,
            m_targetNormal);

        if (!foundPivot)
        {
            //回退策略：若求交失败则直接吸附到目标，避免状态卡住。
            transform.position = m_targetPos;
            transform.forward = m_targetNormal;
            m_savedNormal = m_targetNormal;
            m_isMoving = false;
            m_hasPendingInput = false;
            return;
        }

        pivot.position = pivotCandidate;
        pivot.rotation = Quaternion.identity;
        transform.parent = pivot;

        rotationLerp = 0f;

        Vector3 v1 = transform.position - pivot.position;
        Vector3 v2 = m_targetPos - pivot.position;
        m_startRot = pivot.rotation;
        m_endRot = Quaternion.FromToRotation(v1, v2) * pivot.rotation;
    }

    private void RotateToTargetPoint()
    {
        m_animator.speed = 1;
        rotationLerp += Time.deltaTime * rotateSpeed;
        pivot.rotation = Quaternion.Slerp(m_startRot, m_endRot, rotationLerp);

        if (rotationLerp < 1f)
        {
            return;
        }

        pivot.rotation = m_endRot;
        transform.parent = null;
        transform.position = m_targetPos;
        transform.forward = m_targetNormal;
        m_savedNormal = m_targetNormal;

        rotationLerp = 0f;
        m_isMoving = false;
        m_hasPendingInput = false;

        m_animator.SetFloat("axis", 0);
        m_animator.speed = 0;
    }
    
    private void HandleRevertTo3D()
    {
        if (!m_hasPendingInput)
        {
            return;
        }
        //MARKER 一定要执行完行为才能设为false m_hasPendingInput = false;
        Vector2 centerXZ = m_search.faceCentersXZ[m_currentIndex];
        Vector3 faceCenterV3XZ = new Vector3(centerXZ.x, mergeController.transform.position.y, centerXZ.y);
        Vector3 correctNormal = m_savedNormal;

        mergeController.transform.position = faceCenterV3XZ;
        Vector3 playerFinalPos = faceCenterV3XZ + (correctNormal * 0.5f);

        //调用WallMerge复原检测的接口
        if (!mergeController.TryRevertAt(playerFinalPos, correctNormal))
        {
            //TODO 事件通知播放音效
            GameManager.Instance.PlaySFX(GameSFX.FailAciton);
            Debug.Log("行列已满，无法复原！");
            return;
        }


        transform.parent = null;
        m_isActive = false;
        m_animator.speed = 1;
        m_animator.SetFloat("axis", 0);
        m_animator.SetTrigger("reset");

        mergeController.Transition(false, playerFinalPos, transform.forward);
        m_hasPendingInput = false;
    }

    private void MoveToWallTargetPoint()
    {
        m_animator.speed = 1;
        transform.position = Vector3.MoveTowards(transform.position, m_targetPos, moveSpeed * Time.deltaTime);

        if (Vector3.Distance(transform.position, m_targetPos) < 0.001f)
        {
            transform.position = m_targetPos; 
            m_isMoving = false;
            m_hasPendingInput = false;

            m_animator.SetFloat("axis", 0);
            m_animator.speed = 0;
        }
    }
    
    #endregion

    #region 工具类

    /// <summary>
    /// 返回两条无限延伸的直线在三维空间中的交点（如果存在）
    /// </summary>
    public static bool LineLineIntersection(out Vector3 intersection, Vector3 linePoint1, Vector3 lineVec1, Vector3 linePoint2, Vector3 lineVec2)
    {

        Vector3 lineVec3 = linePoint2 - linePoint1;
        Vector3 crossVec1and2 = Vector3.Cross(lineVec1, lineVec2);
        Vector3 crossVec3and2 = Vector3.Cross(lineVec3, lineVec2);

        float planarFactor = Vector3.Dot(lineVec3, crossVec1and2);

        //is coplanar, and not parrallel
        if (Mathf.Abs(planarFactor) < 0.0001f && crossVec1and2.sqrMagnitude > 0.0001f)
        {
            float s = Vector3.Dot(crossVec3and2, crossVec1and2) / crossVec1and2.sqrMagnitude;
            intersection = linePoint1 + (lineVec1 * s);
            return true;
        }
        else
        {
            intersection = Vector3.zero;
            return false;
        }
    }

    /* private void OnDrawGizmos()
    {
        Gizmos.color = Color.blue;
        Gizmos.DrawSphere(originPos, .1f);
        Gizmos.color = Color.yellow;
        Gizmos.DrawSphere(targetPos, .1f);
        Gizmos.color = Color.red;
        Gizmos.DrawSphere(lineRef1.position, .1f);
        Gizmos.DrawRay(lineRef1.position, lineRef1.forward * 3);
        Gizmos.DrawRay(lineRef1.position, -lineRef1.forward * 3);
        Gizmos.DrawSphere(lineRef2.position, .1f);
        Gizmos.DrawRay(lineRef2.position, lineRef2.forward * 3);
        Gizmos.DrawRay(lineRef2.position, -lineRef2.forward * 3);
        Gizmos.color = Color.green;
        Vector3 inter;
        LineLineIntersection(out inter, lineRef1.position, lineRef1.forward, lineRef2.position, lineRef2.forward);
        Gizmos.DrawSphere(inter, .1f);
    } */
    #endregion

}
