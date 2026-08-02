using System.Collections;
using UnityEngine;


/// <summary>
/// ��ɫ����BlueBox-Layer����Wall/YellowBox/BlueBox
/// ===========
/// ��ɫ����YellowBox-Layer����Wall/YellowBox/BlueBox
/// </summary>
public class PushableEntity : MonoBehaviour
{
    [SerializeField] private float moveDuration = 0.3f;
    public LayerMask blockingLayer;
    private bool isMoving = false;

    public bool IsMoving => isMoving;

    /// <summary>
    /// ��ͨ���������ӵĵݹ��������
    /// </summary>
    public bool CanPush(Vector3 direction)
    {
        if (isMoving)
        {
            return false;
        }

        Vector3 targetPos = transform.position + direction;
        Collider[] hits = Physics.OverlapBox(targetPos, new Vector3(0.4f, 0.4f, 0.4f), Quaternion.identity, blockingLayer);

        if (hits.Length == 0)
        {
            return true;
        } 

        PushableEntity nextBox = hits[0].GetComponent<PushableEntity>();

        if (nextBox != null && nextBox.gameObject.layer == LayerMask.NameToLayer("YellowBox"))
        {
            return nextBox.CanPush(direction);
        }

        return false;
    }

    /// <summary>
    /// ��ԭ���������ӵĵݹ��������
    /// </summary>
    public bool RecoverCanPush(Vector3 direction)
    {
        if (isMoving)
        {
            return false;
        }

        Vector3 targetPos = transform.position + direction;
        Collider[] hits = Physics.OverlapBox(targetPos, new Vector3(0.4f, 0.4f, 0.4f), Quaternion.identity, blockingLayer);

        if (hits.Length == 0)
        {
            return true;
        }

        PushableEntity nextBox = hits[0].GetComponent<PushableEntity>();
        int mask = (1 << LayerMask.NameToLayer("YellowBox") | (1 << LayerMask.NameToLayer("BlueBox")));//MARKER Ӳ����Ϊ��ɫ����ɫ���ӵĲ㼶
        if (nextBox != null && (mask & (1 << nextBox.gameObject.layer)) != 0)
        {
            return nextBox.RecoverCanPush(direction);
        }

        return false;
    } 

    public void Move(Vector3 direction)
    {
        StartCoroutine(ExecuteMove(transform.position + direction));
    }

    public void MoveToWorld(Vector3 target)
    {
        StartCoroutine(ExecuteMove(target));
    }

    private IEnumerator ExecuteMove(Vector3 target)
    {
        isMoving = true;

        float elapsed = 0f;
        while (elapsed < moveDuration)//MARKER �ƶ�Ч���ȿ����
        {
            float t = elapsed / moveDuration;

            transform.localPosition = Vector3.Lerp(transform.position, target, t);
            elapsed += Time.deltaTime;

            yield return null;
        }
        transform.localPosition = target;

        isMoving = false;

        if (GameManager.Instance != null)//CORE ����ÿһ���ƶ��󶼻��������û�б�ѹ
        {
            GameManager.Instance.CheckAllPlates();
        }
    }
}