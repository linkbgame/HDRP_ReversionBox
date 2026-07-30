using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using DG.Tweening;
using Cinemachine;

public interface IMergeController
{
    Transform transform { get; }
    bool TryRevertAt(Vector3 pos, Vector3 dir);
    void Transition(bool merge, Vector3 point, Vector3 normal);
}
public class WallMerge : MonoBehaviour, IMergeController
{
    private IPlayerAgent playerAgent;
    private AnimatedMaterial playerAnimation;
    private Renderer frameRenderer;
    private CinemachineImpulseSource impulseSource;
    private CinemachineBrain brain;
    private RaySearch search;
    private float playerZScale;
    private float activationTime;
    private int wallLayer;

    [Header("参数")]
    public float switchTo2DTime = 0.5f;
    public float revertTo3DTime = 0.3f;
    [SerializeField] private float cooldown = 1.5f;//MARKER 避免反复触发从3D切换2D
    [SerializeField] private float recoverInputLockDuration = 0.15f;
    [Space]
    [Header("投影器")]
    [SerializeField] private GameObject projectorObject;
    public ParticleSystem mergeParticle;
    public ParticleSystem exitParticle;
    private IProjectorAgent m_projectorAgent;
    [Space]
    [Header("画框设置")]
    //public Transform frameQuad;
    //public Color frameLitColor;
    [Space]
    [Header("相机")]
    public GameObject gameCam;
    public GameObject wallCam;
    [Space]
    [Header("后期处理")]
    public Volume dofVolume;
    public Volume zoomVolume;
    [Space]
    [Header("是否显示融合提示")]
    public GameObject mergeTip;
    public bool isShowMergeTip = false;

    //TODO 如果换成Awake会不会有报错
    private void Start()
    {
        playerAgent = GetComponent<IPlayerAgent>();
        m_projectorAgent = projectorObject.GetComponent<IProjectorAgent>();
        playerAnimation = GetComponent<AnimatedMaterial>();
        brain = Camera.main.GetComponent<CinemachineBrain>();
        playerZScale = transform.GetChild(0).localScale.z;
        //frameRenderer = frameQuad.GetComponent<Renderer>();
        impulseSource = Camera.main.GetComponent<CinemachineImpulseSource>();

        activationTime = Time.time;
        wallLayer = LayerMask.NameToLayer("Wall");
    }

    private void Update()
    {
        //MARKER 每帧根据射线与开关控制 mergeTip 显示
        RaycastHit tempHit;
        bool hitWall = Physics.Raycast(transform.position + (Vector3.up * .1f), transform.forward, out tempHit, 1)
                       && tempHit.collider.gameObject.layer == wallLayer;
        if (mergeTip != null)
        {
            mergeTip.SetActive(hitWall && isShowMergeTip);
        }   

        //CORE 融合工作流触发源头的cd
        if (Input.GetKeyDown(KeyCode.Space) && Time.time > activationTime + cooldown)
        {
            Debug.Log("触发WallMerge检测");
            activationTime = Time.time;//MARKER 第一次if判断后立刻开启cd
            HandleMergeInput();
        }
    }

    private void HandleMergeInput()
    {
        if (Physics.Raycast(transform.position + (Vector3.up * .1f), transform.forward, out RaycastHit hit, 1))
        {
            if (hit.collider.gameObject.layer == wallLayer)
            {
                ToRaySearch toSearch = hit.transform.GetComponentInParent<ToRaySearch>();
                if (toSearch == null)
                {
                    return;
                }
                this.search = toSearch.raySearch;

                int currentIndex = GetClosestFaceCenterIndex(search.faceCentersXZ, hit.point);
                Vector2 currentXZ = search.faceCentersXZ[currentIndex];
                float targetProjectionY = search.projectionY;
                Vector3 targetPos = new Vector3(currentXZ.x, targetProjectionY, currentXZ.y);//MARKER 默认三维坐标 y 为 1f

                // 计算方向索引流向
                int nextIndex = (currentIndex + 1) % search.faceCentersXZ.Count;
                Vector2 nextXZ = search.faceCentersXZ[nextIndex];
                Vector3 vecCurrent = new Vector3(currentXZ.x, 0, currentXZ.y);
                Vector3 vecNext = new Vector3(nextXZ.x, 0, nextXZ.y);
                Vector3 listDir = (vecNext - vecCurrent).normalized;// 列表中索引增加的方向 (在XZ平面上)，它代表了我们如果沿着索引增加的方向走，会朝向哪里
                bool isListOrderGoingRight = IsRightSide(-hit.normal, listDir, Vector3.up);

                PlayerSetNotActive();
                Transition(true, targetPos, hit.normal);
                m_projectorAgent.InitializeProjector(this, currentIndex, isListOrderGoingRight, search, hit.normal, targetPos);
            }
        }
    }

    public bool TryRevertAt(Vector3 pos, Vector3 dir)
    {
        if (playerAgent.CanRevertAt(pos, dir))
        {
            return true;
        }   
        return false;
    }

