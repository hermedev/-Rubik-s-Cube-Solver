using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class SelectFace : MonoBehaviour
{
    CubeState cubeState;
    ReadCube readCube;
    int layerMask = 1 << 8;

    private void Start()
    {
        readCube=FindObjectOfType<ReadCube>();
        cubeState = FindObjectOfType<CubeState>();
    }
    // Update is called once per frame
    void Update()
    {
        if (Input.GetMouseButtonDown(0))
        {
            readCube.ReadState();
            RaycastHit hit;
        Ray RAY=Camera.main.ScreenPointToRay(Input.mousePosition);

            if(Physics.Raycast(RAY,out hit, 100.0f, layerMask))
            {
                GameObject face=hit.collider.gameObject;

                List<List<GameObject>> cubesides = new List<List<GameObject>>()
                {
                    cubeState.up,
                    cubeState.Down,
                    cubeState.front,
                    cubeState.back,
                    cubeState.right,
                    cubeState.left

                };
                foreach(List<GameObject> cubeSide in cubesides)
                {
                    if(cubeSide.Contains(face)) {
                        cubeState.PickUp(cubeSide);
                    }

                }
            }
        }

    }
  
}
