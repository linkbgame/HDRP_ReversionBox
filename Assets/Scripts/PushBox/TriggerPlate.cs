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
        GridBoard board = GridBoard.Instance;
        if (board == null)
        {
            return false;
        }

        GridCoord coord = board.WorldToGrid(transform.position);
        return board.IsAnyOccupantAt(
            coord,
            GridOccupantType.Player,
            GridOccupantType.YellowBox,
            GridOccupantType.BlueBox
        );
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

