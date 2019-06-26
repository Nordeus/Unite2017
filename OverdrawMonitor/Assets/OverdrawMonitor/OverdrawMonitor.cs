using System.Collections.Generic;
using UnityEngine;

[DisallowMultipleComponent]
public class OverdrawMonitor : MonoBehaviour
{
    public static OverdrawMonitor instance
    {
        get
        {
            if (_instance == null)
            {
                var go = new GameObject("OverdrawMonitor");
                DontDestroyOnLoad(go);
                go.hideFlags = HideFlags.DontSave;
                _instance = go.AddComponent<OverdrawMonitor>();
            }
            return _instance;
        }
    }
    public int camerasCount => _cameraOverdrawMonitors.Count;

    static OverdrawMonitor _instance;
    List<CameraOverdrawMonitor> _cameraOverdrawMonitors;

    void Awake()
    {
        _cameraOverdrawMonitors = new List<CameraOverdrawMonitor>();
    }

    void Update()
    {
        foreach (Camera activeCamera in Camera.allCameras)
        {
            if (!_cameraOverdrawMonitors.Exists(m => m.sourceCamera == activeCamera))
            {
                var go = new GameObject("CameraOverdrawMonitor");
                go.transform.SetParent(activeCamera.transform, false);
                var cameraOverdrawMonitor = go.AddComponent<CameraOverdrawMonitor>();
                cameraOverdrawMonitor.SetSourceCamera(activeCamera);
                _cameraOverdrawMonitors.Add(cameraOverdrawMonitor);
            }
        }
    }

    public void ResetStats()
    {
        foreach (CameraOverdrawMonitor cameraOverdrawMonitor in _cameraOverdrawMonitors)
            cameraOverdrawMonitor.ResetStats();
    }

    public float GetMaxOverdraw(int index)
    {
        CameraOverdrawMonitor cameraOverdrawMonitor = GetActiveMonitor(index);
        return cameraOverdrawMonitor != null ? cameraOverdrawMonitor.MaxOverdraw : 0f;
    }

    public float GetAccumulatedAverageOverdraw(int index)
    {
        CameraOverdrawMonitor cameraOverdrawMonitor = GetActiveMonitor(index);
        return cameraOverdrawMonitor != null ? cameraOverdrawMonitor.AccumulatedAverageOverdraw : 0f;
    }

    CameraOverdrawMonitor GetActiveMonitor(int index)
    {
        CameraOverdrawMonitor cameraOverdrawMonitor = _cameraOverdrawMonitors[index];
        return cameraOverdrawMonitor != null && cameraOverdrawMonitor.isActiveAndEnabled ? cameraOverdrawMonitor : null;
    }
}
