/*
===================================================================
Unity Assets by Resight: https://resight.io
===================================================================
*/

using UnityEngine;

public class ThrowingBalls : MonoBehaviour
{
    [SerializeField]
    private Camera arCamera;

    [SerializeField]
    private GameObject[] objectToThrow;

    [SerializeField]
    private float force = 200.0f;

    void Update()
    {
        if (Input.touchCount <= 0) return;

        var touch = Input.GetTouch(0);

        if (touch.phase == TouchPhase.Began)
        {
            LaunchRandomProjectile();
        }
    }

    void LaunchRandomProjectile()
    {
        var prefab = objectToThrow [Random.Range(0, objectToThrow.Length)];
        var projectile = Instantiate(prefab, arCamera.transform.position, Quaternion.identity);

        //physics 
        var projectileRigidBody = projectile.GetComponent<Rigidbody>();
        projectileRigidBody.isKinematic = false;
        projectileRigidBody.AddForce(arCamera.transform.forward * force);
    }
}
