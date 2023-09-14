using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

using Mediapipe;
using Mediapipe.Unity;
using Mediapipe.Unity.CoordinateSystem;

using UnityEngine.XR.ARFoundation;
using UnityEngine.XR.ARSubsystems;

public class HandSkeletonVisualize : MonoBehaviour
{
  [SerializeField] private GameObject _char;
  [SerializeField] private ARCameraManager _arCamera;

  public Transform objRoot; // 存放小球的父节点
  public GameObject boneObj;
  public List<GameObject> m_boneObjList;
  
  //!!!!!!!!!!!!!!!!!!!!!!!!!!new in 8.19-1
  public Text coorText;
  public Coordinate coor;
  //new in 8.19-2
  //public bool mediaPipeController = true;
  private readonly int[][] m_connections =
    {
    new []{0, 1, 2, 3, 4},
    new []{0, 5, 6, 7, 8},
    new []{9, 10, 11, 12},
    new []{13, 14, 15, 16},
    new []{0, 17, 18, 19, 20},
    };

  

  public LineRenderer[] lines;
  //9.7给list添加了public
  //9.12改为了静态
  public List<NormalizedLandmarkList> m_currList;
  bool m_newLandMark = false;

  private void Start()
  {



    m_boneObjList = new List<GameObject>();
    while (m_boneObjList.Count < 21)
    {
      m_boneObjList.Add(Instantiate(boneObj, objRoot));

    }
  }

  public void DrawLater(List<NormalizedLandmarkList> list)
  {
    m_currList = list;
    m_newLandMark = true;
  }

  public void DrawNow(List<NormalizedLandmarkList> list)
  {



    if (list.Count == 0)
    {
      return;
    }

    var landmarks = list[0].Landmark; 
    if (landmarks.Count <= 0)
    {
      return;
    }

    for (var i = 0; i < landmarks.Count; i++)
    {
      var mark = landmarks[i];



      var pos = coor.vectorCalculate(mark.X, mark.Y);


      if (i == 0)
      {
        coor.infoPrint();
      }

      m_boneObjList[i].transform.position = objRoot.transform.position + pos;

    }

    coorText.text = "X coordinate: " + landmarks[0].X
    + "Y coordinate: " + landmarks[0].Y;

    //8.23
    for (int i = 0; i < m_connections.Length; i++)
    {
      var connections = m_connections[i];
      var pos = new Vector3[connections.Length];
      for (int j = 0; j < connections.Length; j++)
      {
        pos[j] = m_boneObjList[connections[j]].transform.position;
      }

      lines[i].positionCount = pos.Length;
      lines[i].SetPositions(pos);
    }

  }

  private void LateUpdate()
  {
    if (m_newLandMark)
    {
      UpdateDraw();
    }
  }

  private void UpdateDraw()
  {
    m_newLandMark = false;
    DrawNow(m_currList);
  }

  //todo:对于freeze按钮的响应，当接到按钮指令时，切换使用的预制体为完全透明
  public void onMapping()
  {
    foreach(var joint in m_boneObjList)
    {
      MeshRenderer meshrenderer = joint.GetComponent<MeshRenderer>();
      UnityEngine.Color color = meshrenderer.material.color;
      color.a = 0f;
      meshrenderer.material.color = color;
    }
    foreach(var line in lines)
    {
      line.startColor =new UnityEngine.Color(1, 0, 0, 0);
      line.endColor = new UnityEngine.Color(1, 0, 0, 0);
    }
  }
  //结束mapping模式
  public void onMappingDone()
  {
    foreach (var joint in m_boneObjList)
    {
      MeshRenderer meshrenderer = joint.GetComponent<MeshRenderer>();
      UnityEngine.Color color = meshrenderer.material.color;
      color.a = 1f;  // 将Alpha值设置为1，即不透明
      meshrenderer.material.color = color;
    }

    // 恢复lines中所有线段的不透明状态
    foreach (var line in lines)
    {
      line.startColor = new UnityEngine.Color(1, 0, 0, 1);  // 设置起始颜色为红色，并且Alpha值为1

      line.endColor = new UnityEngine.Color(1, 1, 1, 1);  // 设置结束颜色为白色，并且Alpha值为1
    }
  }

}












