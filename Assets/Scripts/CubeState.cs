using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class CubeState : MonoBehaviour
{
  public List<GameObject> front= new List<GameObject>();
    public List<GameObject> back = new List<GameObject>();
    public List<GameObject> up = new List<GameObject>();
    public List<GameObject> Down = new List<GameObject>();
    public List<GameObject> left = new List<GameObject>();
    public List<GameObject> right = new List<GameObject>();
 
    void Start()
    {
        
    }

    // Update is called once per frame
    void Update()
    {
        
    }

    public void PickUp(List<GameObject> cubeSide)
    {
        foreach(GameObject face in cubeSide)
        {
            if(face != cubeSide[4])
            {
                face.transform.parent.transform.parent = cubeSide[4].transform.parent;
            }
        }
        cubeSide[4].transform.parent.GetComponent<PivotRotation>().Rotate(cubeSide);

    }
    public void PutDown(List<GameObject>LittileCubes,Transform pivot)
    {
        foreach(GameObject littilecube in LittileCubes)
        {
            if (littilecube != LittileCubes[4])
            {
                littilecube.transform.parent.transform.parent= pivot;
            }
        }    
    }
  
}
