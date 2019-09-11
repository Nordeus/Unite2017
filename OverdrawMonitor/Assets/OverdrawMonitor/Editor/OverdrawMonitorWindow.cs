using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

public class OverdrawMonitorWindow : EditorWindow
{
    bool isEnabled => _monitorsGo != null && Application.isPlaying;
    GameObject _monitorsGo;
    Dictionary<CameraOverdrawMonitor, CameraOverdrawStats> _globalMaxInfos;

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
        _globalMaxInfos = new Dictionary<CameraOverdrawMonitor, CameraOverdrawStats>();
    }

    void TryShutdown()
    {
        if (_monitorsGo == null)
            return;

        DestroyImmediate(_monitorsGo);
        _globalMaxInfos = null;
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

            foreach (CameraOverdrawMonitor monitor in _globalMaxInfos.Keys.ToArray())
                if (!Array.Exists(monitors, m => monitor))
                    _globalMaxInfos.Remove(monitor);

            long gameViewArea = gameViewResolution.x * gameViewResolution.y;
            float totalGlobalFillRate = 0f;
            foreach (CameraOverdrawMonitor monitor in monitors)
            {
                using (new GUILayout.HorizontalScope())
                {
                    Camera cam = monitor.targetCamera;
                    GUILayout.Label($"{cam.name} {cam.pixelWidth}x{cam.pixelHeight}");

                    GUILayout.FlexibleSpace();

                    float localFillRate = monitor.fillRate;
                    float globalFillRate = monitor.fragmentsCount / (float)gameViewArea;
                    totalGlobalFillRate += globalFillRate;

                    if (!_globalMaxInfos.TryGetValue(monitor, out CameraOverdrawStats globalMaxInfo))
                    {
                        globalMaxInfo = new CameraOverdrawStats();
                        _globalMaxInfos.Add(monitor, globalMaxInfo);
                    }
                    globalMaxInfo.maxLocalFillRate = Math.Max(localFillRate, globalMaxInfo.maxLocalFillRate);
                    globalMaxInfo.maxGlobalFillRate = Math.Max(globalFillRate, globalMaxInfo.maxGlobalFillRate);

                    GUILayout.Label(FormatResult("Local: {0} / {1} \t Global: {2} / {3}",
                        localFillRate, globalMaxInfo.maxLocalFillRate, globalFillRate, globalMaxInfo.maxGlobalFillRate));
                }
            }

            GUILayout.Space(5);

            float maxTotalGlobalFillRate = 0f;
            foreach (CameraOverdrawStats stat in _globalMaxInfos.Values)
                maxTotalGlobalFillRate += stat.maxGlobalFillRate;
            using (new GUILayout.HorizontalScope())
            {
                GUILayout.Label("TOTAL");
                GUILayout.FlexibleSpace();
                GUILayout.Label(FormatResult("Global: {0} / {1}", totalGlobalFillRate, maxTotalGlobalFillRate));
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
        _globalMaxInfos.Clear();
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
    public float maxLocalFillRate;
    public float maxGlobalFillRate;
}
