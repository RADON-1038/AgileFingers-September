using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.XR.ARFoundation;
using UnityEngine.XR.ARSubsystems;
using UnityEngine.EventSystems;
//一进入场景二即进入摆放模式，该模式下可以放置和旋转物品，并显示手部实时情况。
//点击freeze按钮后，进入mapping模式，手部模型被定格，真实手部情况转为完全透明显示。此时仍然保留模型的旋转
//重要！！mapping模式中禁用原来的射线检测，启动【新的射线检测】，判定模型碰撞情况
//并开始维护字典列表。
//todo:撤销还没做好


//该脚本用来处理手指点击事件
//双指缩放，单指触摸
//脚本需要挂载在session origin下

//所有模型都有一个共同的父物体，该脚本控制父物体的移动、旋转，从而影响子物体的旋转和移动。
public class TapManager : MonoBehaviour
{
  //从上一场景中接收索引相关：
  ChoosingSave _instance;
  //rig模型
  public GameObject[] models = new GameObject[4];
  

  public ModelRig modelRigScript;
  //所有模型的共同父物体挂载位置
  //public GameObject parentOfModels;

  //运动参数
  private Vector2 touchStartPos;
  private const float clickThreshold = 50f;
  public float scaleSpeed = 0.01f; // 缩放速度
  private float lastTouchDistance;
  public float rotationSpeed = 0.5f;
  public float slideSpeed = 1f;
  //射线检测和平面检测
  private Camera mainCamera;
  static List<ARRaycastHit> Hits;
  private ARRaycastManager mRaycastManager;
  //实例化后的对象
  [HideInInspector]public GameObject spawnedObject;

  //按钮信号接收器
  //public string activeButton;

  //骨骼绘制部分///////////////////////////////////////////////////
  //rig和连线line模型的存储空间
  private List<GameObject> rigs;
  private Dictionary<Transform, LineRenderer> lines;
  //渲染骨骼所需预制体
  public GameObject rigPrefab;
  public GameObject lineRendererPrefab;
  //维护mapping字典
  private static Dictionary<string, int?> tempDict = new Dictionary<string, int?>();//先rig，后landmark
  public static List<Dictionary<string, int?>> mappingList = new List<Dictionary<string, int?>>();

  private static Stack<GameObject> tempKey;
  private static Stack<GameObject> oldKey;
  private GameObject tempParent;
  public FreezeHandVisualize FHVScript;
  //private GameObject tempRig;
  //模式控制器
  private bool isMapping = false;
  //切换至动画模式要控制的组件
  //public GameObject MappingButtons;
  //public GameObject MappingInfo;
  //public GameObject Freeze;
  //public GameObject Recorder;
  //public GameObject RecordButtons;
  //public GameObject RecordText;


  public Text text;
  public Text mappingStatus;
  public Text listContent;
  private void Start()
  {
    //相机获得
    mainCamera = Camera.main;
    //索引号取得，模型激活改为选择生成模型
    _instance = ChoosingSave.GetInstance();
    //models[_instance._modelIndex].SetActive(true);

    //模型放置
    Hits = new List<ARRaycastHit>();
    mRaycastManager = GetComponent<ARRaycastManager>();



    //初始化绘制容器
    rigs = new List<GameObject>();
    lines = new Dictionary<Transform, LineRenderer>();

    //初始化字典和列表
    //tempDict = new Dictionary<string, int?>();
    //mappingList = new List<Dictionary<string, int?>>();
    //初始化堆栈
    tempKey = new Stack<GameObject>();
    oldKey = new Stack<GameObject>();
  }

