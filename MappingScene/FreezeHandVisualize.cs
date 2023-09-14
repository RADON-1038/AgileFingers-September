using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class FreezeHandVisualize : MonoBehaviour
{
  //
  public HandSkeletonVisualize HSVScript;

  public TapManager TMScript;

  [HideInInspector]public List<GameObject> freezeLandmarks;
  public GameObject landmarkPrefab;
  public Text text;

  //画线
  public LineRenderer[] lines;
  private readonly int[][] m_connections =
    {
    new []{0, 1, 2, 3, 4},
    new []{0, 5, 6, 7, 8},
    new []{9, 10, 11, 12},
    new []{13, 14, 15, 16},
    new []{0, 17, 18, 19, 20},
    };

  private void Start()
  {
    freezeLandmarks = new List<GameObject>();
  }

  public void startMapping()
  {

    //text.text = "started\n";
    for (var i = 0; i < 21; i++)
    {
      freezeLandmarks.Add(Instantiate(landmarkPrefab, HSVScript.m_boneObjList[i].transform.position,HSVScript.m_boneObjList[i].transform.rotation));
      //text.text += $"{i}:{freezeLandmarks[i].transform.position.x}";
    }

    for (int i = 0; i < m_connections.Length; i++)
    {
      var connections = m_connections[i];
      var pos = new Vector3[connections.Length];
      for (int j = 0; j < connections.Length; j++)
      {
        pos[j] = freezeLandmarks[connections[j]].transform.position;
      }

      lines[i].positionCount = pos.Length;
      lines[i].SetPositions(pos);
    }
  }

  public void MappingDone()
  {
    foreach(var obj in freezeLandmarks)
    {
      Destroy(obj);
    }
    freezeLandmarks.Clear();
  }
}
