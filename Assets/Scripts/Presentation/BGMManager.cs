using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

[Serializable]
public struct SceneBgmRule
{
    public int sceneBuildIndex;
    public int bgmIndex;
}

/// <summary>
/// Persistent BGM manager with scene-threshold-based track switching and crossfade.
/// </summary>
public class BGMManager : MonoBehaviour
{
    private static BGMManager instance;

    [Header("BGM Clips")]
    [SerializeField] private AudioClip[] bgmArray;

    [Header("Scene Rules")]
    [Tooltip("Use the rule with the highest sceneBuildIndex where scene >= sceneBuildIndex.")]
    [SerializeField] private List<SceneBgmRule> sceneBgmRules = new List<SceneBgmRule>();

    [Header("Transition")]
    [Min(0f)]
    [SerializeField] private float crossFadeDuration = 1.5f;

    [Header("Volume")]
    [Range(0f, 1f)]
    [SerializeField] private float masterVolume = 1f;

    [Header("Debug")]
    [SerializeField] private bool verboseLog;

    private AudioSource sourceA;
    private AudioSource sourceB;
    private AudioSource activeSource;
    private Coroutine crossfadeCoroutine;
    private int currentBgmIndex = -1;

    private void Awake()
    {
        if (instance != null && instance != this)
        {
            Destroy(gameObject);
            return;
        }

        instance = this;
        DontDestroyOnLoad(gameObject);

        InitializeAudioSources();
    }

    private void OnEnable()
    {
        SceneManager.sceneLoaded += OnSceneLoaded;
    }

    private void OnDisable()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
    }

    private void Start()
    {
        RefreshBgmForScene(SceneManager.GetActiveScene(), true);
    }

    private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        RefreshBgmForScene(scene, false);
    }

    private void InitializeAudioSources()
    {
        sourceA = GetComponent<AudioSource>();
        if (sourceA == null)
        {
            sourceA = gameObject.AddComponent<AudioSource>();
        }

        sourceA.playOnAwake = false;
        sourceA.loop = true;
        sourceA.volume = GetMasterVolume();

        sourceB = gameObject.AddComponent<AudioSource>();
        sourceB.playOnAwake = false;
        sourceB.priority = 64;
        sourceB.loop = true;
        sourceB.volume = 0f;

        activeSource = sourceA;
    }

    private void RefreshBgmForScene(Scene scene, bool immediate)
    {
        int resolvedIndex = ResolveBgmIndexForScene(scene.buildIndex);
        if (resolvedIndex < 0)
        {
            return;
        }

        if (resolvedIndex == currentBgmIndex)
        {
            if (verboseLog)
            {
                Debug.Log($"[BGMManager] Keep current track index={currentBgmIndex} for scene {scene.name}({scene.buildIndex}).");
            }

            return;
        }

        AudioClip nextClip = bgmArray[resolvedIndex];
        if (nextClip == null)
        {
            Debug.LogWarning($"[BGMManager] bgmArray[{resolvedIndex}] is null. Skip switching.");
            return;
        }

        if (verboseLog)
        {
            Debug.Log($"[BGMManager] Switch track {currentBgmIndex} -> {resolvedIndex} at scene {scene.name}({scene.buildIndex}).");
        }

        currentBgmIndex = resolvedIndex;
        PlayClip(nextClip, immediate);
    }

    private int ResolveBgmIndexForScene(int sceneBuildIndex)
    {
        if (bgmArray == null || bgmArray.Length == 0)
        {
            Debug.LogWarning("[BGMManager] bgmArray is empty. Cannot play BGM.");
            return -1;
        }

        int targetIndex = 0;
        int bestThreshold = int.MinValue;

        for (int i = 0; i < sceneBgmRules.Count; i++)
        {
            SceneBgmRule rule = sceneBgmRules[i];
            if (sceneBuildIndex >= rule.sceneBuildIndex && rule.sceneBuildIndex >= bestThreshold)
            {
                bestThreshold = rule.sceneBuildIndex;
                targetIndex = rule.bgmIndex;
            }
        }

        if (targetIndex < 0)
        {
            Debug.LogWarning($"[BGMManager] Negative bgmIndex({targetIndex}) resolved for scene {sceneBuildIndex}. Fallback to 0.");
            targetIndex = 0;
        }

        if (targetIndex >= bgmArray.Length)
        {
            int clamped = bgmArray.Length - 1;
            Debug.LogWarning($"[BGMManager] Resolved bgmIndex({targetIndex}) exceeds bgmArray length({bgmArray.Length}). Clamped to {clamped}.");
            targetIndex = clamped;
        }

        return targetIndex;
    }

    private void PlayClip(AudioClip clip, bool immediate)
    {
        if (immediate || crossFadeDuration <= 0f || activeSource.clip == null || !activeSource.isPlaying)
        {
            if (crossfadeCoroutine != null)
            {
                StopCoroutine(crossfadeCoroutine);
                crossfadeCoroutine = null;
            }

            sourceA.Stop();
            sourceB.Stop();

            activeSource = sourceA;
            activeSource.clip = clip;
            activeSource.volume = GetMasterVolume();
            activeSource.loop = true;
            activeSource.Play();

            return;
        }

        if (crossfadeCoroutine != null)
        {
            StopCoroutine(crossfadeCoroutine);
        }

        AudioSource from = activeSource;
        AudioSource to = activeSource == sourceA ? sourceB : sourceA;

        to.clip = clip;
        to.loop = true;
        to.volume = 0f;
        to.Play();

        crossfadeCoroutine = StartCoroutine(CrossfadeCoroutine(from, to));
    }

    private IEnumerator CrossfadeCoroutine(AudioSource from, AudioSource to)
    {
        float elapsed = 0f;

        while (elapsed < crossFadeDuration)
        {
            elapsed += Time.unscaledDeltaTime;
            float t = Mathf.Clamp01(elapsed / crossFadeDuration);
            float volume = GetMasterVolume();

            from.volume = volume * (1f - t);
            to.volume = volume * t;

            yield return null;
        }

        from.volume = 0f;
        from.Stop();

        to.volume = GetMasterVolume();
        activeSource = to;
        crossfadeCoroutine = null;
    }

    private float GetMasterVolume()
    {
        return Mathf.Clamp01(masterVolume);
    }
}
