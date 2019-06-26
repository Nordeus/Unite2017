using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

public class OverdrawToolWindow : EditorWindow
{
    List<CameraOverdrawMonitor> _cameraOverdrawMonitors;

    [MenuItem("Tools/Overdraw Tool")]
    static void Show()
    {
        var window = GetWindow<OverdrawToolWindow>();
        window._cameraOverdrawMonitors = new List<CameraOverdrawMonitor>();

        window.Focus();
    }

    [MenuItem("Tools/Overdraw Tool", true)]
    static bool Validate()
    {
        return Application.isPlaying;
    }

    void Update()
    {
        if (!Validate())
            return;

        _cameraOverdrawMonitors.RemoveAll(m => m == null);

        foreach (Camera activeCamera in Camera.allCameras)
        {
            if (!_cameraOverdrawMonitors.Exists(m => m.sourceCamera == activeCamera))
            {
                var cameraOverdrawMonitor = activeCamera.GetComponentInChildren<CameraOverdrawMonitor>(true);
                if (cameraOverdrawMonitor == null)
                {
                    var go = new GameObject("CameraOverdrawMonitor");
                    go.hideFlags = HideFlags.DontSave;
                    go.transform.SetParent(activeCamera.transform, false);
                    cameraOverdrawMonitor = go.AddComponent<CameraOverdrawMonitor>();
                }
                cameraOverdrawMonitor.SetSourceCamera(activeCamera);
                _cameraOverdrawMonitors.Add(cameraOverdrawMonitor);
            }
        }
    }

    void OnGUI()
    {
        if (Validate())
        {
            using (new GUILayout.HorizontalScope())
            {
                if (GUILayout.Button("Reset stats"))
                {
                    foreach (CameraOverdrawMonitor cameraOverdrawMonitor in _cameraOverdrawMonitors)
                        cameraOverdrawMonitor.ResetStats();
                }
            }

            for (int i = 0; i < _cameraOverdrawMonitors.Count; i++)
            {
                CameraOverdrawMonitor cameraOverdrawMonitor = _cameraOverdrawMonitors[i];
                if (cameraOverdrawMonitor == null)
                    continue;

                using (new GUILayout.HorizontalScope())
                {
                    GUILayout.Label("Max\n" + cameraOverdrawMonitor.MaxOverdraw.ToString("0.000"));
                    GUILayout.FlexibleSpace();
                    float accumulatedAverageOverdraw = cameraOverdrawMonitor.isActiveAndEnabled ?
                        cameraOverdrawMonitor.AccumulatedAverageOverdraw : 0f;
                    GUILayout.Label("Average\n" + accumulatedAverageOverdraw.ToString("0.000"));
                }
            }
        }
        else
        {
            GUILayout.Label("Available only in Play mode");
        }

        Repaint();
    }
}
