using UnityEngine;

public class room3 : MonoBehaviour
{
    private bool laser1 = false;
    private bool laser2 = false;
    private bool doorOpen = false;
    [SerializeField] private Animator doorAnim = null;
    [SerializeField] private AudioSource winton;

    public void SetLaser()
    {
        if(laser1 == false)
            laser1 = true;
        else
            laser2 = true;
    }

    public void DesactivateLaser()
    {
        if(laser1 == true)
            laser1 = false;
    }

    private void Update()
    {
        if(laser1 && laser2 && !doorOpen)
        {
            doorOpen = true;
            doorAnim.Play("DoorOpen", 0, 0.0f);
            if(doorAnim != null)
                DoorNavMeshGate.OpenPassageOn(doorAnim.gameObject);
            winton.Play();
        }
    }
}
