
using UdonSharp;
using UnityEngine;
using VRC.SDKBase;
using VRC.Udon;

public class ResetRigidBody : UdonSharpBehaviour {
    
    [SerializeField] private Rigidbody targetRb;

    public override void Interact() {
        ResetLocal(targetRb);
    }

    private void ResetLocal(Rigidbody rb) {
        var rbTransform = rb.transform;
        rbTransform.localPosition = Vector3.zero;
        rbTransform.localRotation = Quaternion.identity;
        rb.velocity = Vector3.zero;
    }
}
