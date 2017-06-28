using System;
using UnityEngine;

/// <summary> This is a singleton component that is responsible for measuring overdraw information
/// on the main camera. You shouldn't add this compoenent manually, but use the Instance getter to
/// access it.
/// 
/// The measurements process is done in two passes. First a new camera is created that will render
/// the scene into a texture with high precission, the texture is called overdrawTexture. This texture
/// contains the information how many times a pixel has been overdrawn. After this step a compute shader
/// is used to add up all the pixels in the overdrawTexture and stores the information into this component.
/// 
/// We say this tool measures exactly the amount of overdraw, but it does so only in certain cases. In other
/// cases the error margin is very small. This is because of the nature of the compute shaders. Compute
/// shaders should operate in batches in order for them to be efficient. In our case the compute shader 
/// batch that sums up the results of the first render pass has a size of 32x32. This means that if the
/// pixel size of the camera is not divisible by 32, then the edge pixels that don't fit in range won't
/// be processed. But since we usually have huge render targets (in comparison to 32x32 pixel blocks) and
/// the error comes from the part of the image that is not important, this is acceptable. 
/// </summary>
[ExecuteInEditMode]
public class OverdrawMonitor : MonoBehaviour
{
	private static OverdrawMonitor instance;
	public static OverdrawMonitor Instance
	{
		get
		{
			if (instance == null)
			{
				instance = GameObject.FindObjectOfType<OverdrawMonitor>();
				if (instance == null)
				{
					var go = new GameObject("OverdrawMonitor");
					instance = go.AddComponent<OverdrawMonitor>();
				}
			}

			return instance;
		}
	}

	private new Camera camera;
	private RenderTexture overdrawTexture;

	private ComputeShader computeShader;

	private const int dataSize = 128 * 128;
	private int[] inputData = new int[dataSize];
	private int[] resultData = new int[dataSize];
	private ComputeBuffer resultBuffer;
	private Shader replacementShader;

	// ========= Results ========
	// Last measurement
	/// <summary> The number of shaded fragments in the last frame. </summary>
	public long TotalShadedFragments { get; private set; }
	/// <summary> The overdraw ration in the last frame. </summary>
	public float OverdrawRatio { get; private set; }

	// Sampled measurement
	/// <summary> Number of shaded fragments in the measured time span. </summary>
	public long IntervalShadedFragments { get; private set; }
	/// <summary> The average number of shaded fragments in the measured time span. </summary>
	public float IntervalAverageShadedFragments { get; private set; }
	/// <summary> The average overdraw in the measured time span. </summary>
	public float IntervalAverageOverdraw { get; private set; }
	public float AccumulatedAverageOverdraw { get { return accumulatedIntervalOverdraw / intervalFrames; } }

	// Extreems
	/// <summary> The maximum overdraw measured. </summary>
	public float MaxOverdraw { get; private set; }

	private long accumulatedIntervalFragments;
	private float accumulatedIntervalOverdraw;
	private long intervalFrames;

	private float intervalTime = 0;
	public float SampleTime = 1;

	/// <summary> An empty method that can be used to initialize the singleton. </summary>
	public void Touch() { }

	#region Measurement magic

	public void Awake()
	{
#if UNITY_EDITOR
		// Since this emulation always turns on by default if on mobile platform. With the emulation
		// turned on the tool won't work.
		UnityEditor.EditorApplication.ExecuteMenuItem("Edit/Graphics Emulation/No Emulation");
		SubscribeToPlayStateChanged();
#endif
		
		if (Application.isPlaying) DontDestroyOnLoad(gameObject);
		gameObject.hideFlags = HideFlags.DontSave | HideFlags.HideInInspector;

		// Prepare the camera that is going to render the scene with the initial overdraw data.
		replacementShader = Shader.Find("Debug/OverdrawInt");

		camera = GetComponent<Camera>();
		if (camera == null) camera = gameObject.AddComponent<Camera>();
		camera.CopyFrom(Camera.main);
		camera.SetReplacementShader(replacementShader, null);

		RecreateTexture(Camera.main);
		RecreateComputeBuffer();

		computeShader = Resources.Load<ComputeShader>("OverdrawParallelReduction");

		for (int i = 0; i < inputData.Length; i++) inputData[i] = 0;
	}

#if UNITY_EDITOR
	public void SubscribeToPlayStateChanged()
	{
		UnityEditor.EditorApplication.playmodeStateChanged -= OnPlayStateChanged;
		UnityEditor.EditorApplication.playmodeStateChanged += OnPlayStateChanged;
	}

