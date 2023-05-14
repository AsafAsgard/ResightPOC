/*
===================================================================
Unity Assets by Resight: https://resight.io
===================================================================
*/

using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.XR.ARFoundation;

public class PlaceAndMoveObject : MonoBehaviour
{
    public ARRaycastManager raycastManager;
    public GameObject objectToPlace;

    private List<GameObject> objectsInScene = new List<GameObject>();
    private GameObject selectedObject;
    private Vector2 touchStartPos;
    private Vector3 objectStartPos;
    private Quaternion objectStartRot;
    private bool isObjectSelected = false;

    void Update()
    {
        if (Input.touchCount > 0)
        {
            Touch touch = Input.GetTouch(0);

            if (touch.phase == TouchPhase.Began)
            {
                List<ARRaycastHit> hits = new List<ARRaycastHit>();
                raycastManager.Raycast(touch.position, hits, UnityEngine.XR.ARSubsystems.TrackableType.Planes);

                bool canPlaceObject = true;
                foreach (GameObject obj in objectsInScene)
                {
                    // Check if the touch position is within a small distance from an existing object
                    if (Vector3.Distance(obj.transform.position, hits[0].pose.position) < 0.05f)
                    {
                        selectedObject = obj;
                        objectStartPos = selectedObject.transform.position;
                        objectStartRot = selectedObject.transform.rotation;
                        touchStartPos = touch.position;
                        isObjectSelected = true;
                        canPlaceObject = false;
                        break;
                    }
                }

                if (canPlaceObject)
                {
                    // Instantiate the object at the hit position and add it to the list of objects in the scene
                    GameObject newObject = GameObject.Instantiate(objectToPlace, hits[0].pose.position, hits[0].pose.rotation);
                    objectsInScene.Add(newObject);

                    // Select the object and record its initial position and rotation
                    selectedObject = newObject;
                    objectStartPos = selectedObject.transform.position;
                    objectStartRot = selectedObject.transform.rotation;
                    touchStartPos = touch.position;
                    isObjectSelected = true;
                }
            }

            if (isObjectSelected && touch.phase == TouchPhase.Moved)
            {
                Vector2 touchDelta = touch.position - touchStartPos;
                Vector3 newPos = objectStartPos + new Vector3(touchDelta.x * 0.001f, 0, touchDelta.y * 0.001f);
                selectedObject.transform.position = newPos;
            }

            if (isObjectSelected && touch.phase == TouchPhase.Ended)
            {
                isObjectSelected = false;
            }
        }
    }

    public void ClearScene()
    {
        // Destroy all objects in the scene and clear the list of objects
        foreach (GameObject obj in objectsInScene)
        {
            GameObject.Destroy(obj);
        }
        objectsInScene.Clear();
    }
}

