using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class AnimatedMaterial : MonoBehaviour
{
    // �������Ĺ�ʽ������ framerate �ķ�ĸ������ materials.Length ���ܵõ����� 1S ��ѭ���Ĵ���������֡��Խ�࣬������Խ��
    public Material[] materials;
    public float frameInterval = 1f / 6f;//MARKER 帧间隔

    private MeshRenderer meshRenderer;
    private int frame;

    private void Awake()
    {
        meshRenderer = GetComponent<MeshRenderer>();
    }

    private void OnEnable()
    {
        InvokeRepeating(nameof(Animate), frameInterval, frameInterval);
    }

    private void OnDisable()
    {
        CancelInvoke();
        meshRenderer.material = materials[0];
        frame = 0;
    }

    private void Animate()
    {
        frame++;

        if (frame >= materials.Length)
        {
            frame = 0;
        }

        if (frame >= 0 && frame < materials.Length)
        {
            meshRenderer.material = materials[frame];
        }
    }
}
