using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using DG.Tweening;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public enum GameSFX
{
    Move,
    Merge,
    Revert,
    Win,
    FailAciton,
    ProjectorMove
}

public class GameManager : MonoBehaviour
{
    public static GameManager Instance;

    [Header("Level Flow")]
    public GameObject menu;
    public List<TriggerPlate> allPlates;
    public ExitGate exitGate;
    public PlayerFSM player;
    public float delayBeforeLoadNextScene = 2f;
    public float delayVolumnDuration = 2f;
    public string nextSceneName = "Lv2";
    public bool isLevelCompleted = false;

    [Header("通关出口设置")]
    [SerializeField] private float winUnlockDelay = 1.2f;
    private float allPlatesActivatedTime = -1f;
    private bool allPlatesActiveNow = false;

    [Header("按钮设置")]
    [SerializeField] private string quitButtonName = "退出游戏按钮";
    [SerializeField] private string menuToggleName = "菜单按钮";
    [SerializeField] private float hoverScale = 1.15f;
    [SerializeField] private float tweenDuration = 0.15f;
    [SerializeField] private Ease hoverEase = Ease.OutBack;
    [SerializeField] private Ease normalEase = Ease.OutQuad;
    private readonly Dictionary<Button, Vector3> m_originScale = new();

    [Space]
    [Header("后期效果")]
    public Volume dofVolume;
    public Volume zoomVolume;

    [Space]
    [Header("音效设置")]
    public AudioSource audioSource;
    public AudioClip moveClip;
    public AudioClip mergeClip;
    public AudioClip revertClip;
    public AudioClip winClip;
    public AudioClip failActionClip;
    public AudioClip projectorMoveClip;

    private void Awake()
    {
        Instance = this;
        allPlates = FindObjectsOfType<TriggerPlate>().ToList();
        if (dofVolume != null)
        {
            dofVolume.weight = 1f;
        }
    }

    private void Start()
    {
        UIInitialization();
        EnterLevelDisablePlayer();
    }
    //TODO 之后可以学习新输入系统，有映射等等，统一管理
    private void Update()
    {
        if (Input.GetKeyDown(KeyCode.N))
        {
            int currentIndex = SceneManager.GetActiveScene().buildIndex;
            int nextIndex = currentIndex + 1;
            if (nextIndex >= SceneManager.sceneCountInBuildSettings)
            {
                nextIndex = 0;
            }

            SceneManager.LoadScene(nextIndex);
            return;
        }

        if (Input.GetKeyDown(KeyCode.R))
        {
            SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
        }
    }

    #region 关卡进程
    public void CheckAllPlates()
    {
        if (allPlates.Count == 0)
        {
            return;
        }

        bool allActivated = allPlates.All(plate => plate.GetIsPressed());
        if (allActivated && !allPlatesActiveNow)
        {
            allPlatesActivatedTime = Time.time;
        }

        allPlatesActiveNow = allActivated;
        if (allActivated)
        {
            exitGate.OpenGate();
        }
        else
        {
            exitGate.CloseGate();
        }
    }

    public bool CanWinNow()
    {
        if (!allPlatesActiveNow) return false;
        if (allPlatesActivatedTime < 0f) return false;
        return Time.time - allPlatesActivatedTime >= winUnlockDelay;
    }

    public void Win()
    {
        if (isLevelCompleted)
        {
            return;
        }

        isLevelCompleted = true;
        if (player != null)
        {
            player.enabled = false;
        }

        StartCoroutine(DelayLoadScene(nextSceneName, delayBeforeLoadNextScene));
    }  

    private IEnumerator DelayLoadScene(string loadSceneName, float delay)
    {
        if (audioSource != null)
        {
            audioSource.Stop();
        }

        yield return new WaitForSeconds(0.5f);

        PlaySFX(GameSFX.Win);
        Tween exitTween = ExitLevelVolumn();

        float winClipDuration = winClip != null ? winClip.length : 0f;
        float avDuration = Mathf.Max(winClipDuration, exitTween.Duration());
        float totalWait = Mathf.Max(delay, avDuration);

        yield return new WaitForSeconds(totalWait);
        SceneManager.LoadScene(loadSceneName);
    }

    public void IsPlayerOnPlate(bool state)
    {
        foreach (TriggerPlate plate in allPlates)
        {
            plate.ActivateShine(state);
        }
    }

