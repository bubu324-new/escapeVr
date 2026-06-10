using UnityEngine;

public class DoorOpenScript : MonoBehaviour
{
    [SerializeField] private Animator myDoor = null;
    [SerializeField] private bool openTrigger = false;

    private SnapToTrigger snap;

    private void OnTriggerEnter(Collider other)
    {
        snap = other.GetComponent<SnapToTrigger>();
        if(other.CompareTag("Objet1"))
        {
            if(openTrigger)
            {
                snap.Snap(gameObject.transform.position);
                myDoor.Play("DoorOpen", 0, 0.0f);
                if(myDoor != null)
                    DoorNavMeshGate.OpenPassageOn(myDoor.gameObject);
                gameObject.SetActive(false);
                openTrigger = false;
            }
        }
    }
}
