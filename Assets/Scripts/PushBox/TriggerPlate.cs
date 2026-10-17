using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// detectionLayer--Player/YellowBox/BlueBox
/// </summary>
public class TriggerPlate : MonoBehaviour
{
    public bool isShining;
    public LayerMask detectionLayer;
    private AnimatedMaterial m_anim;

    private void Awake()
    {
        m_anim = GetComponent<AnimatedMaterial>();
        Reset();
    }

    private void Reset()
    {
        isShining = false;
        m_anim.enabled = false;
    }

    public void ActivateShine(bool state)
    {
        if (state)
        {
            isShining = true;
            m_anim.enabled = true;
        }
        else
        {
            isShining = false;
            m_anim.enabled = false;
        }
    }

    public bool GetIsPressed()
    {
        Collider[] hits = Physics.OverlapBox(transform.position + Vector3.up * 0.5f,
                                            new Vector3(0.4f, 0.1f, 0.4f),
                                            Quaternion.identity,
                                            detectionLayer);
        return hits.Length > 0;
    }

    private void OnTriggerEnter(Collider other)
    {
        AnimatedMaterial animatedMaterial = other.gameObject.GetComponent<AnimatedMaterial>();
        if (animatedMaterial != null)
        {
            animatedMaterial.enabled = true;
        }
            
    }

    private void OnTriggerExit(Collider other)
    {
        AnimatedMaterial animatedMaterial = other.gameObject.GetComponent<AnimatedMaterial>();
        if (animatedMaterial != null)
        {
            animatedMaterial.enabled = false;
        }     
    }
}

