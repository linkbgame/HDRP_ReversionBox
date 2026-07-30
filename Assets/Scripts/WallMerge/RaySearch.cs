using System.Collections;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

//Code special thanks to mix&jam and Freya Holmér
//https://twitter.com/FreyaHolmer

/// <summary>
/// 创建切换为2D维度时移动的面心跑道，这里的面心只是xz坐标匹配，该脚本 y 坐标固定为 1f
/// </summary>
public class RaySearch : MonoBehaviour
{
    public float stepSize = 0.1f;
    public float offsetMargin = 0.01f;
    public int checkCountMax = 100;
    [Header("射线检测层级")]
    public LayerMask raycastLayers = Physics.DefaultRaycastLayers;
    private bool cornerCheck = false;
    private bool roundedCornerCheck = false;
    public List<MeshPoint> meshPoints = new List<MeshPoint>();
    public List<MeshPoint> cornerPoints;
    public List<MeshPoint> roundCornerPoints;
    // public List<Vector3> pathPoints = new List<Vector3>();

    [Header("面心数据")]
    public float projectionY;
    public List<Vector2> faceCentersXZ = new List<Vector2>();

    List<Vector3[]> debugTangentCheck = new List<Vector3[]>();
    List<Vector3[]> debugNegativeCheck = new List<Vector3[]>();
    List<Vector3[]> debugBehindCheck = new List<Vector3[]>();


    void OnDrawGizmos()
    {
#if UNITY_EDITOR
        Handles.color = Color.white;
        //Handles.DrawAAPolyLine(cornerPoints);
        DrawLinePairs(debugTangentCheck, Color.red);
        DrawLinePairs(debugNegativeCheck, Color.blue);
        DrawLinePairs(debugBehindCheck, Color.cyan);

        if (cornerPoints == null)
            return;

        foreach(MeshPoint p in cornerPoints)
        {
            //绘制Scene视图里的 转角点线框球
            Gizmos.DrawWireSphere(p.position, .15f);
        }

        foreach (Vector2 p in faceCentersXZ)
        {
            Vector3 pos = new Vector3(p.x, transform.position.y, p.y);//MARKER Gizmos显示为 自身y
            Gizmos.color = Color.yellow;
            Gizmos.DrawSphere(pos, .15f);//画实心黄色面心球
        }
#endif 
    }

    void DrawLinePairs(List<Vector3[]> list, Color color)
    {
#if UNITY_EDITOR
        Handles.color = color;
        foreach (Vector3[] pair in list)
        {
            Handles.DrawAAPolyLine(pair[0], pair[1]);
        }
#endif 
    }

    /// <summary>
    /// 1、除法（归一化到刻度）2、Round取整（寻找最近刻度）3、乘法（还原物理数值）
    /// 可以修改precision以实现任意精度的舍入
    /// </summary>
    static Vector3 RoundVector3(Vector3 vec, float precision = 0.1f)
    {
        return new Vector3(
            Mathf.Round(vec.x / precision) * precision,
            Mathf.Round(vec.y / precision) * precision,
            Mathf.Round(vec.z / precision) * precision
        );
    }

