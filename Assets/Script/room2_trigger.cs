using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class room2_trigger : MonoBehaviour
{
    [SerializeField] private bool checkSphere;
    [SerializeField] private bool checkCube;
    [SerializeField] private bool checkPyramid;
    [SerializeField] private room2 roomDatas;
    [SerializeField] private bool checkCompile;

    private SnapToTrigger snap;

    private void OnTriggerEnter(Collider collision)
    {
        snap = collision.GetComponent<SnapToTrigger>();
        if (checkSphere && collision.CompareTag("Sphere")) { 
            roomDatas.SetSphereCheck();
            snap.Snap(gameObject.transform.position);
        }
        else if (checkPyramid && collision.CompareTag("Pyramid")) 
        { 
            roomDatas.SetPyramidCheck(); 
            snap.Snap(gameObject.transform.position);
        }
        else if (checkCube && collision.CompareTag("Cube")) 
        { 
            roomDatas.SetCubeCheck(); 
            snap.Snap(gameObject.transform.position);
        }
        if (checkCompile) {
            checkCompile = false;
        }
    }
}
