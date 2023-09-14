using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.XR.ARFoundation;
using UnityEngine.XR.ARSubsystems;
using UnityEngine.EventSystems;
using System.Collections.Generic;
using System.Linq;
//脚本负责管理映射关系，实现映射动作
//新映射模式的优势：只需3自由度信息就可以推断6自由度运动，可以超出动捕的范围，以非运动结构的方式追踪（研究现实关节运动结构正是为了能够设置现在的mapping方式）

public class Tracking : MonoBehaviour
{

  public HandSkeletonVisualize hSVScript;

  //从上一场景中接收索引相关：
  ChoosingSave _instance;
  public GameObject[] models = new GameObject[4];
  [HideInInspector] public GameObject spawnedObject;
  //射线检测和平面检测、物体放置
  private Camera mainCamera;
  static List<ARRaycastHit> Hits;
  private ARRaycastManager mRaycastManager;
  private Vector2 touchStartPos;
  private const float clickThreshold = 50f;
  private float lastTouchDistance;


  public float rotationSpeed = 0.5f;
  public float slideSpeed = 1f;
  public float scaleSpeed = 0.01f;

  //需要一个一次性变量指示是否是初次放置
  [HideInInspector] public bool isPlacing = true;

  //调用次数计数器，每调用一次，计数+1，用来指示当前应当被读取的字典
  private int _mapIndex = 0;//追踪
  //存储映射，替换
  public static Dictionary<GameObject, Dictionary<int, int>> TempCurrentMapping = new Dictionary<GameObject, Dictionary<int, int>>();

  //需要
  public static Dictionary<GameObject, bool> ObjStatus = new Dictionary<GameObject, bool>();
  public static Dictionary<GameObject, Quaternion> InitQ = new Dictionary<GameObject, Quaternion>();
  public static Dictionary<GameObject, Vector3> InitV = new Dictionary<GameObject, Vector3>();
  //需要
  private Dictionary<GameObject, int> CurrentMap = new Dictionary<GameObject, int>();
  private Dictionary<GameObject,GameObject> ParentToChild = new Dictionary<GameObject,GameObject>();
  
  private Vector3 InitCompare;
  private Vector3 CurrCompare;
  private bool pause = false;
  //调试
  public Text text;
  public Text staticText;
  public bool positionTest = true;
  public GameObject empty;




