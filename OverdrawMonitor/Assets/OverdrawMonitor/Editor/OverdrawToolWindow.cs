using UnityEditor;
using UnityEngine;

public class OverdrawToolWindow : EditorWindow
{
    [MenuItem("Tools/Overdraw Tool")]
    static void Show()
    {
        var window = GetWindow<OverdrawToolWindow>();
        window.Focus();
    }

    [MenuItem("Tools/Overdraw Tool", true)]
    static bool Validate()
    {
        return Application.isPlaying;
    }

    void OnGUI()
    {
        if (Validate())
        {
            using (new GUILayout.HorizontalScope())
            {
                if (GUILayout.Button("Reset stats"))
                    OverdrawMonitor.instance.ResetStats();
            }

            for (int i = 0; i < OverdrawMonitor.instance.camerasCount; i++)
            {
                CameraOverdrawMonitor cameraOverdrawMonitor = OverdrawMonitor.instance.GetCameraOverdrawMonitor(i);
                if (cameraOverdrawMonitor == null)
                    continue;

                using (new GUILayout.HorizontalScope())
                {
                    GUILayout.Label("Max\n" + cameraOverdrawMonitor.MaxOverdraw.ToString("0.000"));
                    GUILayout.FlexibleSpace();
                    GUILayout.Label("Average\n" + (cameraOverdrawMonitor.isActiveAndEnabled ? cameraOverdrawMonitor.AccumulatedAverageOverdraw : 0f).ToString("0.000"));
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