    public void Transition(bool merge, Vector3 point, Vector3 normal)
    {
        Vector3 finalNormal = merge ? -normal : normal;
        Vector3 finalPosition = merge ? point : new Vector3(point.x, 0, point.z);
        transform.forward = finalNormal;

        float scale = merge ? 0.01f : playerZScale;//融合则缩小至 0.01
        float finalTransition = merge ? switchTo2DTime : revertTo3DTime;
        float dofDelay = merge ? finalTransition + 0.3f : 0;//TEST 2
        float dofAmount = merge ? 1 : 0;//融合时权重为 1

        DOVirtual.Float(dofVolume.weight, dofAmount, finalTransition, DofPostVolume).SetDelay(dofDelay);//延迟 dofDelay 秒后，在 finalTransition 秒内，把 dofVolume.weight 平滑变到 dofAmount，并在过程中持续调用 DofPostVolume(float) 去应用这个值
        if (merge)
        {
            FrameMovement(normal, finalPosition, finalTransition);
            DOVirtual.Float(zoomVolume.weight, 1, 0.7f, ZoomVolume).OnComplete(() => DOVirtual.Float(zoomVolume.weight, 0, 0.3f, ZoomVolume));
        }
        MergeSequence(merge, finalPosition, finalNormal, scale, finalTransition);
    }

    private Sequence MergeSequence(bool merge, Vector3 finalPosition, Vector3 exitNormal, float scale, float finalTransition)
    {
        Sequence s = DOTween.Sequence();

        //STEP 1 前置准备 (Interval & Particles)
        if (merge)
        {
            s.AppendInterval(0.2f);// 停顿
        }
        else
        {
            s.AppendCallback(() => exitParticle.Play());// 特效
        }
        
        //STEP 2 环境切换 (Camera & Audio)
        s.AppendCallback(() => {
            gameCam.SetActive(!merge);
            wallCam.SetActive(merge);
            
            GameSFX sfx = merge ? GameSFX.Merge : GameSFX.Revert;
            GameManager.Instance.PlaySFX(sfx);
        });

        //STEP 3 核心位移和缩放运动 (DOMove DOScaleZ)
        s.Append(transform.DOMove(finalPosition, finalTransition).SetEase(Ease.InBack));
        s.Join(playerAgent.transform.GetChild(0).DOScaleZ(scale, finalTransition).SetEase(Ease.InSine));

        //STEP 4 Cube玩家实体激活关闭状态切换
        if (merge)
        {
            s.AppendCallback(() => {
                PlayerActivation(merge);
                playerAnimation.enabled = false;//关闭Material切换动画
                impulseSource.GenerateImpulse(); 
            });
        } 
        else
        {
            s.AppendCallback(() => {
                PlayerActivation(merge); 
                playerAgent.TryRevertMove(finalPosition, exitNormal);//CORE 复原推力
            });
        }

        //STEP 5 2D 投影系统同步 (Decal Sync)
        s.AppendCallback(() => {
            m_projectorAgent.transform.GetChild(0).gameObject.SetActive(merge);//第一个子对象是DecalParent
            m_projectorAgent.EnableInput(merge);
            mergeParticle.Play();
        });
        
        //STEP 6 画框视觉修饰
        if (frameRenderer != null)
        {
            s.Join(frameRenderer.material.DOColor(Color.clear, "_UnlitColor", 1f));
        }

        return s;
    }

#region 辅助方法

    //TODO PA && PSNA 不优雅，但解决问题
    private void PlayerActivation(bool merge)
    {
        if (merge == true)
        {      
            playerAgent.gameObject.SetActive(false);
            playerAgent.IsMoving = false;
        }
        else
        {
            playerAgent.IsActive = true;
            playerAgent.gameObject.SetActive(true);
            playerAgent.IsMoving = false;
            playerAgent.LockInput(recoverInputLockDuration);//MARKER 禁止复原瞬间FSM移动生效
        }
    }

    //融合时禁止playerAgent的输入，保证融合过程位置一致性，同时把移动标志处理干净
    private void PlayerSetNotActive()
    {
        playerAgent.IsActive = false;
    }

    private void FrameMovement(Vector3 normal, Vector3 finalPosition, float finalTransition)
    {
        /* frameQuad.position = transform.position + new Vector3(0, .85f, 0) - (transform.forward * .5f);
        frameQuad.forward = -normal;
        frameRenderer.material.SetColor("_UnlitColor", Color.clear);
        frameRenderer.material.DOColor(frameLitColor, "_UnlitColor", 1f).SetDelay(.3f);
        frameQuad.DOMove(finalPosition + new Vector3(0, .85f, 0) - (transform.forward * .05f), finalTransition).SetEase(Ease.InBack).SetDelay(.2f); */
    }

    private int GetClosestFaceCenterIndex(List<Vector2> centers, Vector3 hitPoint)
    {
        Vector2 flatHit = new Vector2(hitPoint.x, hitPoint.z);
        int bestIndex = -1;
        float closestDist = float.MaxValue;

        for (int i = 0; i < centers.Count; i++)
        {
            float d = Vector2.Distance(flatHit, centers[i]);
            if (d < closestDist)
            {
                closestDist = d;
                bestIndex = i;
            }
        }
        return bestIndex;
    }


    //https://forum.unity.com/threads/left-right-test-function.31420/
    //向量积（Cross Product）建立局部坐标系，并利用点积（Dot Product）
    //叉积得到的结果向量垂直于输入的两个向量
    //在 3D 空间中，如果你已知“上方”（Up）和“前方”（Forward），通过叉积就能计算出垂直于这两个方向的“右方”（Right）
    //右手定则
    //再来 点积 判断“右方”与目标方向的相似性
    private bool IsRightSide(Vector3 fwd, Vector3 targetDir, Vector3 up)
    {
        Vector3 right = Vector3.Cross(up.normalized, fwd.normalized);        // right vector
        float dir = Vector3.Dot(right, targetDir.normalized);
        return dir > 0f;
    }

    //MARKER 作为回调方法在DOVirtual.Float()被每帧调用
    private void DofPostVolume(float x)
    {
        dofVolume.weight = x;
    }

    private void ZoomVolume(float x)
    {
        zoomVolume.weight = x;
    } 
    #endregion
}
