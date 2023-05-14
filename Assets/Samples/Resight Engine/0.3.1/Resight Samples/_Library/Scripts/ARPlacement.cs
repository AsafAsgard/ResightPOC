/*
===================================================================
Unity Assets by Resight: https://resight.io
===================================================================
*/

using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.XR.ARFoundation;

public class ARPlacement : MonoBehaviour
{
    public GameObject ObjectToPlace;
    public ARRaycastManager RaycastManager;

    private void Update()
    {
        if (Input.touchCount>0 && Input.GetTouch(0).phase==TouchPhase.Began)
        {
            List<ARRaycastHit> touches = new List<ARRaycastHit>();
            RaycastManager.Raycast(Input.GetTouch(0).position, touches, UnityEngine.XR.ARSubsystems.TrackableType.Planes);

            if (touches.Count > 0)
                GameObject.Instantiate(ObjectToPlace, touches[0].pose.position, touches[0].pose.rotation);
        }
    }
}
