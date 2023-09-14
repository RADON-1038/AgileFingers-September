using System.Collections;
using System.Collections.Generic;
using UnityEngine;


//该脚本挂载在joint预制体上，用来向tapmanager发送自身射线检测响应
public class JointDetectable : MonoBehaviour
{
  private Renderer _renderer;
  private void Start()
  {
    _renderer = GetComponent<Renderer>();
  }
  public void OnRaycastHit()
  {
    _renderer.material.color = Color.red;
  }
  public void OnUndo()
  {
    _renderer.material.color = Color.white;
  }
}
//todo：考虑一下通过点击来取消映射的功能？可能从性能和表现上都好一些
