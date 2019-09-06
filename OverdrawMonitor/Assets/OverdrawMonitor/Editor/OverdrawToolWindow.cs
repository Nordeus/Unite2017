using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

class MonitorInfo
{
    public Camera sourceCamera;
    public CameraOverdrawMonitor monitor;
}

// Todo: add ability to use overdraw tool on source camera
// I.e. source camera and compute camera can be the same. It need for canvases.
// Можно сделать галку возле камеры на выводе результатов, она позволит менять способ мониторинга: через отдельную камеру или заменить шейдер на текущей

public class OverdrawToolWindow : EditorWindow
{
    bool isEnabled => _monitorsRoot != null;
    Transform _monitorsRoot;
    List<MonitorInfo> _monitors;
    bool _replacing;

    [MenuItem("Tools/Overdraw Tool")]
    static void ShowWindow()
    {
        var window = GetWindow<OverdrawToolWindow>();
        window.Show();
        window.Focus();
    }

    [MenuItem("Tools/Overdraw Tool", true)]
    static bool Validate()
    {
        return Application.isPlaying;
    }

    void Init()
    {
        _monitors = new List<MonitorInfo>();

        var monitorsRootGo = new GameObject("OverdrawMonitorsRoot");
        monitorsRootGo.hideFlags = HideFlags.DontSave;
        _monitorsRoot = monitorsRootGo.transform;
    }

    void TryShutdown()
    {
        if (_monitors != null)
        {
            foreach (MonitorInfo monitorInfo in _monitors.ToArray())
                RemoveMonitor(monitorInfo);
            _monitors = null;
        }

        if (_monitorsRoot != null)
        {
            DestroyImmediate(_monitorsRoot.gameObject);
            _monitorsRoot = null;
        }

        _replacing = false;
    }

    void AddMonitorForCamera(Camera camera)
    {
        GameObject go;
        Camera computeCamera;
        if (_replacing)
        {
            go = camera.gameObject;
            computeCamera = camera;
        }
        else
        {
            go = new GameObject("CameraOverdrawMonitor");
            go.hideFlags = HideFlags.DontSave;
            go.transform.SetParent(_monitorsRoot, false);
            computeCamera = go.AddComponent<Camera>();
        }

        var monitor = go.AddComponent<CameraOverdrawMonitor>();
        monitor.SetCameras(camera, computeCamera);

        _monitors.Add(new MonitorInfo
        {
            sourceCamera = camera,
            monitor = monitor,
        });
    }

    void RemoveMonitor(MonitorInfo monitorInfo)
    {
        CameraOverdrawMonitor monitor = monitorInfo.monitor;
        if (monitor != null)
        {
            if (IsReplacingMonitor(monitorInfo))
                DestroyImmediate(monitor);
            else
                DestroyImmediate(monitor.gameObject);
        }
        _monitors.Remove(monitorInfo);
    }

    void CheckMonitor(MonitorInfo monitorInfo)
    {
        if (monitorInfo.sourceCamera == null || !monitorInfo.sourceCamera.isActiveAndEnabled)
            RemoveMonitor(monitorInfo);
    }

    bool IsReplacingMonitor(MonitorInfo monitorInfo)
    {
        return monitorInfo.monitor.gameObject == monitorInfo.sourceCamera.gameObject;
    }

    void Update()
    {
        // Check shutdown if needed
        if (!isEnabled || !Validate())
            TryShutdown();
        if (_monitors == null)
            return;

        // Check existing monitors
        foreach (MonitorInfo monitorInfo in _monitors.ToArray())
            CheckMonitor(monitorInfo);

        // Refresh monitors
        foreach (Camera activeCamera in Camera.allCameras)
            if (!_monitors.Exists(m => m.sourceCamera == activeCamera))
                AddMonitorForCamera(activeCamera);
    }

    void OnGUI()
    {
        if (Validate())
        {
            using (new GUILayout.HorizontalScope())
            {
                if (GUILayout.Button(isEnabled ? "Stop" : "Start", GUILayout.MaxWidth(100), GUILayout.MaxHeight(25)))
                {
                    if (!isEnabled && Validate())
                        Init();
                    else
                        TryShutdown();
                }

                if (!isEnabled)
                    return;

                GUILayout.FlexibleSpace();

                if (GUILayout.Button("Reset Max", GUILayout.Width(100), GUILayout.Height(20)))
                {
                    foreach (MonitorInfo monitorInfo in _monitors)
                        if (monitorInfo.monitor != null)
                            monitorInfo.monitor.ResetStats();
                }
            }

            GUILayout.Space(5);

            bool newReplacing = GUILayout.Toggle(_replacing, "Replacing shader for cameras (for Canvas cameras)");
            if (newReplacing != _replacing)
            {
                TryShutdown();
                Init();
                _replacing = newReplacing;
                return;
            }

            GUILayout.Space(5);

            float totalAverage = 0f;
            float totalMax = 0f;
            foreach (MonitorInfo monitorInfo in _monitors)
            {
                using (new GUILayout.HorizontalScope())
                {
                    CameraOverdrawMonitor monitor = monitorInfo.monitor;

                    GUILayout.Label(monitorInfo.sourceCamera.name);
                    GUILayout.FlexibleSpace();

                    float accumulatedAverageOverdraw = monitor.isActiveAndEnabled ? monitor.AccumulatedAverageOverdraw : 0f;
                    GUILayout.Label(FormatResult(accumulatedAverageOverdraw, monitor.MaxOverdraw));

                    totalMax += monitor.MaxOverdraw;
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