	private static void OnPlayStateChanged()
	{
		if (!UnityEditor.EditorApplication.isPlayingOrWillChangePlaymode && UnityEditor.EditorApplication.isPlaying)
		{
			if (instance != null) instance.OnDisable();
		}
	}
#endif

	private bool disabled = true;

	public void OnEnable()
	{
		disabled = false;
	}

	public void OnDisable()
	{
		disabled = true;
		OnDestroy();
	}

	public void LateUpdate()
	{
		if (disabled) return;

		Camera main = Camera.main;
		camera.CopyFrom(main);
		camera.clearFlags = CameraClearFlags.SolidColor;
		camera.backgroundColor = Color.black;
		camera.targetTexture = overdrawTexture;
		camera.SetReplacementShader(replacementShader, null);

		transform.position = main.transform.position;
		transform.rotation = main.transform.rotation;

		RecreateTexture(main);

		intervalTime += Time.deltaTime;
		if (intervalTime > SampleTime)
		{
			IntervalShadedFragments = accumulatedIntervalFragments;
			IntervalAverageShadedFragments = (float)accumulatedIntervalFragments / intervalFrames;
			IntervalAverageOverdraw = (float)accumulatedIntervalOverdraw / intervalFrames;

			intervalTime -= SampleTime;

			accumulatedIntervalFragments = 0;
			accumulatedIntervalOverdraw = 0;
			intervalFrames = 0;
		}
	}

	/// <summary> Checks if the overdraw texture should be updated. This needs to happen if the main camera
	/// configuration changes. </summary>
	private void RecreateTexture(Camera main)
	{
		if (overdrawTexture == null)
		{
			overdrawTexture = new RenderTexture(camera.pixelWidth, camera.pixelHeight, 24, RenderTextureFormat.RFloat);
			overdrawTexture.enableRandomWrite = true;
			camera.targetTexture = overdrawTexture;
		}

		if (main.pixelWidth != overdrawTexture.width || main.pixelHeight != overdrawTexture.height)
		{
			overdrawTexture.Release();
			overdrawTexture.width = main.pixelWidth;
			overdrawTexture.height = main.pixelHeight;
		}
	}

	private void RecreateComputeBuffer()
	{
		if (resultBuffer != null) return;
		resultBuffer = new ComputeBuffer(resultData.Length, 4);
	}

	public void OnDestroy()
	{
		if (camera != null)
		{
			camera.targetTexture = null;
		}
		if (resultBuffer != null) resultBuffer.Release();
	}

	public void OnPostRender()
	{
		if (disabled) return;

		int kernel = computeShader.FindKernel("CSMain");

		RecreateComputeBuffer();

		// Setting up the data
		resultBuffer.SetData(inputData);
		computeShader.SetTexture(kernel, "Overdraw", overdrawTexture);
		computeShader.SetBuffer(kernel, "Output", resultBuffer);

		int xGroups = (overdrawTexture.width / 32);
		int yGroups = (overdrawTexture.height / 32);

		// Summing up the fragments
		computeShader.Dispatch(kernel, xGroups, yGroups, 1);
		resultBuffer.GetData(resultData);

		// Getting the results
		TotalShadedFragments = 0;
		for (int i = 0; i < resultData.Length; i++)
		{
			TotalShadedFragments += resultData[i];
		}

		OverdrawRatio = (float)TotalShadedFragments / (xGroups * 32 * yGroups * 32);

		accumulatedIntervalFragments += TotalShadedFragments;
		accumulatedIntervalOverdraw += OverdrawRatio;
		intervalFrames++;

		if (OverdrawRatio > MaxOverdraw) MaxOverdraw = OverdrawRatio;
	}

	#endregion
	#region Measurement control methods

	public void StartMeasurement()
	{
		enabled = true;
		camera.enabled = true;
	}
	
	public void Stop()
	{
		enabled = false;
		camera.enabled = false;
	}

	public void SetSampleTime(float time)
	{
		SampleTime = time;
	}

	public void ResetSampling()
	{
		accumulatedIntervalOverdraw = 0;
		accumulatedIntervalFragments = 0;
		intervalTime = 0;
		intervalFrames = 0;
	}

	public void ResetExtreemes()
	{
		MaxOverdraw = 0;
	}

	#endregion
}