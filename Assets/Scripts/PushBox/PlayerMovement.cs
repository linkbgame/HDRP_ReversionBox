using UnityEngine;
using System.Collections;

public class PlayerMovement : MonoBehaviour
{
    private Vector3 m_velocity;
    private float m_horizontalAxis;
    private float m_verticalAxis;
    private bool m_isMove = false;

    public float moveDuration = 0.2f;
    public LayerMask blockingLayer;

    private void Update()
    {
        if (m_isMove)
        {
            return;
        }

        HandleInput();
    }


    private void HandleInput()
    {
        m_horizontalAxis = Input.GetAxisRaw("Horizontal");
        m_verticalAxis = Input.GetAxisRaw("Vertical");

        //MARKER 若两轴同时有输入，则取消移动（可以阻止斜向或优先冲突）
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

        if (m_horizontalAxis != 0 || m_verticalAxis != 0)
        {
            m_velocity = new Vector3(m_horizontalAxis, 0, m_verticalAxis);
            TryMove(m_velocity);
        }

        //MARKER 从上往下不满足条件挨个检查一遍
        if (m_velocity.x > 0f)
        {
            transform.rotation = Quaternion.Euler(0, 90, 0);
        }  
        else if (m_velocity.x < 0f)
        {
            transform.rotation = Quaternion.Euler(0, -90, 0);
        }  
        else if (m_velocity.z > 0f)
        {
            transform.rotation = Quaternion.Euler(0, 0, 0);
        } 
        else if (m_velocity.z < 0f)
        {
            transform.rotation = Quaternion.Euler(0, 180, 0);
        }      
    }

    //CORE PlayerMovement <- WallMerge <- ProjectorMovement 在墙上检测能否复原
    public bool CanRecoverAt(Vector3 position, Vector3 direction)
    {
        //STEP 1 探测目标点是否有东西
        Collider[] hitColliders = Physics.OverlapBox(position, new Vector3(0.4f, 0.4f, 0.4f), Quaternion.identity, blockingLayer);

        if (hitColliders.Length == 0)
        {
            return true;
        }

        //STEP 2 如果有东西，检查是不是能推的箱子
        PushableEntity box = hitColliders[0].GetComponent<PushableEntity>();
        int mask = (1 << LayerMask.NameToLayer("YellowBox")) | (1 << LayerMask.NameToLayer("BlueBox"));

        if (box != null && (mask & (1 << box.gameObject.layer)) != 0)
        {
            return box.RecoverCanPush(direction);
        }

        //STEP 3 墙壁则不能复原
        return false;
    }

    /// <summary>
    /// 复原推力推箱子，传进来的就是角色复原的位置，直接判断有没有箱子，再使用法线判断
    /// </summary>
    public void TryRecoverMove(Vector3 position, Vector3 direction)
    {
        Vector3 targetPos = position;
        Collider[] hitColliders = Physics.OverlapBox(targetPos, new Vector3(0.4f, 0.4f, 0.4f), Quaternion.identity, blockingLayer);
        if (hitColliders.Length == 0)
        {
            return;
        }
        else
        {
            //Debug.Log("进入到了复原推箱子");
            //Debug.Log($"Hit object: {hitColliders[0].gameObject.name}", hitColliders[0].gameObject);
            PushableEntity box = hitColliders[0].GetComponent<PushableEntity>();
            int mask = (1 << LayerMask.NameToLayer("YellowBox")) | (1 << LayerMask.NameToLayer("BlueBox"));
            if (box != null && (mask & (1 << box.gameObject.layer)) != 0)
            {
                if (box.RecoverCanPush(direction))
                {
                    PushChain(targetPos, direction);
                }
            }
        }
    }

    /// <summary>
    /// 角色普通移动的处理
    /// </summary>
    private void TryMove(Vector3 direction)
    {
        Vector3 targetPos = transform.position + direction;
        Collider[] hitColliders = Physics.OverlapBox(targetPos, new Vector3(0.4f, 0.4f, 0.4f), Quaternion.identity, blockingLayer);

        if (hitColliders.Length == 0)
        {
            StartCoroutine(ExecuteMove(targetPos));
        }
        else
        {
            PushableEntity box = hitColliders[0].GetComponent<PushableEntity>();
            if (box != null && box.gameObject.layer == LayerMask.NameToLayer("YellowBox"))
            {
                if (box.CanPush(direction))
                {
                    PushChain(targetPos, direction);
                    StartCoroutine(ExecuteMove(targetPos));
                }
            }
            else
            {
                //TODO 播放无法移动的音效
            }
        }
    }

    //MARKER 先让远处箱子移动，再让近处箱子移动，最后让玩家移动，像链条一样
    private void PushChain(Vector3 position, Vector3 direction)
    {
        Collider[] hits = Physics.OverlapBox(position, new Vector3(0.4f, 0.4f, 0.4f), Quaternion.identity, blockingLayer);

        if (hits.Length > 0)
        {
            PushableEntity box = hits[0].GetComponent<PushableEntity>();
            if (box != null)
            {
                PushChain(position + direction, direction);
                box.Move(direction);
            }
        }
    }

    private IEnumerator ExecuteMove(Vector3 target)
    {
        m_isMove = true;

        if (GameManager.Instance != null)//MARKER 每次移动都播放音效，放在这里可以保证无论是普通移动还是被推着移动都会有声音
        {
            GameManager.Instance.PlaySFX(GameSFX.Move);
        }

        float elapsed = 0f;
        while (elapsed < moveDuration)
        {
            float t = elapsed / moveDuration;

            transform.localPosition = Vector3.Lerp(transform.position, target, t);
            elapsed += Time.deltaTime;

            yield return null;
        }
        transform.localPosition = target;

        m_isMove = false;

        if (GameManager.Instance != null)//CORE 玩家每一次移动后都会检测机关有没有被压
        {
            GameManager.Instance.CheckAllPlates();
        }
    }
}