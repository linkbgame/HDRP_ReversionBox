using System.Collections;
using AI.Base;
using DG.Tweening;
using UnityEngine;

//MARKER 写接口考虑 实现接口的对象被别人所调用的接口方法；或者做成状态机的话，每个状态会调用什么
public interface IPlayerAgent : IAgent
{
    GameObject gameObject { get; }
    Transform transform { get; }
    bool IsMoving { get; set; }
    bool IsActive { get; set; }

    bool ReadPlaneMoveInput(out Vector3 dir);//输入层
    void RequestPlaneMove(Vector3 dir);//请求层
    void ExecutePlaneMove();//执行层
    void FaceTo(Vector3 dir);//朝向
    void TryPlaneMove(Vector3 dir);//MARKER 写接口方法的作用 1:告诉自己这个对象的行为有什么 2:其他对象要调用这个对象的方法可以解耦 3:用范式写代码，行为最终交给状态机调用
    bool CanRevertAt(Vector3 position, Vector3 dir);
    void TryRevertMove(Vector3 position, Vector3 dir);
    void LockInput(float seconds);
}
public class PlayerAgent : MonoBehaviour, IPlayerAgent
{
    private Vector3 m_requestedMoveDir;
    private float m_horizontalAxis;
    private float m_verticalAxis;
    private float m_inputLockUntil = -1f;
    private bool m_hasPendingMove = false;
    private bool m_isMove = false;

    public float moveDuration = 0.25f;
    public float moveScaleMin = 0.85f;
    public LayerMask blockingLayer;//CORE 基于物理层级检测实现的推箱子系统
    public bool IsMoving
    {
        get => m_isMove;
        set => m_isMove = value;
    }
    public bool IsActive//MARKER 对应了PlayerFSM的激活状态
    {
        get => this.enabled;
        set => this.enabled = value;
    }
    public bool ReadPlaneMoveInput(out Vector3 dir)
    {
        dir = Vector3.zero;

        if (Time.time < m_inputLockUntil) return false;
        if (m_isMove) return false;

        m_horizontalAxis = Input.GetAxisRaw("Horizontal");
        m_verticalAxis = Input.GetAxisRaw("Vertical");

        //MARKER 轴互斥：禁止斜向
        if (m_horizontalAxis != 0f && m_verticalAxis != 0f)
        {
            m_horizontalAxis = 0f;
            m_verticalAxis = 0f;
        }
        else if (m_horizontalAxis != 0f)
        {
            m_verticalAxis = 0f;
        }
        else if (m_verticalAxis != 0f)
        {
            m_horizontalAxis = 0f;
        }
        //MARKER 没有输入
        if (m_horizontalAxis == 0f && m_verticalAxis == 0f)
        {
            return false;
        }

        dir = new Vector3(m_horizontalAxis, 0f, m_verticalAxis);
        return true;
    }
    public void RequestPlaneMove(Vector3 dir)
    {
        m_requestedMoveDir = dir;
        m_hasPendingMove = true;
        FaceTo(dir);
    }
    public void ExecutePlaneMove()
    {
        if (!m_hasPendingMove)
        {
            return;
        }
        TryPlaneMove(m_requestedMoveDir);
        m_hasPendingMove = false;

    }
    //TODO 学习帕箱优化眼睛的视觉表现
    public void FaceTo(Vector3 dir)
    {
        if (dir.x > 0f) transform.rotation = Quaternion.Euler(0, 90, 0);
        else if (dir.x < 0f) transform.rotation = Quaternion.Euler(0, -90, 0);
        else if (dir.z > 0f) transform.rotation = Quaternion.Euler(0, 0, 0);
        else if (dir.z < 0f) transform.rotation = Quaternion.Euler(0, 180, 0);
    }
    public void LockInput(float seconds)
    {
        float lockSeconds = Mathf.Max(0f, seconds);
        m_inputLockUntil = Mathf.Max(m_inputLockUntil, Time.time + lockSeconds);

        // 清理本帧/残留输入，避免锁期间带入
        m_hasPendingMove = false;
        m_horizontalAxis = 0f;
        m_verticalAxis = 0f;
    }

    public void TryPlaneMove(Vector3 dir)
    {
        if (m_isMove)
        {
            return;
        }

        Vector3 targetPos = transform.position + dir;
        Collider[] hitColliders = Physics.OverlapBox(targetPos, new Vector3(0.4f, 0.4f, 0.4f), Quaternion.identity, blockingLayer);

        if (hitColliders.Length == 0)
        {
            StartCoroutine(ExecuteMove(targetPos));
            return;
        }

        PushableEntity box = hitColliders[0].GetComponent<PushableEntity>();
        if (box != null && box.gameObject.layer == LayerMask.NameToLayer("YellowBox") && box.CanPush(dir))
        {
            PushChain(targetPos, dir);
            StartCoroutine(ExecuteMove(targetPos));
        }
        else
        {
            //TODO 反馈无法移动（碰撞音效）
        }
    }