  private void Start()
  {
    //相机获得
    mainCamera = Camera.main;
    //索引号取得，模型激活改为选择生成模型
    _instance = ChoosingSave.GetInstance();
    //模型放置
    Hits = new List<ARRaycastHit>();
    mRaycastManager = GetComponent<ARRaycastManager>();

    isPlacing = true;
    _mapIndex = 0;
  }
  /// <summary>
  /// 1.放置模型 2.处理跟踪
  /// </summary>
  private void Update()
  {
    //if (positionTest && !isPlacing)
    //{
    //  Position_Test();
    //  return;
    //}
    if (isPlacing)
    {

      TapManage();
    }
    else
    {

      OnMoving(spawnedObject);
    }

  }
  /// <summary>
  /// 在放置阶段，管理输入
  /// </summary>
  private void TapManage()
  {
    if (Input.touchCount == 1)
    {
      Touch touch = Input.GetTouch(0);

      switch (touch.phase)
      {
        case TouchPhase.Began:
          touchStartPos = touch.position;
          break;
        case TouchPhase.Ended:
          if (Vector2.Distance(touch.position, touchStartPos) < clickThreshold)
          {
            SingleTap(touch.position);
            //isPlacing = false;
          }
          break;
      }
    }
    // 双指触摸
    if (Input.touchCount == 2)
    {
      Touch touch1 = Input.GetTouch(0);
      Touch touch2 = Input.GetTouch(1);

      if (touch1.phase == TouchPhase.Moved || touch2.phase == TouchPhase.Moved)
      {
        float currentTouchDistance = Vector2.Distance(touch1.position, touch2.position);
        bool shouldZoom = currentTouchDistance != lastTouchDistance;
        bool shouldSlide = currentTouchDistance < clickThreshold && Vector2.Angle(touch1.deltaPosition, touch2.deltaPosition) < 30f;

        if (shouldZoom && !shouldSlide)
        {
          // 执行缩放操作
          float distanceDelta = currentTouchDistance - lastTouchDistance;
          DoubleZoom(distanceDelta);
          lastTouchDistance = currentTouchDistance;
        }

      }

    }
  }
  /// <summary>
  /// 单指触摸处理
  /// </summary>
  /// <param name="position"></param>
  //todo：模型初始化处理
  private void SingleTap(Vector2 position)
  {
    //var touch = Input.GetTouch(0);

    if (EventSystem.current.IsPointerOverGameObject())
    {
      // 点击发生在UI元素上，不进行射线检测
      return;
    }


    if (mRaycastManager.Raycast(position, Hits, TrackableType.PlaneWithinPolygon | TrackableType.PlaneWithinBounds))
    {
      var hitPose = Hits[0].pose;
      if (spawnedObject == null)
      {
        spawnedObject = Instantiate(models[_instance._modelIndex], hitPose.position, hitPose.rotation);
        //spawnedObject.transform.SetParent(empty.transform,false);
        Set_Quaternion(spawnedObject);
        Refresh_Map();
        Set_Parent_To_Child(spawnedObject);
        Set_ObjStatus(spawnedObject);
        text.text = $"\ninitq:{InitQ.Count}";
        text.text += $"\np2c:{ParentToChild.Count}";
        text.text += $"\nobjstatus:{ObjStatus.Count}";
      }
      else
      {
        spawnedObject.transform.position = hitPose.position;
      }
    }
  }
  /// <summary>
  /// 双指处理
  /// </summary>
  /// <param name="distanceDelta"></param>
  private void DoubleZoom(float distanceDelta)
  {
    // 计算新的缩放因子
    float scaleFactor = 1 + (distanceDelta * scaleSpeed);

    // 更新物体的大小
    spawnedObject.transform.localScale = spawnedObject.transform.localScale * scaleFactor;
  }
  /// <summary>
  /// 设置初始旋转
  /// </summary>
  /// <param name="obj"></param>
  private void Set_Quaternion(GameObject obj)
  {
    if (!obj.CompareTag("NotRig"))
    {
      InitQ[obj] = obj.transform./*local*/rotation;
    }
    foreach (Transform child in obj.transform)
    {
      Set_Quaternion(child.gameObject);
    }
  }
  /// <summary>
  /// 填入currentMap，清空字典和指针自增都在外面
  /// </summary>
  private void Refresh_Map()
  {
    foreach (var pair in TapManager.mappingList[_mapIndex])
    {
      var obj = String_Obj_Match(pair.Key, spawnedObject);
      if (obj != null)
      {
        CurrentMap.Add(obj, (int)pair.Value);
        continue;
      }
    }
  }
  /// <summary>
  /// 搜索名称匹配字符串的物体
  /// </summary>
  /// <param name="objName"></param>
  /// <returns></returns>
  private GameObject String_Obj_Match(string objName, GameObject model)
  {

    if (model.name == objName)
    {
      return model;
    }
    foreach (Transform child in model.transform)
    {
      var result = String_Obj_Match(objName, child.gameObject);
      if (result != null)
      {
        return result;
      }
    }
    return null;
  }
  /// <summary>
  /// 父子关系字典
  /// </summary>
  /// <param name="obj"></param>
  //todo:需要随字典更新
  private void Set_Parent_To_Child(GameObject obj)
  {

    if (!obj.CompareTag("NotRig"))
    {
      GameObject validChild = null;
      foreach (Transform child in obj.transform)
      {
        if (!child.gameObject.CompareTag("NotRig") && CurrentMap.ContainsKey(child.gameObject))
        {
          validChild = child.gameObject;
          break;
        }
      }

      // 只有当找到了符合条件的子物体时，才将这一对添加到字典中
      if (validChild != null)
      {
        ParentToChild[obj] = validChild;
      }
    }
    foreach (Transform child in obj.transform)
    {
      Set_Parent_To_Child(child.gameObject);
    }
  }
  /// <summary>
  /// 初始化使用情况表
  /// </summary>
  /// <param name="obj"></param>
  private void Set_ObjStatus(GameObject obj)
  {
    // 如果物体的tag不是"NotRig"，则将它添加到字典中
    if (!obj.CompareTag("NotRig"))
    {
      ObjStatus[obj] = false;
    }

    // 递归地处理所有子物体
    foreach (Transform child in obj.transform)
    {
      Set_ObjStatus(child.gameObject);
    }
  }
  /// <summary>
  /// 切换map时更新使用状况
  /// </summary>
  private void Refresh_ObjStatus()
  {
    foreach (var pair in CurrentMap)
    {
      ObjStatus[pair.Key] = true;
    }
  }



