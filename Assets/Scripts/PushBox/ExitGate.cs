using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class ExitGate : MonoBehaviour
{
    public bool isOpen;
    private AnimatedMaterial m_anim;

    private void Awake()
    {
        m_anim = GetComponent<AnimatedMaterial>();
        Reset(); 
    }

    private void Reset()
    {
        isOpen = false;
        m_anim.enabled = false;
    }

    public void OpenGate()
    {
        if (isOpen)
        {
            return;
        }
        isOpen = true;
        m_anim.enabled = true;
    }

    public void CloseGate()
    {
        if (!isOpen)
        {
            return;
        }
        isOpen = false;
        m_anim.enabled = false;
    }
    //MARKER Stay 处理胜利，其他处理玩家和引导视效
    private void OnTriggerStay(Collider other)
    {
        if (isOpen && other.gameObject.layer == LayerMask.NameToLayer("Player"))
        {
            if (!GameManager.Instance.CanWinNow())
            {
                return;
            }
            GameManager.Instance.Win();
        }
    }

    private void OnTriggerEnter(Collider other)
    {
        if (other.gameObject.layer == LayerMask.NameToLayer("Player"))
        {
            AnimatedMaterial animatedMaterial = other.gameObject.GetComponent<AnimatedMaterial>();
            animatedMaterial.enabled = true;
            GameManager.Instance.IsPlayerOnPlate(true);
        }
    }

    private void OnTriggerExit(Collider other)
    {
        if (other.gameObject.layer == LayerMask.NameToLayer("Player"))
        {
            AnimatedMaterial animatedMaterial = other.gameObject.GetComponent<AnimatedMaterial>();
            animatedMaterial.enabled = false;
            GameManager.Instance.IsPlayerOnPlate(false);
        }
    }
}
