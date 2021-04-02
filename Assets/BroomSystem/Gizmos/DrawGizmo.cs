#if UNITY_EDITOR
using System;
using UnityEditor;
using UnityEngine;

/// <summary>
/// ギズモちゃん
/// </summary>
public class DrawGizmo : MonoBehaviour {
    [SerializeField]
    private enum Mode {
        Sphere,
        WireSphere
    }

    [SerializeField, Header("描画モード選択")] private Mode mMode = Mode.WireSphere;
    [SerializeField, Header("ギズモの色")] private Color mGizmoColor = new Color(1f, 0, 0, 0.3f);
    [SerializeField, Header("衝突対象")] private LayerMask layerMask;
    [SerializeField, Header("最大距離")] private float maxDistance = 4.0f;

    void OnDrawGizmos() {
        Gizmos.color = mGizmoColor;

        var scale = transform.lossyScale.x * 0.5f;
        bool isHit;

        isHit = Physics.SphereCast(transform.position, scale, transform.forward,
            out var hit, maxDistance, layerMask);
        if (isHit) {
            Gizmos.DrawRay(transform.position, transform.forward * hit.distance);
            switch (mMode) {
                case Mode.Sphere:
                    Gizmos.DrawSphere(transform.position + transform.forward * hit.distance, scale);
                    break;
                case Mode.WireSphere:
                    Gizmos.DrawWireSphere(transform.position + transform.forward * hit.distance, scale);
                    break;
                default:
                    throw new ArgumentOutOfRangeException();
            }
        } else {
            Gizmos.DrawRay(transform.position, transform.forward * maxDistance);
        }
    }

    [CustomEditor(typeof(DrawGizmo))]
    public class CustomWindow : Editor {
        public override void OnInspectorGUI() {
            EditorGUILayout.BeginVertical(GUI.skin.box);
            {
                EditorGUILayout.HelpBox("ギズモでEditサポート", MessageType.Info);
            }
            EditorGUILayout.EndVertical();

            base.OnInspectorGUI();
        }
    }
}
#endif