    public void LoadLevelByIndex(int buildIndex)
    {
        if (buildIndex < 0 || buildIndex >= SceneManager.sceneCountInBuildSettings)
        {
            Debug.LogWarning($"Invalid level index: {buildIndex}");
            return;
        }

        SceneManager.LoadScene(buildIndex);
    }
    #endregion

    #region 音效和Volumn
    public void PlaySFX(GameSFX sfx)
    {
        if (audioSource == null)
        {
            return;
        }

        switch (sfx)
        {
            case GameSFX.Move:
                audioSource.PlayOneShot(moveClip);
                break;
            case GameSFX.Merge:
                audioSource.PlayOneShot(mergeClip);
                break;
            case GameSFX.Revert:
                audioSource.PlayOneShot(revertClip);
                break;
            case GameSFX.Win:
                audioSource.PlayOneShot(winClip);
                break;
            case GameSFX.FailAciton:
                audioSource.PlayOneShot(failActionClip);
                break;
            case GameSFX.ProjectorMove:
                audioSource.PlayOneShot(projectorMoveClip);
                break;
        }
    } 

    private void EnterLevelDisablePlayer()
    {
        if (player != null)
        {
            player.enabled = false;
        }

        EnterLevelVolumn().OnComplete(() =>
        {
            if (player != null)
            {
                player.enabled = true;
            }
        });
    }

    private Tween EnterLevelVolumn()
    {
        return DOVirtual.Float(dofVolume.weight, 0f, 1f, DofPostVolume);
    }

    private Tween ExitLevelVolumn()
    {
        Sequence s = DOTween.Sequence();
        s.Join(DOVirtual.Float(dofVolume.weight, 1f, delayVolumnDuration, DofPostVolume));
        s.Join(DOVirtual.Float(zoomVolume.weight, 1f, delayVolumnDuration, ZoomVolume));
        return s;
    }

    private void DofPostVolume(float x)
    {
        if (dofVolume != null)
        {
            dofVolume.weight = x;
        }
    }

    private void ZoomVolume(float x)
    {
        if (zoomVolume != null)
        {
            zoomVolume.weight = x;
        }
    }
    #endregion

    #region  UI
    private void UIInitialization()
    {
        BindButtonHoverTween();
        AutoSetNextSceneName();
    }

    private void BindButtonHoverTween()
    {
        Button[] buttons = FindObjectsByType<Button>(FindObjectsInactive.Include, FindObjectsSortMode.None);

        foreach (var btn in buttons)
        {
            m_originScale[btn] = btn.transform.localScale;

            // 确保有 EventTrigger
            var trigger = btn.GetComponent<EventTrigger>();
            if (trigger == null) trigger = btn.gameObject.AddComponent<EventTrigger>();
            if (trigger.triggers == null) trigger.triggers = new List<EventTrigger.Entry>();

            AddTrigger(trigger, EventTriggerType.PointerEnter, () =>
            {
                btn.transform.DOKill();
                btn.transform.DOScale(m_originScale[btn] * hoverScale, tweenDuration).SetEase(hoverEase);
            });

            AddTrigger(trigger, EventTriggerType.PointerExit, () =>
            {
                btn.transform.DOKill();
                btn.transform.DOScale(m_originScale[btn], tweenDuration).SetEase(normalEase);
            });
        }
    }

    private void AddTrigger(EventTrigger trigger, EventTriggerType type, UnityEngine.Events.UnityAction action)
    {
        var entry = new EventTrigger.Entry { eventID = type };
        entry.callback.AddListener(_ => action.Invoke());
        trigger.triggers.Add(entry);
    }

    private void AutoSetNextSceneName()
    {
        string currentName = SceneManager.GetActiveScene().name;
        Match match = Regex.Match(currentName, @"^Lv\s*(\d+)$", RegexOptions.IgnoreCase);
        if (!match.Success)
        {
            return;
        }

        int currentIndex = int.Parse(match.Groups[1].Value);
        int nextIndex = currentIndex + 1;
        nextSceneName = $"Lv{nextIndex}";
    }

    public void ToggleMenu()
    {
        if (menu == null) return;
        menu.SetActive(!menu.activeSelf);
    }

    public void QuitGame()
    {
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif
    }
    #endregion
}
