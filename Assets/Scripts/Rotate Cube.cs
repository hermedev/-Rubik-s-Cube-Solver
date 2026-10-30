using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class RotateCube : MonoBehaviour
{
    Vector2 firstPerssPos;
    Vector2 secondPerssPos;
    Vector2 currentSWipe;
    Vector3 previousMousePosition;
    Vector3 mouseDelta;
    public GameObject target;
    float speed = 200f;
    void Start()
    {
        
    }

    // Update is called once per frame
    void Update()
    {
        Swipe();
        Drag();

    }

    void Drag()
    {
        if (Input.GetMouseButton(1))
        {
            mouseDelta = Input.mousePosition - previousMousePosition;
            mouseDelta *= 0.1f;
            transform.rotation = Quaternion.Euler(mouseDelta.y, -mouseDelta.x, 0) * transform.rotation;
        }
        else
        {
            if (transform.rotation != target.transform.rotation)
            {
                var step = speed * Time.deltaTime;
                transform.rotation = Quaternion.RotateTowards(transform.rotation, target.transform.rotation, step);
            }
        }
        previousMousePosition = Input.mousePosition;
    }
    void Swipe()
        {
            if (Input.GetMouseButtonDown(1))
            {
                firstPerssPos = new Vector2(Input.mousePosition.x, Input.mousePosition.y);
                //  print(firstPerssPos);
            }
            if (Input.GetMouseButtonUp(1))
            {

                secondPerssPos = new Vector2(Input.mousePosition.x, Input.mousePosition.y);
                currentSWipe = new Vector2(secondPerssPos.x - firstPerssPos.x, secondPerssPos.y - firstPerssPos.y)
           ;
                currentSWipe.Normalize();
                if (LeftSwipe(currentSWipe))
                {
                    target.transform.Rotate(0, 90, 0, Space.World);
                }
                else if (RightSwipe(currentSWipe))
                {
                    target.transform.Rotate(0, -90, 0, Space.World);
                }else if (UpLeftSwipe(currentSWipe))
                {
                target.transform.Rotate(90, 0, 0, Space.World);
                }else if (UpRightSwipe(currentSWipe))
                {
                target.transform.Rotate(0, 0, -90, Space.World);
                }else if (DownLeftSwipe(currentSWipe))
                {
                target.transform.Rotate(0, 0, 90, Space.World);
                }else if (DownRightSwipe(currentSWipe))
            {
                target.transform.Rotate(-90, 0, 0, Space.World);
            }
            }
        }
        bool LeftSwipe(Vector2 swipe)
        {
            return currentSWipe.x < 0 && currentSWipe.y > -0.5f && currentSWipe.y < 0.5f;
        }
        bool RightSwipe(Vector2 swipe)
        {
            return currentSWipe.x > 0 && currentSWipe.y > -0.5f && currentSWipe.y < 0.5f;
        }
    bool UpLeftSwipe(Vector2 swipe)
    {
        return currentSWipe.y > 0 && currentSWipe.x < 0f;
    }
    bool UpRightSwipe(Vector2 swipe)
    {
        return currentSWipe.y > 0 && currentSWipe.x >0f;
    }
    bool DownLeftSwipe(Vector2 swipe)
    {
        return currentSWipe.y < 0 && currentSWipe.x < 0f;
    }
    bool DownRightSwipe(Vector2 swipe)
    {
        return currentSWipe.y < 0 && currentSWipe.x >0f;
    }
}
