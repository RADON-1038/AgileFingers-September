using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class LandmarkDetectable : MonoBehaviour
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