  private void Update()
  {


    // 单指触摸，内部完成
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
            if (isMapping != true)//freeze之后进入mapping模式，不能再移动模型
            {

              singleTap(touch.position);
            }
            else
            {
              mappingDetect(touch.position);
            }
          }
          break;
        case TouchPhase.Moved:
          float horizontalRotation = touch.deltaPosition.x * rotationSpeed;
          float verticalRotation = touch.deltaPosition.y * rotationSpeed;
          singleSlide(horizontalRotation, verticalRotation);

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
          doubleZoom(distanceDelta);
          lastTouchDistance = currentTouchDistance;
        }

      }

    }
    //绘制骨骼
    if(spawnedObject != null)
    {
      DrawRigConnections();
    }
    mappingStatus.text = $"joints:{tempDict.Count}\n";
    foreach(var pair in tempDict)
    {
      mappingStatus.text += $"{pair.Key}\n";
    }
  }
  //单指触摸
  private void singleTap(Vector2 position)
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
      if(spawnedObject == null)
      {
        spawnedObject = Instantiate(models[_instance._modelIndex], hitPose.position, hitPose.rotation);
        rigsInitiate();
      }
      else
      {
        spawnedObject.transform.position = hitPose.position;
      }
    }
  }


  //单指滑动
  private void singleSlide(float horizontalRotation, float verticalRotation)
  {
    if (spawnedObject != null)
    {
      var cameraUp = mainCamera.transform.up;
      var cameraRight = mainCamera.transform.right;

      // 在物体自身的位置上应用旋转
      spawnedObject.transform.Rotate(cameraUp, -horizontalRotation * rotationSpeed, Space.World); // 水平旋转
      spawnedObject.transform.Rotate(cameraRight, verticalRotation * rotationSpeed, Space.World);  // 垂直旋转
    }
    // 获取相机的局部轴
    else return;
  }


  //双指缩放
  public void doubleZoom(float distanceDelta)
  {
    // 计算新的缩放因子
    float scaleFactor = 1 + (distanceDelta * scaleSpeed);

    // 更新物体的大小
    spawnedObject.transform.localScale = spawnedObject.transform.localScale * scaleFactor;
  }





  //内部骨骼绘制部分//////////////////////////////////////////////
  //绘制调用
  private void rigsInitiate()
  {
    //text.text = "RI entered!";
    FillRigList(spawnedObject);

    CreateRigs();
  }

  //递归获取模型的rig，填入rig的list
  void FillRigList(GameObject parent)
  {
    //text.text += "FRL entered";
    Transform parentTransform = parent.transform;

    if (parent.tag != "NotRig")//不需要的rig不要加入list
    {
      if (!rigs.Contains(parent))  // 避免重复
      {
        rigs.Add(parent);
      }
    }



    // 遍历所有子对象
    for (int i = 0; i < parentTransform.childCount; i++)
    {
      var childTransform = parentTransform.GetChild(i);
      var childGameObject = childTransform.gameObject;

      if (childGameObject.tag != "NotRig")
      {
        if (!rigs.Contains(childGameObject))
        {
          rigs.Add(childGameObject);
        }
      }


      // 递归
      FillRigList(childGameObject);
    }
  }

  //rig预制体生成
  void CreateRigs()
  {
    foreach (var rig in rigs)
    {
      GameObject newRig = Instantiate(rigPrefab, rig.transform.position, rig.transform.rotation);
      newRig.transform.SetParent(rig.transform, true);  // 将新生成的预制体设置为子对象
    }
  }

  //rig之间画线
  void DrawRigConnections()
  {
    foreach (var rig in rigs)
    {
      GameObject go = rig;
      Transform parentTransform = go.transform.parent;

      // 检查该GameObject是否有父对象且该父对象也在字典中
      if (parentTransform != null && rigs.Contains(parentTransform.gameObject))
      {
        // 确保该连接有一个LineRenderer
        if (!lines.ContainsKey(go.transform))
        {
          GameObject newLineObject = Instantiate(lineRendererPrefab, Vector3.zero, Quaternion.identity);
          LineRenderer newLine = newLineObject.GetComponent<LineRenderer>();
          lines.Add(go.transform, newLine);
        }

        // 设置LineRenderer的位置
        LineRenderer line = lines[go.transform];
        line.SetPosition(0, go.transform.position);
        line.SetPosition(1, parentTransform.position);
      }
    }
  }














  //切换至mapping状态//////////////////////////////////////////////////////
  public void startMapping()
  {
    isMapping = true;
  }
  //字典列表维护//////////////////////////////////////////////////////
  //射线检测找点，包含了将检测点加入字典和通知节点变色的功能
  //private void mappingDetect(Vector2 touchPosition)
  //{
  //  Ray ray = mainCamera.ScreenPointToRay(touchPosition);
  //  RaycastHit hit;
  //  if (Physics.Raycast(ray, out hit, 10f))
  //  {
  //    // 获取被命中物体的 GameObject 和标签
  //    GameObject hitObject = hit.collider.gameObject;
  //    string hitTag = hitObject.tag;

  //    if(hitTag == "RigPrefabTag")
  //    {
  //      JointInput(hitObject);
  //      return;
  //    }
  //    if(hitTag == "StaticLandmark")
  //    {
  //      LandmarkInput(hitObject);
  //      return;
  //    }
  //    else
  //    {
  //      text.text = "Not joint or landmark";
  //    }
  //  }
  //}







  //private void JointInput(GameObject hitObject)
  //{
  //  tempDict.Add(hitObject.transform.parent.name, null);
  //  var script = hitObject.GetComponent<JointDetectable>();
  //  script.OnRaycastHit();
  //  return;
  //}

  //private void LandmarkInput(GameObject hitObject)
  //{
  //  foreach(var pair in tempDict)
  //  {
  //    if(pair.Value == null)
  //    {
  //      int index = FHVScript.freezeLandmarks.IndexOf(hitObject);
  //      pair.Value = 
  //    }
  //  }
  //}

  //private void JointValidTest()
  //{

  //}

  //private void LandmarkValidTest()
  //{

  //}
  //todo:设置一个字典，显示哪些可选，哪些不可选
  private void mappingDetect(Vector2 touchPosition)
  {
    if (EventSystem.current.IsPointerOverGameObject())
    {
      // 点击发生在UI元素上，不进行射线检测
      return;
    }
    Ray ray = mainCamera.ScreenPointToRay(touchPosition);
    RaycastHit hit;

    if (Physics.Raycast(ray, out hit, 10f))
    {
      // 获取被命中物体的 GameObject 和标签
      GameObject hitObject = hit.collider.gameObject;
      string hitTag = hitObject.tag;

      //joint的情况
      if(hitTag == "RigPrefabTag")
      {
        //已经存在的键不能再次加入
        if (tempDict.ContainsKey(hitObject.transform.parent.name))
        {
          text.text = "Joint already exist!";
          return;
        }

        //如果堆栈为空，则物体不能是叶子结点
        if (tempKey.Count == 0 && hitObject.transform.parent.transform.childCount == 1)
        {
          text.text = "The first joint should be a parent joint";
          return;
        }
        //必须是上一个点的子节点
        if(hitObject.transform.parent != tempParent){
          text.text = "the following joints must be children";
        }
        //除了第一个键，其他的必须是子物体
        //if (tempKey.Count > 0 && !tempDict.ContainsKey(hitObject.transform.parent.parent.name))
        //{
        //  text.text = "you can only choose child joints";
        //  return;
        //}
        //确认加入字典后通知对象变色
        tempDict.Add(hitObject.transform.parent.name, null);
        tempKey.Push(hitObject.transform.parent.gameObject);
        text.text = "joint added";
        tempParent = hitObject;
        var script = hitObject.GetComponent<JointDetectable>();

        script.OnRaycastHit();
        return;

      }
      //landmark的情况
      else
      {

        if (hitTag == "StaticLandmark")
        {
          //满了不能加入
          if (!tempDict.ContainsValue(null))
          {
            text.text = "All joints already have landmarks, add new joints";
            return;
          }

          GameObject[] keyArr = tempKey.ToArray();
          System.Array.Reverse(keyArr);
          //找int
          int index = FHVScript.freezeLandmarks.IndexOf(hitObject/*.transform.parent.gameObject*/);
          foreach(var key in keyArr)
          {
            if (tempDict[key.name] == null)
            {
              tempDict[key.name] = index;
              text.text = "landmark added";
              break;
            }
          }
          //确认加入字典后通知对象变色
          var script = hitObject.GetComponent<LandmarkDetectable>();
          if (script != null)
          {
            script.OnRaycastHit();
            return;
          }
          else
          {
            text.text = "no script found";
            return;
          }
        }
        else
        {
          text.text = "not a joint or landmark";
          return;
        }
      }


      // 调用该 GameObject 的 OnRaycastHit 方法（如果存在）

    }
    else
    {
      text.text = "raycast no return";
      //return;
    }
  }
  ////给字典添加一个joint键
  //private void JointInput(string jointName)
  //{
  //  if (tempDict.ContainsKey(jointName))
  //  {
  //    text.text = "joint already exist!";
  //    return;
  //  }
  //  tempDict.Add(jointName, null);
  //  tempKey.Push(jointName);
  //}
  ////向最后一个键添加landmark值
  //private void LandmarkInput(int landmarkIndex)
  //{

  //}
  //将当前字典加入列表
  public void addList()
  {

    if (tempDict.Count == 0)
    {
      text.text = $"No element in dictionary{tempDict.Count}";
      return;
    }
    //如果仅有joint的空值，不能加入列表
    else
    {
      if (tempDict.ContainsValue(null))
      {
        text.text = "mapping not finished yet";
        return;
      }
    }

    Dictionary<string, int?> tempDictCopy = new Dictionary<string, int?>(tempDict);
    mappingList.Add(tempDictCopy);

    tempDict.Clear();

    //todo：堆栈需要清空
    oldKey = tempKey;

    tempKey.Clear();
    var landmarks = FHVScript.freezeLandmarks;
    foreach(var landmark in landmarks)
    {
      var script = landmark.GetComponent<LandmarkDetectable>();
      script.OnUndo();
    }

    return;
  }

  //移出当前字典中的最后一组元素
  public void undoJoint()
  {
    text.text = "1";
    if (tempDict.Count == 0)
    {
      text.text = "Dictionary already empty";
      return;
    }
    //移除元素并通知所有节点改变颜色
    var undoJoint = tempKey.Pop();
    var jointScript = undoJoint.GetComponent<JointDetectable>();
    jointScript.OnUndo();

    var landmarkScript = FHVScript.freezeLandmarks[(int)tempDict[undoJoint.name]].GetComponent<LandmarkDetectable>();
    landmarkScript.OnUndo();

    tempDict.Remove(undoJoint.name);
    return;
  }
  //移出列表中的最后一组字典
  public void undoDict()
  {
    text.text = "2";
    if (mappingList.Count == 0)
    {
      text.text = "List already empty";
      return;
    }
    //根据物体名找到物体，然后根据索引号找到landmark
    GameObject[] keyArr = oldKey.ToArray();

    var undoDict = mappingList[mappingList.Count - 1];
    foreach (var pair in undoDict)
    {
      //先遍历找到要移除的joint
      GameObject undoJoint = null;
      foreach(GameObject go in keyArr)
      {
        if (go.name == pair.Key)
        {
          undoJoint = go;
          break;
        }
      }
      var jointScript = undoJoint.GetComponent<JointDetectable>();
      jointScript.OnUndo();

      var landmarkScript = FHVScript.freezeLandmarks[(int)pair.Value].GetComponent<LandmarkDetectable>();
      landmarkScript.OnUndo();

      //移除字典
      mappingList.RemoveAt(mappingList.Count - 1);
    }
    return;
  }




  //完成映射进入动画场景
  public void done()
  {
    text.text = "4";
    if (mappingList.Count == 0)
    {
      text.text = "No mapping set yet";
      return;
    }
    Destroy(spawnedObject);
    text.text = "4.1";
    MappingStore.MappingListStore = mappingList;
    this.enabled = false;

  }




  //显示当前list情况
  public void PrintMappingList()
  {
    int dictIndex = 1;
    string content = "";
    foreach (var dict in mappingList)
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

  public void onDoneClick()
  {
    _ = StartCoroutine(destorySpawnedObj());
  }

  IEnumerator  destorySpawnedObj()
  {
    Destroy(spawnedObject);
    yield return null;
    foreach (var pair in lines)
    {
      Debug.Log("Destroying Line: " + pair.Value.gameObject.name);
      Destroy(pair.Value.gameObject);
    }
    lines.Clear();



  }

}
