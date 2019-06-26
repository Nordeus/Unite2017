using UnityEditor;
using UnityEngine;

// A simple tool to track the exact amount of overdraw in the game window.
// This tool only shows the result, see OverdrawMonitor to check out how this is implemented.
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
                if (GUILayout.Button("Start"))
                {
                    OverdrawMonitor.Instance.StartMeasurement();
                    OverdrawMonitor.Instance.ResetSampling();
                    OverdrawMonitor.Instance.ResetExtremes();
                }

                if (GUILayout.Button("End"))
                {
                    OverdrawMonitor.Instance.Stop();
                }
            }

            using (new GUILayout.HorizontalScope())
            {
                GUILayout.Label("Max\n" + OverdrawMonitor.Instance.MaxOverdraw.ToString("0.000"));
                GUILayout.FlexibleSpace();
                GUILayout.Label("Average\n" + OverdrawMonitor.Instance.AccumulatedAverageOverdraw.ToString("0.000"));
            }
        }
        else
        {
            GUILayout.Label("Available only in Play mode");
        }

        Repaint();
    }
}
