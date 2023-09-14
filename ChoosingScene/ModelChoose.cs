using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.XR.ARFoundation;

public class ModelChoose : MonoBehaviour
{
  public int _modelIndex = 0;
  private Vector3 _modelRotation = new Vector3(0f, 20f, 0f);
  public GameObject[] models = new GameObject[4];
  public Transform parent;
  public ARCameraManager aRCameraManager;
  public Camera camera;
  private GameObject currentObject;


  private void Start()
  {
    models[0].SetActive(true);
    currentObject = models[0];
    //currentObject = Instantiate(models[_modelIndex], parent.position + new Vector3(0f, 0f, 1f), Quaternion.identity, parent);
  }


  private void Update()
  {
    currentObject.transform.Rotate(_modelRotation * Time.deltaTime);
  }


  public void OnLeftClicked()
  {
    //Destroy(currentObject);
    models[_modelIndex].SetActive(false);
    if (_modelIndex == 0)
    {
      _modelIndex = 3;
    }
    else
    {
      _modelIndex--;
    }
    models[_modelIndex].SetActive(true);
    currentObject = models[_modelIndex];
    //camera = aRCameraManager.GetComponent<Camera>();
    //var front = camera.transform.forward;
    //currentObject = Instantiate(models[_modelIndex], parent.position + (front * 0.5f), Quaternion.identity, parent) as GameObject;
  }


  public void OnRightClicked()
  {
    //Destroy(currentObject);
    models[_modelIndex].SetActive(false);
    if (_modelIndex == 3)
    {
      _modelIndex = 0;
    }
    else
    {
      _modelIndex++;
    }
    models[_modelIndex].SetActive(true);
    currentObject = models[_modelIndex];
    //camera = aRCameraManager.GetComponent<Camera>();
    //var front = camera.transform.forward;
    //currentObject = Instantiate(models[_modelIndex], parent.position + (front * 0.5f), Quaternion.identity, parent) as GameObject;
  }


}
