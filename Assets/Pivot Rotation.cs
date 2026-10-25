using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class PivotRotation : MonoBehaviour
{
    private List<GameObject> activeSide;
    private Vector3 localForwords;
    private Vector3 mouseRef;
    private bool dragging = false;
    private ReadCube readCube;
    private CubeState cubeState;
    private bool autorotating=false;
    private float sensitivity = 0.4f;
    private float speed = 300f;
    private Vector3 rotation;
    private Quaternion targetQutenion;
    // Start is called before the first frame update
    void Start()
    {
        readCube = FindObjectOfType<ReadCube>();
        cubeState = FindObjectOfType<CubeState>();
    }

    // Update is called once per frame
    void Update()
    {
        if (dragging)
        {
            SpinSide(activeSide);
            if (Input.GetMouseButtonUp(0))
            {
                dragging = false;
                RotateToRightAngle();
            }
                
        }
        if (autorotating)
        {
            AutoRotate();
        }
         
    }
    private void SpinSide(List<GameObject>side)
    {
        rotation = Vector3.zero;
        Vector3 mouseOffeset =( Input.mousePosition - mouseRef);
    
        if (side == cubeState.up)
        {
            rotation.y = (mouseOffeset.x + mouseOffeset.y) * sensitivity * 1;
        }
        if (side == cubeState.Down)
        {
            rotation.y = (mouseOffeset.x + mouseOffeset.y) * sensitivity * -1;
        }
        if (side == cubeState.left)
        {
            rotation.z = (mouseOffeset.x + mouseOffeset.y) * sensitivity * 1;
        }
        if (side == cubeState.right)
        {
            rotation.z = (mouseOffeset.x + mouseOffeset.y) * sensitivity * -1;
        }
        if (side == cubeState.front)
        {
            rotation.x = (mouseOffeset.x + mouseOffeset.y) * sensitivity * -1;
        }
        if (side == cubeState.back)
        {
            rotation.x = (mouseOffeset.x + mouseOffeset.y) * sensitivity * 1;
        }
        transform.Rotate(rotation, Space.Self);
        mouseRef = Input.mousePosition;
    }
    public void Rotate(List<GameObject> side)
    {
        activeSide = side;
        mouseRef=Input.mousePosition;
        dragging = true;

        localForwords = Vector3.zero - side[4].transform.parent.transform.localPosition;
    }
    public void RotateToRightAngle()
    {
        Vector3 vec = transform.localEulerAngles;
        vec.x=Mathf.Round(vec.x/90)*90;
        vec.y = Mathf.Round(vec.y / 90) * 90;
        vec.z = Mathf.Round(vec.z / 90) * 90;

        targetQutenion.eulerAngles=vec;
        autorotating=true;
    }
    private void AutoRotate()
    {   
        dragging=false;
        var step = speed * Time.deltaTime;
        transform.localRotation=Quaternion.RotateTowards(transform.localRotation,targetQutenion,step);

        if (Quaternion.Angle(transform.localRotation, targetQutenion) <= 1)
        {
            transform.localRotation = targetQutenion;
            cubeState.PutDown(activeSide,transform.parent);
            readCube.ReadState();
            autorotating = false;
            dragging = false;
        }
    }
}
