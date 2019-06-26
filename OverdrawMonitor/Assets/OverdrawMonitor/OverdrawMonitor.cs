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

        _cameraOverdrawMonitors.RemoveAll(m => m == null);
    }

    public void ResetStats()
    {
        foreach (CameraOverdrawMonitor cameraOverdrawMonitor in _cameraOverdrawMonitors)
            cameraOverdrawMonitor.ResetStats();
    }

    public CameraOverdrawMonitor GetCameraOverdrawMonitor(int index)
    {
        return _cameraOverdrawMonitors[index];
    }
}
