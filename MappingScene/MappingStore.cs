using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class MappingStore : MonoBehaviour
{

  //list显示文本
  public Text listContent;
  //接收list
  public static List<Dictionary<string, int?>> MappingListStore = new List<Dictionary<string, int?>>();

  public void PrintStoredList()
  {
    int dictIndex = 1;
    string content = "";
    foreach (var dict in MappingListStore)
    {
      content += $"字典{dictIndex}\n";
      foreach (var pair in dict)
      {
        content += $"键值对：({pair.Key}, {pair.Value})\n";
      }
      dictIndex++;
    }

    listContent.text = content;  // 更新Text组件
  }

}
