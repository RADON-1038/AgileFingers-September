using System.Collections;
using System.Collections.Generic;
using UnityEngine;

using UnityEngine.Events;
using UnityEngine.UI;

using UnityEngine.XR.ARFoundation;
using UnityEngine.XR.ARSubsystems;

using UnityEngine.Rendering;

public class GetPixelDepth : MonoBehaviour
{

  [SerializeField] private AROcclusionManager occlusionManager;
  [SerializeField] private Text _debugDepthText;
  [SerializeField] private ARCameraManager _cameraManager;
  [SerializeField] private RawImage _rawImage;
  private Texture2D texture;
  public Text text;
  //8.28
  private float neighbourSum = 0f;
  //https://docs.unity3d.com/Packages/com.unity.xr.arsubsystems@4.2/api/UnityEngine.XR.ARSubsystems.XRTextureDescriptor.html
  private XRTextureDescriptor _descriptor;
  private XROcclusionSubsystem xROcclusionSubsystem;

  int pixelDepth;

  //!!!!!!!!!!!!!!!!!!new in 8.18
  public float[] publicDepthData;
  public float[,] publicDepthData2D;

  void OnEnable()
  {
    _cameraManager.frameReceived += OnCameraFrameEventReceived;


  }

  void OnDisable()
  {
    _cameraManager.frameReceived -= OnCameraFrameEventReceived;
  }
  private void Awake()
  {
    texture = new Texture2D(256, 192);
  }


  void Update()
  {

  }

  //float GetDepthOnScreen(Texture2D depthTexture, Vector2 screenPosition)
  //{
  //  // Convert screen position to UV coordinates
  //  float u = screenPosition.x / Screen.width;
  //  float v = screenPosition.y / Screen.height;

  //   // Sample depth texture
  //  float distanceInMeters = depthTexture.GetPixelBilinear(u, v).r;

  //  return distanceInMeters;
  //}

  private void OnCameraFrameEventReceived(ARCameraFrameEventArgs eventArgs)
  {
    Texture2D depthTexture = occlusionManager.environmentDepthTexture;

    if (depthTexture)
    {

      RequestDepthData(depthTexture);

      //_rawImage.texture = depthTexture;
      //_rawImage.texture = flipTexture(depthTexture);
    }
  }

  void RequestDepthData(Texture2D depthTexture)
  {
    AsyncGPUReadback.Request(depthTexture, 0, TextureFormat.RFloat, OnCompleteReadback);
  }

  void OnCompleteReadback(AsyncGPUReadbackRequest request)
  {
    if (request.hasError)
    {
      Debug.Log("Failed to read GPU data");
      return;
    }
    float[] depthData = request.GetData<float>().ToArray();
    float[,] depthData2D = new float[192, 256];

    for (int y = 0; y < 192; y++)
    {
      for (int x = 0; x < 256; x++)
      {
        depthData2D[y, x] = depthData[(y * 256) + x];
        Color color = new Color(depthData2D[y, x], depthData2D[y, x], depthData2D[y, x], 1);
        texture.SetPixel(x, y, color);
      }
    }
    texture.Apply();
    _rawImage.texture = texture;

    //!!!!!!!!!!!!!!!!!!!!new in 8.18
    publicDepthData = depthData;
    publicDepthData2D = depthData2D;

  }

  //!!!!!!!!!!!!!!!new in 8.18
  public float getDepthByPixel(float x, float y)
  {
    neighbourSum = 0f;
    //text.text = "value of index=" + (int)Mathf.Round((1 - y) * 192) + "," + (int)Mathf.Round(x * 256);
    for(int i = -1; i < 2; i++)
    {
      for(int j = -1; j < 1; j++)
      {
        neighbourSum += publicDepthData2D[((int)Mathf.Round((1 - y) * 192)) + i, ((int)Mathf.Round(x * 256)) + j];
      }
    }

    return neighbourSum / 9f;
    //publicDepthData2D[(int)Mathf.Round((1 - y) * 192), (int)Mathf.Round(x * 256)];
  }

}


