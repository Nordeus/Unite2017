using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

public class OverdrawMonitorWindow : EditorWindow
{
    bool isEnabled => _monitorsGo != null && Application.isPlaying;
    GameObject _monitorsGo;
    Dictionary<CameraOverdrawMonitor, CameraOverdrawStats> _stats;

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
        _stats = new Dictionary<CameraOverdrawMonitor, CameraOverdrawStats>();
    }

    void TryShutdown()
    {
        if (_monitorsGo == null)
            return;

        DestroyImmediate(_monitorsGo);
        _stats = null;
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

                if (GUILayout.Button("Reset Stats", GUILayout.Width(100), GUILayout.Height(20)))
                    ResetStats();
            }

            GUILayout.Space(5);

            Vector2Int gameViewResolution = GetGameViewResolution();
            GUILayout.Label($"Screen {gameViewResolution.x}x{gameViewResolution.y}");

            GUILayout.Space(5);

            foreach (CameraOverdrawMonitor monitor in _stats.Keys.ToArray())
                if (!Array.Exists(monitors, m => monitor))
                    _stats.Remove(monitor);

            long gameViewArea = gameViewResolution.x * gameViewResolution.y;
            float totalGlobalOverdrawRatio = 0f;
            foreach (CameraOverdrawMonitor monitor in monitors)
            {
                using (new GUILayout.HorizontalScope())
                {
                    Camera cam = monitor.targetCamera;
                    GUILayout.Label($"{cam.name} {cam.pixelWidth}x{cam.pixelHeight}");

                    GUILayout.FlexibleSpace();

                    float localOverdrawRatio = monitor.overdrawRatio;
                    float globalOverdrawRatio = monitor.fragmentsCount / (float)gameViewArea;
                    totalGlobalOverdrawRatio += globalOverdrawRatio;

                    if (!_stats.TryGetValue(monitor, out CameraOverdrawStats monitorStats))
                    {
                        monitorStats = new CameraOverdrawStats();
                        _stats.Add(monitor, monitorStats);
                    }
                    monitorStats.maxLocalOverdrawRatio = Math.Max(localOverdrawRatio, monitorStats.maxLocalOverdrawRatio);
                    monitorStats.maxGlobalOverdrawRatio = Math.Max(globalOverdrawRatio, monitorStats.maxGlobalOverdrawRatio);

                    GUILayout.Label(FormatResult("Local: {0} / {1} \t Global: {2} / {3}",
                        localOverdrawRatio, monitorStats.maxLocalOverdrawRatio, globalOverdrawRatio, monitorStats.maxGlobalOverdrawRatio));
                }
            }

            GUILayout.Space(5);

            float maxTotalGlobalOverdrawRatio = 0f;
            foreach (CameraOverdrawStats stat in _stats.Values)
                maxTotalGlobalOverdrawRatio += stat.maxGlobalOverdrawRatio;
            using (new GUILayout.HorizontalScope())
            {
                GUILayout.Label("TOTAL");
                GUILayout.FlexibleSpace();
                GUILayout.Label(FormatResult("Global: {0} / {1}", totalGlobalOverdrawRatio, maxTotalGlobalOverdrawRatio));
            }
        }
        else
        {
            GUILayout.Label("Available only in Play mode");
        }

        Repaint();
    }

    void ResetStats()
    {
        _stats.Clear();
    }

    string FormatResult(string format, params float[] args)
    {
        var stringArgs = new List<string>();
        foreach (float arg in args)
            stringArgs.Add($"{arg:N3}");
        return string.Format(format, stringArgs.ToArray());
    }

    static Vector2Int GetGameViewResolution()
    {
        var resString = UnityStats.screenRes.Split('x');
        return new Vector2Int(int.Parse(resString[0]), int.Parse(resString[1]));
    }
}

class CameraOverdrawStats
{
    public float maxLocalOverdrawRatio;
    public float maxGlobalOverdrawRatio;
}