  /// <summary>
  /// done
  /// </summary>
  public void Button_Done()
  {
    isPlacing = false;
    pause = false;
    Set_InitVector();
    Set_InitCompare();

  }
  /// <summary>
  /// stop
  /// </summary>
  public void Button_Stop()
  {
    pause = true;
  }
  /// <summary>
  /// next
  /// </summary>
  //todo:计算新的vector，重置模型位置
  public void Button_Next()
  {

    CurrentMap.Clear();
    _mapIndex++;
    Refresh_Map();
    Refresh_ObjStatus();
    Set_Parent_To_Child(spawnedObject);
    //todo:计算新的vector，重置模型位置
    pause = false;
  }
  /// <summary>
  /// init Vectors
  /// </summary>
  private void Set_InitVector()
  {
    foreach (var pair in CurrentMap)
    {
      var selfIndex = CurrentMap[pair.Key];
      var childIndex = CurrentMap[ParentToChild[pair.Key]];
      var landmarks = hSVScript.m_currList[0].Landmark;
      var childPosition = new Vector3(landmarks[childIndex].X, landmarks[childIndex].Y, landmarks[childIndex].Z);
      var selfPosition = new Vector3(landmarks[selfIndex].X, landmarks[selfIndex].Y, landmarks[selfIndex].Z);
      //var childPosition = hSVScript.m_boneObjList[childIndex].transform.position;
      //var selfPosition = hSVScript.m_boneObjList[selfIndex].transform.position;
      var initVector = childPosition - selfPosition;
      InitV.Add(pair.Key, initVector);
    }
  }
  /// <summary>
  /// initial vector5-17
  /// </summary>
  private void Set_InitCompare()
  {
    var landmarks = hSVScript.m_currList[0].Landmark;
    var childPosition = new Vector3(landmarks[17].X, landmarks[17].Y, landmarks[17].Z);
    var selfPosition = new Vector3(landmarks[5].X, landmarks[5].Y, landmarks[5].Z);
    //var childPosition = hSVScript.m_boneObjList[17].transform.position;
    //var selfPosition = hSVScript.m_boneObjList[5].transform.position;
    InitCompare = childPosition - selfPosition;

  }
  /// <summary>
  /// decide what to do while moving
  /// </summary>
  /// <param name="model"></param>
  private void OnMoving(GameObject model)
  {
    text.text = $"cam:{mainCamera.transform.rotation.eulerAngles}\nposition:{mainCamera.transform.position}";
    if (pause)
    {
      return;
    }
    var root = FindFirstNonTagged(spawnedObject);
    foreach (var pair in CurrentMap)
    {
      if(pair.Key == mainCamera)
      {
        //text.text = "Camera!!!!!!1";
        return;
      }
      if (pair.Key == root && ObjStatus[pair.Key] == false)
      {

        Root_First_Move(pair.Key);
        //text.text += $"root:{pair.Key.transform.position}";
        continue;
      }
      if (pair.Key == root && ObjStatus[pair.Key])
      {
        Root_Again_Move(pair.Key);
        //text.text += $"root2:{pair.Key.transform.rotation.eulerAngles}";
        continue;
      }
      if (ParentToChild.ContainsKey(pair.Key) && ObjStatus[pair.Key] == false)
      {
        Parent_First_Move(pair.Key);
        //text.text += $"parent:{pair.Key.transform.rotation.eulerAngles}";
        continue;
      }
      if (ParentToChild.ContainsKey(pair.Key) && ObjStatus[pair.Key])
      {
        Parent_Again_Move(pair.Key);
        //text.text += $"parent2:{pair.Key.transform.rotation.eulerAngles}";
        continue;
      }
      if (!ParentToChild.ContainsKey(pair.Key))
      {
        //text.text += $"child:{pair.Key.transform.rotation.eulerAngles}";
        continue;
      }
    }
  }
  private Vector3 Get_Runtime_Position(GameObject obj)
  {
    var selfIndex = CurrentMap[obj];
    var landmarks = hSVScript.m_currList[0].Landmark;
    return new Vector3(landmarks[selfIndex].X, landmarks[selfIndex].Y, landmarks[selfIndex].Z);
    //return hSVScript.m_boneObjList[selfIndex].transform.position;
  }
  private Vector3 Get_Runtime_Vector(GameObject obj)
  {
    var landmarks = hSVScript.m_currList[0].Landmark;
    var selfIndex = CurrentMap[obj];
    var childIndex = CurrentMap[ParentToChild[obj]];
    var childPosition = new Vector3(landmarks[childIndex].X, landmarks[childIndex].Y, landmarks[childIndex].Z);
    var selfPosition = new Vector3(landmarks[selfIndex].X, landmarks[selfIndex].Y, landmarks[selfIndex].Z);
    //var childPosition = hSVScript.m_boneObjList[childIndex].transform.position;
    //var selfPosition = hSVScript.m_boneObjList[selfIndex].transform.position;
    return childPosition - selfPosition;
  }
  private Vector3 Get_Runtime_Compare()
  {
    var landmarks = hSVScript.m_currList[0].Landmark;
    var childPosition = new Vector3(landmarks[17].X, landmarks[17].Y, landmarks[17].Z);
    var selfPosition = new Vector3(landmarks[5].X, landmarks[5].Y, landmarks[5].Z);
    //var childPosition = hSVScript.m_boneObjList[17].transform.position;
    //var selfPosition = hSVScript.m_boneObjList[5].transform.position;
    return childPosition - selfPosition;
  }
  /// <summary>
  /// 找模型的根
  /// </summary>
  /// <param name="parent"></param>
  /// <returns></returns>
  private GameObject FindFirstNonTagged(GameObject parent)
  {
    if (!parent.CompareTag("NotRig"))
    {

      return parent;
    }

    foreach (Transform child in parent.transform)
    {
      var result = FindFirstNonTagged(child.gameObject);
      if (result != null)
      {

        return result;
      }
    }

    return null;
  }

