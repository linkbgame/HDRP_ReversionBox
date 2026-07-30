using System;
using System.Collections.Generic;
using UnityEngine;

[DisallowMultipleComponent]
public class CameraOcclusionDither : MonoBehaviour
{
    private const int MaxHits = 64;

    [Header("References")]
    [SerializeField] private Camera targetCamera;
    [SerializeField] private Transform target;
    [SerializeField] private Vector3 targetOffset = new Vector3(0f, 0.8f, 0f);

    [Header("Occlusion")]
    [SerializeField] private LayerMask occluderMask;
    [SerializeField, Min(0.01f)] private float checkInterval = 0.05f;
    [SerializeField] private bool includeChildrenRenderers = true;

    [Header("Dither")]
    [SerializeField] private Material ditherMat;
    [SerializeField, Range(0f, 1f)] private float ditherAmount = 0.55f;
    [SerializeField] private string ditherAmountProperty = "_DitherAmount";

    private readonly RaycastHit[] m_hits = new RaycastHit[MaxHits];
    private readonly HashSet<Renderer> m_detectedRenderers = new HashSet<Renderer>();
    private readonly HashSet<Renderer> m_activeRenderers = new HashSet<Renderer>();
    private readonly List<Renderer> m_restoreBuffer = new List<Renderer>(32);
    private readonly HashSet<Collider> m_targetColliders = new HashSet<Collider>();
    private readonly Dictionary<Collider, Renderer[]> m_colliderRendererCache = new Dictionary<Collider, Renderer[]>();
    private readonly Dictionary<Renderer, Material[]> m_originalMaterials = new Dictionary<Renderer, Material[]>();

    private float m_nextCheckTime;
    private int m_ditherAmountId;
    private Transform m_cachedTarget;

    private void Reset()
    {
        target = transform;
        targetCamera = Camera.main;
        occluderMask = LayerMask.GetMask("Wall");
    }

    private void Awake()
    {
        if (target == null)
        {
            target = transform;
        }

        if (targetCamera == null)
        {
            targetCamera = Camera.main;
        }

        m_ditherAmountId = Shader.PropertyToID(ditherAmountProperty);
        m_cachedTarget = target;
        CacheTargetColliders();
        ApplyDitherAmountToMaterial();
    }

    private void OnValidate()
    {
        checkInterval = Mathf.Max(0.01f, checkInterval);
        ditherAmount = Mathf.Clamp01(ditherAmount);

        if (Application.isPlaying)
        {
            m_colliderRendererCache.Clear();
            CacheTargetColliders();
            ApplyDitherAmountToMaterial();
        }
    }

    private void OnDisable()
    {
        RestoreAllRenderers();
    }

    private void LateUpdate()
    {
        if (targetCamera == null || target == null || ditherMat == null)
        {
            RestoreAllRenderers();
            return;
        }

        if (m_cachedTarget != target)
        {
            RestoreAllRenderers();
            m_cachedTarget = target;
            m_colliderRendererCache.Clear();
            CacheTargetColliders();
        }

        if (Time.time < m_nextCheckTime)
        {
            return;
        }

        m_nextCheckTime = Time.time + checkInterval;
        ApplyDitherAmountToMaterial();
        RefreshOcclusion();
    }

    private void RefreshOcclusion()
    {
        m_detectedRenderers.Clear();

        Vector3 from = targetCamera.transform.position;
        Vector3 to = target.position + targetOffset;
        Vector3 dir = to - from;
        float distance = dir.magnitude;

        if (distance <= 0.001f)
        {
            UpdateAppliedRenderers();
            return;
        }

        int hitCount = Physics.RaycastNonAlloc(
            from,
            dir / distance,
            m_hits,
            distance,
            occluderMask,
            QueryTriggerInteraction.Ignore);

        for (int i = 0; i < hitCount; i++)
        {
            Collider hitCollider = m_hits[i].collider;
            if (hitCollider == null)
            {
                continue;
            }

            if (m_targetColliders.Contains(hitCollider))
            {
                continue;
            }

            CollectRenderers(hitCollider);
        }

        UpdateAppliedRenderers();
    }

