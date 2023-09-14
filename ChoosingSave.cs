using System.Collections;
using System.Collections.Generic;
using UnityEngine;


//单例模式储存信息
public class ChoosingSave : MonoBehaviour
{
  private static ChoosingSave instance;
  public int _modelIndex { get; set; }

  private void Awake()
  {
    if (instance == null)
    {
      instance = this;
      DontDestroyOnLoad(this.gameObject);
    }
    else if (instance != this)
    {

      Destroy(this.gameObject);
    }
  }
  //获取
  public static ChoosingSave GetInstance()
  {
    return instance;
  }

}
