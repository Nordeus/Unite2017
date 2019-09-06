using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

public class OverdrawToolWindow : EditorWindow
{
    bool isEnabled => _monitors != null && Application.isPlaying;
    List<CameraOverdrawMonitor> _monitors;

    [MenuItem("Tools/Overdraw Tool")]
    static void ShowWindow()
    {
        var window = GetWindow<OverdrawToolWindow>();
        window.Show();
        window.Focus();
    }

    // Todo: fix auto start!!!
    void Init()
    {
        _monitors = new List<CameraOverdrawMonitor>();
    }

    void TryShutdown()
    {
        if (_monitors == null)
            return;

        foreach (CameraOverdrawMonitor monitor in _monitors.ToArray())
            RemoveMonitor(monitor);
        _monitors = null;
    }

    void AddMonitorForCamera(Camera camera)
    {
        // Todo: create separate object in root object for monitor components - no need to use source camera objects

        var monitor = camera.gameObject.AddComponent<CameraOverdrawMonitor>();
        monitor.SetComputeCamera(camera);
        _monitors.Add(monitor);
    }

    void RemoveMonitor(CameraOverdrawMonitor monitor)
    {
        if (monitor != null)
            DestroyImmediate(monitor);
        _monitors.Remove(monitor);
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

        // Remove expired monitors
        _monitors.RemoveAll(m => m == null);
        foreach (CameraOverdrawMonitor monitor in _monitors.ToArray())
            if (!Array.Exists(activeCameras, c => monitor.GetComponent<Camera>() == c))
                RemoveMonitor(monitor);

        // Refresh monitors
        foreach (Camera activeCamera in activeCameras)
            if (!_monitors.Exists(m => m.GetComponent<Camera>() == activeCamera))
                AddMonitorForCamera(activeCamera);
    }

    void OnGUI()
    {
        if (Application.isPlaying)
        {
            using (new GUILayout.HorizontalScope())
            {
                if (GUILayout.Button(isEnabled ? "Stop" : "Start", GUILayout.MaxWidth(100), GUILayout.MaxHeight(25)))
                {
                    if (!isEnabled)
                        Init();
                    else
                        TryShutdown();
                }

                if (!isEnabled)
                    return;

                GUILayout.FlexibleSpace();

                if (GUILayout.Button("Reset Max", GUILayout.Width(100), GUILayout.Height(20)))
                {
                    foreach (CameraOverdrawMonitor monitor in _monitors)
                        if (monitor != null)
                            monitor.ResetStats();
                }
            }

            GUILayout.Space(5);

            float totalAverage = 0f;
            float totalMax = 0f;

            if (_monitors != null)
            {
                foreach (CameraOverdrawMonitor monitor in _monitors)
                {
                    using (new GUILayout.HorizontalScope())
                    {
                        GUILayout.Label(monitor.gameObject.name);
                        GUILayout.FlexibleSpace();

                        float accumulatedAverageOverdraw = monitor.isActiveAndEnabled ? monitor.AccumulatedAverageOverdraw : 0f;
                        GUILayout.Label(FormatResult(accumulatedAverageOverdraw, monitor.MaxOverdraw));

                        totalMax += monitor.MaxOverdraw;
                        totalAverage += accumulatedAverageOverdraw;
                    }
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