    private void CollectRenderers(Collider hitCollider)
    {
        if (!m_colliderRendererCache.TryGetValue(hitCollider, out Renderer[] renderers) || renderers == null)
        {
            renderers = ResolveRenderers(hitCollider);
            m_colliderRendererCache[hitCollider] = renderers;
        }

        for (int i = 0; i < renderers.Length; i++)
        {
            Renderer renderer = renderers[i];
            if (renderer != null)
            {
                m_detectedRenderers.Add(renderer);
            }
        }
    }

    private Renderer[] ResolveRenderers(Collider hitCollider)
    {
        if (includeChildrenRenderers)
        {
            return hitCollider.GetComponentsInChildren<Renderer>(true);
        }

        Renderer selfRenderer = hitCollider.GetComponent<Renderer>();
        if (selfRenderer != null)
        {
            return new[] { selfRenderer };
        }

        Renderer parentRenderer = hitCollider.GetComponentInParent<Renderer>();
        if (parentRenderer != null)
        {
            return new[] { parentRenderer };
        }

        return Array.Empty<Renderer>();
    }

    private void UpdateAppliedRenderers()
    {
        foreach (Renderer renderer in m_detectedRenderers)
        {
            ApplyDither(renderer);
        }

        m_restoreBuffer.Clear();

        foreach (Renderer renderer in m_activeRenderers)
        {
            if (!m_detectedRenderers.Contains(renderer))
            {
                m_restoreBuffer.Add(renderer);
            }
        }

        for (int i = 0; i < m_restoreBuffer.Count; i++)
        {
            RestoreRenderer(m_restoreBuffer[i]);
        }
    }

    private void ApplyDither(Renderer renderer)
    {
        if (renderer == null || m_activeRenderers.Contains(renderer))
        {
            return;
        }

        Material[] sharedMats = renderer.sharedMaterials;
        if (sharedMats == null || sharedMats.Length == 0)
        {
            return;
        }

        Material[] cachedOriginal = new Material[sharedMats.Length];
        Array.Copy(sharedMats, cachedOriginal, sharedMats.Length);
        m_originalMaterials[renderer] = cachedOriginal;

        bool changed = false;
        for (int i = 0; i < sharedMats.Length; i++)
        {
            if (sharedMats[i] != ditherMat)
            {
                sharedMats[i] = ditherMat;
                changed = true;
            }
        }

        if (changed)
        {
            renderer.sharedMaterials = sharedMats;
        }

        m_activeRenderers.Add(renderer);
    }

    private void RestoreRenderer(Renderer renderer)
    {
        if (renderer != null && m_originalMaterials.TryGetValue(renderer, out Material[] original))
        {
            renderer.sharedMaterials = original;
        }

        m_activeRenderers.Remove(renderer);
        m_originalMaterials.Remove(renderer);
    }

    private void RestoreAllRenderers()
    {
        m_restoreBuffer.Clear();
        m_restoreBuffer.AddRange(m_activeRenderers);

        for (int i = 0; i < m_restoreBuffer.Count; i++)
        {
            RestoreRenderer(m_restoreBuffer[i]);
        }

        m_detectedRenderers.Clear();
    }

    private void CacheTargetColliders()
    {
        m_targetColliders.Clear();

        if (target == null)
        {
            return;
        }

        Collider[] colliders = target.GetComponentsInChildren<Collider>(true);
        for (int i = 0; i < colliders.Length; i++)
        {
            if (colliders[i] != null)
            {
                m_targetColliders.Add(colliders[i]);
            }
        }
    }

    private void ApplyDitherAmountToMaterial()
    {
        if (ditherMat == null)
        {
            return;
        }

        if (string.IsNullOrEmpty(ditherAmountProperty))
        {
            ditherAmountProperty = "_DitherAmount";
        }

        m_ditherAmountId = Shader.PropertyToID(ditherAmountProperty);
        if (ditherMat.HasFloat(m_ditherAmountId))
        {
            ditherMat.SetFloat(m_ditherAmountId, ditherAmount);
        }
    }
}
