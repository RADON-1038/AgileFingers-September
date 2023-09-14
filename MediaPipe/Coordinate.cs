using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Mediapipe;
using UnityEngine.XR.ARFoundation;
using UnityEngine.XR.ARSubsystems;
using UnityEngine.UI;

public class Coordinate : MonoBehaviour
{
  //objects
  public Camera camera;
  public ARCameraManager arcamManager;
  //scripts
  public GetPixelDepth dScript;
  public HandSkeletonVisualize HSVScript;
  //params
  private Vector3 front, up, ray;
  private float ray_norm;
  private float x_halfscale, y_halfscale;

  private Vector3 x_offset, y_offset, tempray;
  private float depth,filteredDepth,dot,dist;
  private float fov;

  //private int xPixel, yPixel;

  //8.20

  public Text calculatorInfo;
  public Text cal1;
  public Text cal2;
  public Text cal3;
  public Text cal4;
  public Text startText;
  //private int times = 0;


  //filter
  //private Queue<float> filterQueue;
  //private float sum = 0;
  private int windowSize=3;
  private int signalNum = 0;
  OnlineMeanFilter[] meanFilter_D, meanFilter_X, meanFilter_Y;

  private void Start()
  {
    //filter
    meanFilter_D = new OnlineMeanFilter[21];
    meanFilter_X = new OnlineMeanFilter[21];
    meanFilter_Y = new OnlineMeanFilter[21];


    for(int i = 0; i < 21; i++)
    {
      meanFilter_D[i] = new OnlineMeanFilter(windowSize);
      meanFilter_X[i] = new OnlineMeanFilter(windowSize);
      meanFilter_Y[i] = new OnlineMeanFilter(windowSize);
    }
  }
  private void Update()
  {

  }
  public void infoPrint()
  {
    cal1.text = $"x_offset={x_offset}, y_offset={y_offset}";
    cal2.text = $"ray= {tempray}\nray_norm= {ray_norm}";
    cal3.text = $"fov={fov}";
    cal4.text = $"depth= {depth}\n resolution={arcamManager.currentConfiguration.GetValueOrDefault().resolution}";
    //startText.text = $"camresolution:{arcamManager.currentConfiguration.GetValueOrDefault().resolution}\n" +
    //  $"Device screen resolution:{Screen.width}*{Screen.height}";

  }

  //public Vector3 vectorCalculate(float x, float y)//输入的xy是已经[0,1]归一化的mediapipe坐标
  //{

  //  fov = CalculateFOV();

  //  camera = arcamManager.GetComponent<Camera>();
  //  if (camera != null)
  //  {

  //    var aspectRatio = 1920f / 1440f;
  //    y_halfscale = Mathf.Tan(fov / 2.0f);
  //    x_halfscale = y_halfscale * aspectRatio;

  //  }
  //  else
  //  {
  //    Debug.Log("Camera not found");
  //  }




  //  front = camera.transform.forward;
  //  up = camera.transform.up;
  //  x_offset = (0.5f - x) * 2f * x_halfscale * Vector3.Cross(up, front);
  //  y_offset = ((0.5f - y) * 2f * y_halfscale * up);
  //  ray = front + (((0.5f - x) * 2f * x_halfscale * Vector3.Cross(up, front)) + ((0.5f - y) * 2f * y_halfscale * up));

  //  ray_norm = (float)System.Math.Sqrt((ray.x * ray.x) + (ray.y * ray.y) + (ray.z * ray.z));
  //  ray /= ray_norm;
  //  depth = dScript.getDepthByPixel(x, y);

  //  //filter
  //  filteredDepth = meanFilter.Filter(depth);

  //  dot = Vector3.Dot(front, ray);
  //  dist = filteredDepth / dot;
  //  return ray * dist;

  //}


  //8.29filter-version
  public Vector3 vectorCalculate(float x, float y)//输入的xy是已经[0,1]归一化的mediapipe坐标
  {
    //8.22

    //filter
    x = meanFilter_X[signalNum].Filter(x);
    y = meanFilter_Y[signalNum].Filter(y);
   
    fov = CalculateFOV();

    camera = arcamManager.GetComponent<Camera>();
    if (camera != null)
    {
      //float fov = camera.fieldOfView * Mathf.Deg2Rad;
      var aspectRatio = 1920f / 1440f;//2388f / 1668f;
      y_halfscale = Mathf.Tan(fov / 2.0f);
      x_halfscale = y_halfscale * aspectRatio;
      //startText.text = $"fov= {fov}\naspectratio={aspectRatio}\n y_half={y_halfscale}, x_half={x_halfscale}";
    }
    else
    {
      Debug.Log("Camera not found");
    }



    //calculatorInfo.text = "Calculator working!";
    front = camera.transform.forward;
    up = camera.transform.up;
    x_offset = (0.5f - x) * 2f * x_halfscale * Vector3.Cross(up, front);
    y_offset = ((0.5f - y) * 2f * y_halfscale * up);
    ray = front + (((0.5f - x) * 2f * x_halfscale * Vector3.Cross(up, front)) + ((0.5f - y) * 2f * y_halfscale * up));
    //tempray = ray;
    ray_norm = (float)System.Math.Sqrt((ray.x * ray.x) + (ray.y * ray.y) + (ray.z * ray.z));
    ray /= ray_norm;
    depth = dScript.getDepthByPixel(x, y);//这里访问的是按照mediapipe提供的归一化xy坐标计算的像素点位置，回传的是该点深度值（米）

    //filter
    filteredDepth = meanFilter_D[signalNum].Filter(depth);
    if (signalNum == 20)
    {
      signalNum = 0;
    }
    else
    {
      signalNum++;
    }


    dot = Vector3.Dot(front, ray);
    dist = filteredDepth / dot;
    return ray * dist;

  }










  private float CalculateFOV()
  {
    if (arcamManager.TryGetIntrinsics(out var intrinsics))
    {
      var vFOV = 2.0f * Mathf.Atan(intrinsics.resolution.y / (2.0f * intrinsics.focalLength.y));
      startText.text = $"resolution={intrinsics.resolution.x}*{intrinsics.resolution.y}";
      return vFOV;
    }
    else
    {
      return 114514f;
    }
  }


}