  private void Root_First_Move(GameObject obj)
  {
    var quaternionTransform = Calculate_Runtime_Rotation(obj);
    var selfIndex = CurrentMap[obj];
    obj.transform.position = hSVScript.m_boneObjList[selfIndex].transform.position;
    obj.transform.rotation = quaternionTransform * InitQ[obj];
  }
  private void Root_Again_Move(GameObject obj)
  {
    Child_Reaction(obj);
  }
  private void Parent_First_Move(GameObject obj)
  {
    var quaternionTransform = Calculate_Runtime_Rotation(obj);
    obj.transform.rotation = quaternionTransform * InitQ[obj];
  }
  private void Parent_Again_Move(GameObject obj)
  {
    Child_Reaction(obj);
  }
  public Quaternion Calculate_Runtime_Rotation(GameObject obj)
  {
    var CurrV = Get_Runtime_Vector(obj);
    var CurrCompare = Get_Runtime_Compare();

    var CurrSystemQ = Quaternion.LookRotation(CurrV, CurrCompare);
    var InitSystemQ = Quaternion.LookRotation(InitV[obj], InitCompare);
    return CurrSystemQ * Quaternion.Inverse(InitSystemQ);

  }
  public void Child_Reaction(GameObject obj)
  {
    var child = ParentToChild[obj];
    var selfPosition = Get_Runtime_Position(obj);
    var childPosition = Get_Runtime_Position(child);
    var distance = Vector3.Distance(childPosition, selfPosition);

    var direction = Get_Runtime_Vector(obj);
    direction.Normalize();

    child.transform.position = gameObject.transform.position + (direction * distance);
  }






  private void Position_Test()
  {
    staticText.text = $"InitQ\n";
    foreach (var pairq in InitQ)
    {
      staticText.text += $"{pairq.Value.eulerAngles}\n";
    }

    foreach (var pair in CurrentMap)
    {
      pair.Key.transform.position = hSVScript.m_boneObjList[pair.Value].transform.position;
      //Decide_Rotation(pair.Key);
      //var landmarks = hSVScript.m_currList[0].Landmark;
      //pair.Key.transform.position = new Vector3(landmarks[pair.Value].X, landmarks[pair.Value].Y, landmarks[pair.Value].Z);

    }    
  }
}


















