using UnityEditor;
using UnityEngine;

/// <summary> A simple tool to track the exact amount of overdraw in the game window. This tool
/// only shows the result, <see cref="OverdrawMonitor"/> to check out how this is implemented. </summary>
public class OverdrawToolWindow : EditorWindow
{
	[MenuItem("Tools/Overdraw Tool")]
	public static void ShowWindow()
	{
		var window = GetWindow<OverdrawToolWindow>();
		window.Focus();
	}

	public void OnGUI()
	{
		using (new GUILayout.HorizontalScope())
		{
			if (GUILayout.Button("Start"))
			{
				OverdrawMonitor.Instance.StartMeasurement();
				OverdrawMonitor.Instance.ResetSampling();
				OverdrawMonitor.Instance.ResetExtreemes();
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

		Repaint();
	}
}