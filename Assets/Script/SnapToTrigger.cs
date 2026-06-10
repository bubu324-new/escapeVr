using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Transformers;

public class SnapToTrigger : MonoBehaviour
{
    private XRGrabInteractable interactable;
    private XRGeneralGrabTransformer transformer;
    private Rigidbody rb;
    private bool isSnapped;

    private void Start()
    {
        interactable = GetComponent<XRGrabInteractable>();
        transformer = GetComponent<XRGeneralGrabTransformer>();
        rb = GetComponent<Rigidbody>();
    }

    private void Update()
    {
        if (isSnapped)
        {
            gameObject.transform.RotateAroundLocal(new Vector3(0, 1, 0), 0.05f);
        }
    }

    public void Snap(Vector3 trigger_pos)
    {
        interactable.enabled = false;
        transformer.enabled = false;
        gameObject.transform.position = new Vector3(trigger_pos.x, trigger_pos.y + 0.4f, trigger_pos.z);
        gameObject.transform.rotation = new Quaternion(0, 0, 0, 0);
        rb.constraints = RigidbodyConstraints.FreezeAll;
        isSnapped = true;
    }
}