////获得需要的初始向量
//private Vector3 GetInitVector(GameObject obj)
//{
//  staticText.text = $"GIV entered";
//  var result = HSVScript.m_boneObjList[TempCurrentMapping[obj].First().Value].transform.position
//    - HSVScript.m_boneObjList[TempCurrentMapping[obj].First().Key].transform.position;
//  staticText.text += $"{result}";
//  return result;
//}
////建立控制关系
//public void ChangeMapping()
//{ //没有模型不能调用
//  if (spawnedObject == null)
//  {
//    //text.text = "no object found!";
//    return;
//  }

//  var tempDict = MappingStore.MappingListStore[CurrentIndex];


//  //text.text += $"count:{MappingStore.MappingListStore[CurrentIndex].Count}";
//  //text.text += $"temp:{tempDict.First().Key},{tempDict.First().Value}";
//  //todo:对于一个元素的情况无法正确处理

//  foreach (var pair_parent in tempDict)
//  {
//    var parent_obj = SearchGameObject(spawnedObject.transform, pair_parent.Key);
//    //对于parent没有子节点的情况
//    if (parent_obj.transform.childCount == 0)
//    {
//      var matching = new Dictionary<int, int>();
//      matching.Add((int)pair_parent.Value, -1);
//      TempCurrentMapping.Add(parent_obj, matching);
//      continue;
//    }
//    foreach (var pair_child in tempDict)
//    {
//      if(pair_child.Key == pair_parent.Key)
//      {
//        continue;
//      }
//      var child_obj = SearchGameObject(spawnedObject.transform, pair_child.Key);
//      if (child_obj.transform.parent == parent_obj.transform)
//      {
//        var matching = new Dictionary<int, int>();
//        matching.Add((int)pair_parent.Value, (int)pair_child.Value);
//        TempCurrentMapping.Add(parent_obj, matching);
//      }

//    }
//  }

//}
////遍历查找符合字符串的物体/子物体
//private GameObject SearchGameObject(Transform transform, string objName)
//{
//  //text.text = $"objname:{objName}";
//  //text.text = $"\ntransform name:{transform.name} ";
//  if (transform.name.Contains(objName))
//  {
//    return transform.gameObject;
//  }
//  foreach(Transform child in transform)
//  {
//    GameObject found = SearchGameObject(child, objName);
//    if (found != null)
//    {
//      return found;
//    }
//  }
//  //text.text = "no matching obj";
//  return null;
//}
//public void BeforeMoving()
//{
//  //todo:参数不对，应该是手部节点不是物体节点
//  foreach(var pair in TempCurrentMapping)
//  {
//    InitV.Add(pair.Key,GetInitVector(pair.Key));
//  }
//}
////当被调用时，控制当前组的运动
//public void OnMoving()
//{
//  //text.text = "onMoving";
//  foreach (var pair in TempCurrentMapping)
//  {
//    //text.text = "loopEntered";
//    //根节点
//    if (pair.Key == FindFirstNonTagged(spawnedObject))
//    {
//      text.text = "moving0";
//      pair.Key.transform.position = HSVScript.m_boneObjList[pair.Value.First().Key].transform.position;
//      //获取当前向量和对应的初始向量，获得旋转，并和初旋转相乘
//      //if(InitV == null)
//      //{
//      //  text.text = "InitV not found";
//      //}
//      text.text = $"{InitV.Count}";
//      var initVec = InitV[pair.Key];
//      text.text += $"initVec:{initVec}";
//      var currentVec = HSVScript.m_boneObjList[pair.Value.First().Value].transform.position
//        - HSVScript.m_boneObjList[pair.Value.First().Key].transform.position;
//      text.text += $"\nto:{HSVScript.m_boneObjList[pair.Value.First().Value].transform.position}" +
//        $"\nfrom:{HSVScript.m_boneObjList[pair.Value.First().Key].transform.position}";
//      var currentRotation = Quaternion.FromToRotation(initVec, currentVec);
//      //var rootRotation = HSVScript.m_boneObjList[pair.Value.First().Value].transform.position
//       // - HSVScript.m_boneObjList[pair.Value.First().Key].transform.position;
//      pair.Key.transform.rotation = InitQ[pair.Key] * currentRotation;//rootRotation;
//      text.text += $"rotation:{currentRotation}";
//      //todo:这个变换不应该出现在这，应该出现在change里

