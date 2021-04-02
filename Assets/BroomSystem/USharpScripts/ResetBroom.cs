
using System;
using UdonSharp;
using UnityEngine;
using VRC.SDKBase;
using VRC.Udon;

public class ResetBroom : UdonSharpBehaviour {
    
    [SerializeField] private BroomController broomController;
    
    private Rigidbody _rbBroom;

    private void Start() {
        _rbBroom = gameObject.GetComponent<Rigidbody>();
    }

    public override void Interact() {
        Networking.LocalPlayer.UseAttachedStation();
        Networking.SetOwner(Networking.LocalPlayer, _rbBroom.gameObject);
    }
    
    public override void OnStationExited(VRCPlayerApi playerApi) {
        broomController.speed = 0;
        ResetLocal(_rbBroom);
    }
    
    private void ResetLocal(Rigidbody rb) {
        var rbTransform = rb.transform;
        rbTransform.localPosition = Vector3.zero;
        rbTransform.localRotation = Quaternion.identity;
        rb.velocity = Vector3.zero;
    }
}
