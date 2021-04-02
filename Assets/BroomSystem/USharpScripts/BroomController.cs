using System;
using System.Diagnostics.CodeAnalysis;
using UdonSharp;
using UnityEditor;
using UnityEngine;
using VRC.SDKBase;
using VRC.Udon;

public class BroomController : UdonSharpBehaviour {
    public float maxSpeed; //最高速を決める変数(km/h)
    public float accelPerSecond; //加速力を決める変数(km/h*s)
    public float speed;

    [SerializeField] private Rigidbody rbBroom;
    [SerializeField] private Transform[] raycastYStartList;
    [SerializeField] private Transform raycastXRotateStart;
    [SerializeField] private Transform raycastXStart;
    [SerializeField] private Transform raycastMinusXStart;
    [SerializeField] private LayerMask layerMask;
    [SerializeField] private float mass = 2.0f;
    [SerializeField] private Transform controllerTarget;

    private Rigidbody _rbController;
    private bool _isPickup;
    private bool _isPickupTriggered;
    private Vector3[] _preNormals;
    private RaycastHit[] _targetHitList;
    private float[] _targetYList;

    private const float BROOM_MIN_GROUND = 2.0f;
    private const float MaxYDistance = 4.0f;
    private const float MaxYRotateDistance = 4.0f;
    private float _accY;
    private float _accX;

    void Start() {
        speed = 0;
        _rbController = gameObject.GetComponent<Rigidbody>();
        _isPickup = false;
        _isPickupTriggered = false;
        _targetHitList = new RaycastHit[raycastYStartList.Length];
        _targetYList = new float[raycastYStartList.Length];
        _preNormals = new Vector3[2];
    }

    public override void OnPickupUseDown() {
        _isPickupTriggered = true;
    }

    public override void OnPickupUseUp() {
        _isPickupTriggered = false;
    }

    public override void OnPickup() {
        _isPickup = true;
    }

    public override void OnDrop() {
        _isPickup = false;
    }

    private void ResetLocal(Rigidbody rb) {
        var rbTransform = rb.transform;
        rbTransform.localPosition = Vector3.zero;
        rb.velocity = Vector3.zero;
    }

    private void Update() {
        //コントローラーを離した場合の位置リセット
        if (!_isPickup) {
            ResetLocal(_rbController);
        }
    }

    private void FixedUpdate() {
        var velocity = rbBroom.velocity;

        //速さの計算
        if (_isPickupTriggered) {
            speed += accelPerSecond * Time.deltaTime;
            if (speed > maxSpeed) speed = maxSpeed;
        } else {
            speed -= accelPerSecond * Time.deltaTime / 2;
            if (speed < 0) speed = 0;
        }
        var nextVelocity = transform.forward * speed;

        //Y方向
        nextVelocity = CalcVelocityY(velocity, nextVelocity);

        //横方向
        nextVelocity = CalcVelocityX(velocity, nextVelocity);

        //速度を更新
        rbBroom.velocity = nextVelocity;

        //コントローラーによる回転の適用
        ApplyControllerRotate();
    }

    [SuppressMessage("ReSharper", "Unity.InefficientPropertyAccess")]
    private void ApplyControllerRotate() {
        var rbB = rbBroom.transform;
        var rbBRot = rbB.transform.rotation;

        //コントローラーによるY軸回転の適用
        var targetPosition = controllerTarget.position;
        targetPosition.y = rbB.position.y;
        var controllerVec = targetPosition - rbB.position;
        var lookRotation = Quaternion.LookRotation(controllerVec, rbB.transform.up);
        var rot1 = Quaternion.Slerp(rbBRot, lookRotation, Time.deltaTime);
        rbB.transform.rotation = rot1;
        
        //真下のRaycastの法線方向によるX,Z軸回転の適用
        RaycastHit hit;
        var t = raycastXRotateStart;
        var scale = t.lossyScale.x * 0.5f;
        var isHit = Physics.SphereCast(t.position, scale, t.forward,
            out hit, MaxYRotateDistance, layerMask);
        if (isHit) {
            //保存した複数回分の平均で取得
            var averageNormal = Vector3Average(_preNormals);
            //法線方向にローカルYを補正
            var nextRotation = Quaternion.Slerp(
                Quaternion.FromToRotation(rbB.up, averageNormal), Quaternion.identity, 0.1f) * rbB.transform.rotation;
            //現在のローテーションに適用
            rbB.transform.rotation = Quaternion.Slerp(rbB.transform.rotation, nextRotation, Time.deltaTime);
            //複数回分を保存
            Vector3Push(_preNormals, hit.normal);
        } else {
            //保存した複数回分の平均で取得
            var averageNormal = Vector3Average(_preNormals);
            //重力方向にローカルYを補正
            var nextRotation = Quaternion.Slerp(
                Quaternion.FromToRotation(rbB.up, averageNormal), Quaternion.identity, 0.05f) * rbB.transform.rotation;
            //現在のローテーションに適用
            rbB.transform.rotation = Quaternion.Slerp(rbB.transform.rotation, nextRotation, Time.deltaTime);
            //複数回分を保存
            Vector3Push(_preNormals, Vector3.up);
        }
    }