//      text.text += $"initVec:{initVec}\ncurrentVec:{currentVec}\nRotation:{currentRotation}";
//      continue;
//    }
//    //一般父节点
//    if (pair.Key.transform.childCount != 0 && !ObjStatus[pair.Key])
//    {
//      text.text = "moving1";
//      var initVec = InitV[pair.Key];
//      var currentVec = HSVScript.m_boneObjList[pair.Value.First().Value].transform.position
//        - HSVScript.m_boneObjList[pair.Value.First().Key].transform.position;
//      var currentRotation = Quaternion.FromToRotation(initVec, currentVec);
//      pair.Key.transform.rotation = InitQ[pair.Key] * currentRotation;

//      continue;
//    }
//    //多子父节点，找到本次控制中的子节点，给它赋予位置
//    if (pair.Key.transform.childCount != 0 && ObjStatus[pair.Key])
//    {
//      text.text = "moving2";
//      foreach(var pair_child in TempCurrentMapping)
//      {
//        if(pair_child.Key.transform.parent == pair.Key)//正常来讲应该只有一个符合的子节点，找到了就退出
//        {
//          text.text = "moving3";
//          var distance = Vector3.Distance(pair.Key.transform.position, pair_child.Key.transform.position);
//          var direction = HSVScript.m_boneObjList[pair.Value.First().Value].transform.position
//           - HSVScript.m_boneObjList[pair.Value.First().Key].transform.position;
//          direction.Normalize();
//          pair_child.Key.transform.position = pair.Key.transform.position + direction * distance;
//          //子节点的旋转不要管，也不要将状态设置到true，遍历到它的时候才会处理
//        }
//      }
//      continue;
//    }
//    //叶子结点
//    if (pair.Key.transform.childCount == 0)
//    {
//      text.text = "moving4";
//      //不做任何控制，跟着父节点转就行
//      continue;
//    }
//    //text.text += "case not found";
//  }
//  //text.text = "onMovingDone";


//}
////寻找根节点
//private GameObject FindFirstNonTagged(GameObject parent)
//{
//  if (parent.tag != "NotRig")
//  {

//    return parent;
//  }

//  foreach (Transform child in parent.transform)
//  {
//    GameObject result = FindFirstNonTagged(child.gameObject);
//    if (result != null)
//    {

//      return result;
//    }
//  }

//  return null;
//}
////是否是首次选中
//private Dictionary<GameObject, bool> GetInitStatus(GameObject obj)
//{
//  var result = new Dictionary<GameObject, bool>();
//  result.Add(obj, false);
//  foreach(Transform child in obj.transform)
//  {
//    var childResult = GetInitStatus(child.gameObject);
//    foreach(var pair in childResult)
//    {
//      result.Add(pair.Key, pair.Value);
//    }
//  }
//  return result;

//}
////遍历物体获得初始旋转
//private Dictionary<GameObject, Quaternion> GetInitQuaternion(GameObject parent)
//{
//  var result = new Dictionary<GameObject, Quaternion>();
//  result.Add(parent,parent.transform.rotation);  // 添加自身到列表
//  foreach (Transform child in parent.transform)
//  {
//    var childResult = GetInitQuaternion(child.gameObject);
//    foreach (var pair in childResult)
//    {
//      result.Add(pair.Key, pair.Value);
//    }
//  }
//  return result;
//}
////当录制模块新增track，调用下面的方法
//public void onTrackChanged()
//{
//  foreach(var pair in TempCurrentMapping)
//  {
//    ObjStatus[pair.Key] = true;
//  }
//  TempCurrentMapping.Clear();
//  CurrentIndex++;
//  ChangeMapping();
//}
///// <summary>
///// done按钮响应
///// </summary>
//public void PlacingDone()
//{
//  isPlacing = false;

//}
////找儿子
//GameObject FindChildObject(GameObject parent)
//{
//  foreach (var key in TempCurrentMapping.Keys)
//  {
//    if (key.transform.parent == parent.transform)
//    {
//      return key;
//    }
//  }
//  return null;
//}
////临时调试用，打印mappingliststore字典列表
//public void PrintStoredList()
//{
//  text.text = "print entered";
//  int dictIndex = 1;
//  string content = "";
//  foreach (var dict in MappingStore.MappingListStore)
//  {
//    content += $"字典{dictIndex}\n";
//    foreach (var pair in dict)
//    {
//      content += $"键值对：({pair.Key}, {pair.Value})\n";
//    }
//    dictIndex++;
//  }

//  text.text = content;  // 更新Text组件
//}

