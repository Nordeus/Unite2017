using System;
using UnityEditor;
using UnityEngine;

public class OverdrawMonitorWindow : EditorWindow
{
    bool isEnabled => _monitorsGo != null && Application.isPlaying;
    GameObject _monitorsGo;

    [MenuItem("Tools/Overdraw Monitor")]
    static void ShowWindow()
    {
        GetWindow<OverdrawMonitorWindow>().Show();
    }

    void Init()
    {
        if (_monitorsGo != null)
            throw new Exception("Attempt to start overdraw monitor twice");

        _monitorsGo = new GameObject("OverdrawMonitor");
        _monitorsGo.hideFlags = HideFlags.HideAndDontSave;
    }

    void TryShutdown()
    {
        if (_monitorsGo != null)
            DestroyImmediate(_monitorsGo);
    }

    void Update()
    {
        // Check shutdown if needed
        if (!isEnabled)
        {
            TryShutdown();
            return;
        }

        Camera[] activeCameras = Camera.allCameras;

        // Remove monitors for non-active cameras
        var monitors = GetAllMonitors();
        foreach (var monitor in monitors)
            if (!Array.Exists(activeCameras, c => monitor.targetCamera == c))
                DestroyImmediate(monitor);

        // Add new monitors
        monitors = GetAllMonitors();
        foreach (Camera activeCamera in activeCameras)
        {
            if (!Array.Exists(monitors,m => m.targetCamera == activeCamera))
            {
                var monitor = _monitorsGo.AddComponent<CameraOverdrawMonitor>();
                monitor.SetTargetCamera(activeCamera);
            }
        }
    }

    CameraOverdrawMonitor[] GetAllMonitors()
    {
        return _monitorsGo.GetComponentsInChildren<CameraOverdrawMonitor>(true);
    }

    void OnGUI()
    {
        if (Application.isPlaying)
        {
            int startButtonHeight = 25;
            if (GUILayout.Button(isEnabled ? "Stop" : "Start", GUILayout.MaxWidth(100), GUILayout.MaxHeight(startButtonHeight)))
            {
                if (!isEnabled)
                    Init();
                else
                    TryShutdown();
            }

            if (!isEnabled)
                return;

            CameraOverdrawMonitor[] monitors = GetAllMonitors();

            GUILayout.Space(-startButtonHeight);
            using (new GUILayout.HorizontalScope())
            {
                GUILayout.FlexibleSpace();

                if (GUILayout.Button("Reset Max", GUILayout.Width(100), GUILayout.Height(20)))
                {
                    foreach (CameraOverdrawMonitor monitor in monitors)
                        monitor.ResetStats();
                }
            }

            GUILayout.Space(5);

            float totalAverage = 0f;
            float totalMax = 0f;
            foreach (CameraOverdrawMonitor monitor in monitors)
            {
                using (new GUILayout.HorizontalScope())
                {
                    GUILayout.Label(monitor.targetCamera.name);
                    GUILayout.FlexibleSpace();

                    float accumulatedAverageOverdraw = monitor.isActiveAndEnabled ? monitor.accumulatedAverageOverdraw : 0f;
                    GUILayout.Label(FormatResult(accumulatedAverageOverdraw, monitor.maxOverdraw));

                    totalMax += monitor.maxOverdraw;
                    totalAverage += accumulatedAverageOverdraw;
                }
            }

            GUILayout.Space(5);

            using (new GUILayout.HorizontalScope())
            {
                GUILayout.Label("TOTAL");
                GUILayout.FlexibleSpace();
                GUILayout.Label(FormatResult(totalAverage, totalMax));
            }
        }
        else
        {
            GUILayout.Label("Available only in Play mode");
        }

        Repaint();
    }

    string FormatResult(float average, float max)
    {
        return $"{average:N3}\tMax: {max:N3}";
    }
}
