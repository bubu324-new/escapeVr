using UnityEngine;

public class room2 : MonoBehaviour
{
    private bool SphereChecked;
    private bool CubeChecked;
    private bool PyramidChecked;
    private bool doorOpen;
    [SerializeField] private Animator doorAnim = null;

    public void SetSphereCheck() { SphereChecked = true; }

    public void SetCubeCheck() { CubeChecked = true; }

    public void SetPyramidCheck() { PyramidChecked = true; }

    private void Update()
    {
        if(SphereChecked && CubeChecked && PyramidChecked && !doorOpen)
        {
            doorOpen = true;
            doorAnim.Play("DoorOpen", 0, 0.0f);
            if(doorAnim != null)
                DoorNavMeshGate.OpenPassageOn(doorAnim.gameObject);
        }
    }
}