    void FindNext(Vector3 pt, Vector3 normal)
    {
        MeshPoint mp = new MeshPoint(); mp.position = pt; mp.normal = normal;

        // 转角点检测
        if (meshPoints.Count > 1)
        {

            MeshPoint mpnew = new MeshPoint(); mpnew.position = pt; mpnew.normal = normal;

            if (cornerPoints.Count > 0)
            {
                if (Vector3.Distance(cornerPoints[0].position, mpnew.position) < .3f && cornerPoints[0].normal == normal)
                    cornerCheck = true;
            }

            print(Vector3.Dot(meshPoints[meshPoints.Count - 1].normal, normal));

            // Dot点积小于0.98认为是转角点，点积为1则两个向量方向完全相同
            if (Vector3.Dot(meshPoints[meshPoints.Count - 1].normal, normal) < .98f && !cornerCheck)
                cornerPoints.Add(mpnew);// 转角点记录的数据是 mpnew
        }

        // 约分转角点检测
        if (meshPoints.Count > 1)
        {
            Vector3 roundedPt = RoundVector3(pt, 0.1f);
            MeshPoint mpround = new MeshPoint(); mpround.position = roundedPt; mpround.normal = normal;


            if (roundCornerPoints.Count > 0)
            {
                if (Vector3.Distance(roundCornerPoints[0].position, mpround.position) < .3f && roundCornerPoints[0].normal == normal)
                    roundedCornerCheck = true;
            }

            print(Vector3.Dot(meshPoints[meshPoints.Count - 1].normal, normal));

            // Dot点积小于0.98认为是转角点，点积为1则两个向量方向完全相同
            if (Vector3.Dot(meshPoints[meshPoints.Count - 1].normal, normal) < .98f && !roundedCornerCheck)
                roundCornerPoints.Add(mpround);// 约分转角点记录的数据是 mpround
        }

        meshPoints.Add(mp);// 普通点记录的数据是 mp

        Vector3 tangent = Vector3.Cross(normal, Vector3.up);
        Vector3 offsetPt = pt + normal * offsetMargin;
        Vector3 tangentCheckPoint = offsetPt + tangent * stepSize;
        Vector3 negativeCheckPoint = tangentCheckPoint - normal * (offsetMargin * 2);
        Vector3 behindCheckPoint = negativeCheckPoint - tangent * (stepSize * 0.75f);

        // find positive turn or flat surface
        bool foundThing = false;
        RaycastHit hit;

        // Check positive turn
        if (Physics.Raycast(offsetPt, tangent, out hit, stepSize, raycastLayers))
        {
            debugTangentCheck.Add(new[] { offsetPt, hit.point });
            foundThing = true;
        }
        else
        {
            debugTangentCheck.Add(new[] { offsetPt, tangentCheckPoint });
            // check flat or slight negative turn
            if (Physics.Raycast(tangentCheckPoint, -normal, out hit, offsetMargin * 2, raycastLayers))
            {
                debugNegativeCheck.Add(new[] { tangentCheckPoint, hit.point });
                foundThing = true;
            }
            else
            {
                debugNegativeCheck.Add(new[] { tangentCheckPoint, negativeCheckPoint });
                // check negative turn
                if (Physics.Raycast(negativeCheckPoint, -tangent, out hit, stepSize * 2, raycastLayers))
                {
                    foundThing = true;
                    debugBehindCheck.Add(new[] { negativeCheckPoint, hit.point });
                }
                else
                {
                    debugBehindCheck.Add(new[] { negativeCheckPoint, behindCheckPoint });
                }
            }
        }

        if (foundThing && meshPoints.Count < checkCountMax)
        {
            FindNext(hit.point, hit.normal);
        }

    }

    /// <summary>
    /// 获取面心数据
    /// </summary>
    public void GetFaceCenters()
    {
        faceCentersXZ.Clear();
        if (roundCornerPoints.Count < 2)
        {
             return;
        }

        //MARKER 根据是否闭合来决定循环次数
        int segmentCount = roundedCornerCheck ? roundCornerPoints.Count : roundCornerPoints.Count - 1;
        for (int i = 0; i < segmentCount; i++)
        {
            //STEP 1 获取一个转角点作为起点
            Vector3 pStart = roundCornerPoints[i].position;
            //STEP 2 获取下一个转角点作为终点
            Vector3 pEnd = roundCornerPoints[(i + 1) % roundCornerPoints.Count].position;//MARKER 当 i 是最后一个索引时，(i+1) % count 会自动回到 0
            //STEP 3 计算起点和终点之间的距离和方向
            float distance = Vector3.Distance(pStart, pEnd);
            Vector3 direction = (pEnd - pStart).normalized;
            //STEP 4 根据距离和方向计算需要生成多少个立方体（面心）
            int cubeCount = Mathf.RoundToInt(distance);
            //STEP 5 循环生成面心位置
            for (int k = 0; k < cubeCount; k++)
            {
                //公式：起点 + 方向 * (0.5 + 索引偏移)
                //k=0 时偏移 0.5, k=1 时偏移 1.5 ...
                Vector3 centerPos = pStart + direction * (0.5f + k);
                //提取XZ并修约精度
                Vector2 xzCenter = new Vector2(
                    Mathf.Round(centerPos.x * 10f) / 10f,
                    Mathf.Round(centerPos.z * 10f) / 10f
                );
                faceCentersXZ.Add(xzCenter);
            }
        }
    }

    [ContextMenu("生成点位")]
    void DoPoints()
    {
#if UNITY_EDITOR
        Undo.RecordObject(this, "Generate RaySearch Points");
#endif 
        cornerCheck = false;
        roundedCornerCheck = false;

        // 清除之前的数据，避免重复添加

        if (meshPoints.Count > 0)
        {
            projectionY = 0f;
            meshPoints.Clear();
            cornerPoints.Clear();
            roundCornerPoints.Clear();
            debugTangentCheck.Clear();
            debugNegativeCheck.Clear();
            debugBehindCheck.Clear();
        }

        if (Physics.Raycast(transform.position, Vector3.forward, out RaycastHit hit, Mathf.Infinity, raycastLayers))
            FindNext(hit.point, hit.normal);

        projectionY = this.transform.position.y;
        GetFaceCenters();

#if UNITY_EDITOR
        EditorUtility.SetDirty(this);

        UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(this.gameObject.scene);
        Debug.Log("数据已成功持久化到场景中！");
#endif
        //create array for white line
        //cornerPoints = new Vector3[meshPoints.Count];
        //for (int i = 0; i < meshPoints.Count; i++)
        //    cornerPoints[i] = meshPoints[i].position;
    }
}

[System.Serializable]
public struct MeshPoint
{
    public Vector3 position;
    public Vector3 normal;
}
