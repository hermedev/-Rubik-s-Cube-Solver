using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class ReadCube : MonoBehaviour
{
    public Transform tUp;
    public Transform tDown;
    public Transform tLeft;
    public Transform tRight;
    public Transform tFront;
    public Transform tBack;

    private List<GameObject> frontRays = new List<GameObject>();
    private List<GameObject> backRays = new List<GameObject>();
    private List<GameObject> upRays = new List<GameObject>();
    private List<GameObject> downRays = new List<GameObject>();
    private List<GameObject> leftRays = new List<GameObject>();
    private List<GameObject> rightRays = new List<GameObject>();
    private int layermask = 1 << 8;
    CubeState cubestate;
    CubeMap cubeMap;
    public GameObject emtyGo;
    void Start()
    {
        SetRayTransform();
        cubestate = GetComponent<CubeState>();
        cubeMap = FindObjectOfType<CubeMap>();
      
    }
    public void ReadState()
    {
         cubestate= FindObjectOfType<CubeState>();
        cubeMap= FindObjectOfType<CubeMap>();
        cubestate.up = ReadFace(upRays, tUp);
        cubestate.Down=ReadFace(downRays, tDown);
        cubestate.left=ReadFace(leftRays, tLeft);
        cubestate.right=ReadFace(rightRays, tRight);
        cubestate.front=ReadFace(frontRays, tFront);
        cubestate.back= ReadFace(backRays, tBack);

        cubeMap.set();
    }
    // Update is called once per frame
    void Update()
    {
       



    }
    void SetRayTransform()
    {
        upRays = BulidRays(tUp, new Vector3(90, 90, 0));
        downRays = BulidRays(tDown, new Vector3(270, 90, 0));
        leftRays = BulidRays(tLeft, new Vector3(0, 180, 0));
        rightRays  = BulidRays(tRight, new Vector3(0, 0, 0));
        frontRays = BulidRays(tFront, new Vector3(0, 90, 0));
       backRays = BulidRays(tBack, new Vector3(0, 270, 0));
    }
    List<GameObject>BulidRays(Transform rayTransform,Vector3 direction)
    {
        int rayCount = 0;
        List<GameObject> rays = new List<GameObject>();

        for (int y = 1; y > -2; y--)
        {
            for(int x = -1; x < 2; x++)
            {
                Vector3 startPOs = new Vector3(rayTransform.localPosition.x+x
                                             ,rayTransform.localPosition.y+y,
                                              rayTransform.localPosition.z );
                GameObject rayStart = Instantiate(emtyGo,startPOs,Quaternion.identity,rayTransform);
                rayStart.name=rayCount.ToString();
                rays.Add(rayStart);
                rayCount++;
            }
        }
   rayTransform.localRotation=Quaternion.Euler(direction);
        return rays;
    }
    public List<GameObject>ReadFace(List<GameObject> rayStarts,Transform rayTransform)
    {
        List<GameObject> faceHit = new List<GameObject>();
        foreach(GameObject raystart in rayStarts)
        {
            Vector3 ray = raystart.transform.position;
            RaycastHit hit;
            if (Physics.Raycast(ray,rayTransform.forward, out hit, Mathf.Infinity, layermask))
            {
                Debug.DrawRay(ray, rayTransform.forward * hit.distance, color: Color.yellow);
                faceHit.Add(hit.collider.gameObject);
              //  print(hit.collider.gameObject.name);
            }
            else
            {
                Debug.DrawRay(ray, rayTransform.forward * 1000, color: Color.green);
            }
        }
     
       return faceHit;
    
    }


}