    public void TryRevertMove(Vector3 position, Vector3 dir)
    {
        Collider[] hitColliders = Physics.OverlapBox(position, new Vector3(0.4f, 0.4f, 0.4f), Quaternion.identity, blockingLayer);
        if (hitColliders.Length == 0)
        {
            return;
        }
        PushableEntity box = hitColliders[0].GetComponent<PushableEntity>();
        int mask = (1 << LayerMask.NameToLayer("YellowBox")) | (1 << LayerMask.NameToLayer("BlueBox"));
        if (box != null && (mask & (1 << box.gameObject.layer)) != 0 && box.RecoverCanPush(dir))
        {
            PushChain(position, dir);
        }       
    }

    public bool CanRevertAt(Vector3 position, Vector3 dir)
    {
        Collider[] hitColliders = Physics.OverlapBox(position, new Vector3(0.4f, 0.4f, 0.4f), Quaternion.identity, blockingLayer);
        if (hitColliders.Length == 0)
        {
            return true;
        }
        PushableEntity box = hitColliders[0].GetComponent<PushableEntity>();
        int mask = (1 << LayerMask.NameToLayer("YellowBox")) | (1 << LayerMask.NameToLayer("BlueBox"));
        if (box != null && (mask & (1 << box.gameObject.layer)) != 0)
        {
            return box.RecoverCanPush(dir);
        } 
        return false;
    }

    private void PushChain(Vector3 position, Vector3 direction)
    {
        Collider[] hits = Physics.OverlapBox(position, new Vector3(0.4f, 0.4f, 0.4f), Quaternion.identity, blockingLayer);
        if (hits.Length == 0)
        {
            return;
        }
        PushableEntity box = hits[0].GetComponent<PushableEntity>();
        if (box == null)
        {
            return;
        }
        PushChain(position + direction, direction);
        box.Move(direction);
    }

    /* private IEnumerator ExecuteMove(Vector3 target)
    {
        m_isMove = true;
        if (GameManager.Instance != null)
        {
            GameManager.Instance.PlaySFX(GameSFX.Move);
        } 

        float elapsed = 0f;
        while (elapsed < moveDuration)
        {
            float t = elapsed / moveDuration;
            transform.localPosition = Vector3.Lerp(transform.position, target, t);//TODO 父对象移动和插值起点
            elapsed += Time.deltaTime;
            yield return null;
        }
        transform.localPosition = target;
        m_isMove = false;

        if (GameManager.Instance != null)
        {
            GameManager.Instance.CheckAllPlates();
        }        
    } */

    //TODO 优化主角场景对象层级关系
    private IEnumerator ExecuteMove(Vector3 target)
    {
        //STEP 1 更新标志同时播放音效
        m_isMove = true;
        if (GameManager.Instance != null)
        {
            GameManager.Instance.PlaySFX(GameSFX.Move);
        }

        //STEP 2 数据处理，目前是直接拿到脚本挂载对象的tranform
        Transform visual = transform;
        Vector3 baseScale = visual.localScale;
        //Vector3 baseScale = transform.localScale;
        float half = moveDuration * 0.5f;

        //STEP 3 DOTween的位移和缩放序列
        Sequence seq = DOTween.Sequence();
        seq.Join(transform.DOLocalMove(target, moveDuration).SetEase(Ease.OutSine));
        seq.Join(
            DOTween.Sequence()
                .Append(visual.DOScale(baseScale * moveScaleMin, half).SetEase(Ease.OutQuad))
                .Append(visual.DOScale(baseScale, half).SetEase(Ease.InQuad))
        );

        //STEP 4 后续数据处理
        bool done = false;
        seq.OnComplete(() =>
        {
            transform.localPosition = target; // 保证落点准确
            visual.localScale = baseScale;    // 保证缩放复位
            done = true;
        });
        yield return seq.WaitForCompletion();
        if (!done)
        {
            transform.localPosition = target;
            visual.localScale = baseScale;
        }

        m_isMove = false;
        if (GameManager.Instance != null)
        {
            GameManager.Instance.CheckAllPlates();//CORE 检测机关是否激活的逻辑，放在玩家每次走完一步的地方
        }
    }
}