    private Vector3 Vector3Average(Vector3[] list) {
        var t = list[0];
        for (var i = 1; i < list.Length; i++) {
            t += list[i];
        }
        t *= (1.0f / list.Length);
        return t;
    }

    private void Vector3Push(Vector3[] list, Vector3 next) {
        for (var i = 0; i < list.Length - 1; i++) {
            list[i] = list[i + 1];
        }
        list[list.Length - 1] = next;
    }

    private Vector3 CalcVelocityY(Vector3 velocity, Vector3 nextVelocity) {
        //Raycastの取得
        for (var i = 0; i < raycastYStartList.Length; i++) {
            RaycastHit hit;
            var t = raycastYStartList[i];
            var scale = t.lossyScale.x * 0.5f;
            var isHit = Physics.SphereCast(t.position, scale, t.forward,
                out hit, MaxYDistance, layerMask);
            if (isHit) {
                _targetHitList[i] = hit;
                _targetYList[i] = hit.point.y;
            } else {
                _targetYList[i] = -Mathf.Infinity;
            }
        }

        if (IsBroomNotGrounded(_targetYList)) {
            //一定以上の高さの場合は重力での落下
            nextVelocity.y += Physics.gravity.y * Time.deltaTime * mass;
        } else {
            //一定以下の高さの場合は距離で加速度計算
            var index = GetIndexMaxY(_targetYList);
            var target = _targetYList[index];
            var raycastYStartPos = raycastYStartList[index].position.y;
            nextVelocity.y = target + BROOM_MIN_GROUND - raycastYStartPos - (velocity.y * Time.deltaTime);
        }

        return nextVelocity;
    }

    private bool IsBroomNotGrounded(float[] targetYList) {
        for (var i = 0; i < raycastYStartList.Length; i++) {
            if (targetYList[i] + BROOM_MIN_GROUND > raycastYStartList[i].position.y) {
                return false;
            }
        }
        return true;
    }

    private int GetIndexMaxY(float[] targetYList) {
        var index = 0;
        for (var i = 0; i < targetYList.Length - 1; i++) {
            if (targetYList[i] < targetYList[i + 1]) {
                index = i + 1;
            }
        }
        return index;
    }

    private const float BROOM_MIN_SIDE = 1.0f;

    private Vector3 CalcVelocityX(Vector3 velocity, Vector3 nextVelocity) {
        //真下のRaycastの法線方向によるX,Z軸回転の適用
        RaycastHit hit1;
        var t1 = raycastXStart;
        var scale1 = t1.lossyScale.z * 0.5f;
        var isHit1 = Physics.SphereCast(t1.position, scale1, t1.forward,
            out hit1, BROOM_MIN_SIDE, layerMask);
        if (isHit1) {
            //一定以下の高さの場合は距離で加速度計算
            var target = hit1.point.x;
            var raycastYStartPos = raycastXStart.position.x;
            nextVelocity.z -= target + BROOM_MIN_SIDE - raycastYStartPos - (velocity.x * Time.deltaTime);
        }
        
        RaycastHit hit2;
        var t2 = raycastMinusXStart;
        var scale2 = t2.lossyScale.z * 0.5f;
        var isHit2 = Physics.SphereCast(t2.position, scale2, t2.forward,
            out hit2, BROOM_MIN_SIDE, layerMask);
        if (isHit2) {
            //一定以下の高さの場合は距離で加速度計算
            var target = hit2.point.x;
            var raycastYStartPos = raycastMinusXStart.position.x;
            nextVelocity.z += target + BROOM_MIN_SIDE - raycastYStartPos - (velocity.x * Time.deltaTime);
        }
        
        return nextVelocity;
    }
}