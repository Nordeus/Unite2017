using System;
using UnityEngine;

// This is a singleton component that is responsible for measuring overdraw information
// on the main camera. You shouldn't add this component manually, but use the Instance getter to
// access it.
//
// The measurements process is done in two passes. First a new camera is created that will render
// the scene into a texture with high precision, the texture is called overdrawTexture. This texture
// contains the information how many times a pixel has been overdrawn. After this step a compute shader
// is used to add up all the pixels in the overdrawTexture and stores the information into this component.
//
// We say this tool measures exactly the amount of overdraw, but it does so only in certain cases. In other
// cases the error margin is very small. This is because of the nature of the compute shaders. Compute
// shaders should operate in batches in order for them to be efficient. In our case the compute shader
// batch that sums up the results of the first render pass has a size of 32x32. This means that if the
// pixel size of the camera is not divisible by 32, then the edge pixels that don't fit in range won't
// be processed. But since we usually have huge render targets (in comparison to 32x32 pixel blocks) and
// the error comes from the part of the image that is not important, this is acceptable.

public class OverdrawMonitor : MonoBehaviour
{
    const int GroupDimension = 32;
    const int DataDimension = 128;
    const int DataSize = DataDimension * DataDimension;

    public static OverdrawMonitor Instance
    {
        get
        {
            if (instance == null)
            {
                instance = FindObjectOfType<OverdrawMonitor>();
                if (instance == null)
                {
                    var go = new GameObject("OverdrawMonitor");
                    DontDestroyOnLoad(go);
                    go.hideFlags = HideFlags.DontSave;
                    go.AddComponent<OverdrawMonitor>();
                }
            }
            return instance;
        }
    }

    // Last measurement
    // The number of shaded fragments in the last frame
    public long TotalShadedFragments { get; private set; }

    // The overdraw ration in the last frame
    public float OverdrawRatio { get; private set; }

    // Sampled measurement
    // Number of shaded fragments in the measured time span
    public long IntervalShadedFragments { get; private set; }

    // The average number of shaded fragments in the measured time span
    public float IntervalAverageShadedFragments { get; private set; }

    // The average overdraw in the measured time span
    public float IntervalAverageOverdraw { get; private set; }
    public float AccumulatedAverageOverdraw => accumulatedIntervalOverdraw / intervalFrames;

    // Extremes
    // The maximum overdraw measured
    public float MaxOverdraw { get; private set; }

    static OverdrawMonitor instance;
    Camera _camera;
    RenderTexture overdrawTexture;
    ComputeShader computeShader;
    int[] inputData = new int[DataSize];
    int[] resultData = new int[DataSize];
    ComputeBuffer resultBuffer;
    Shader replacementShader;
    long accumulatedIntervalFragments;
    float accumulatedIntervalOverdraw;
    long intervalFrames;
    float intervalTime = 0;
    public float SampleTime = 1;

    void Awake()
    {
        // Compute shader
        computeShader = Resources.Load<ComputeShader>("OverdrawParallelReduction");

        // Replacement shader
        replacementShader = Shader.Find("Debug/OverdrawInt");
        Shader.SetGlobalFloat("OverdrawFragmentWeight", 1f / (GroupDimension * GroupDimension));

        // Camera
        _camera = gameObject.AddComponent<Camera>();

        for (int i = 0; i < inputData.Length; i++)
            inputData[i] = 0;

        instance = this;
    }

    void LateUpdate()
    {
        Camera main = Camera.main;

        _camera.CopyFrom(main);
        _camera.clearFlags = CameraClearFlags.SolidColor;
        _camera.backgroundColor = Color.black;
        _camera.SetReplacementShader(replacementShader, null);

        RecreateTexture(main);
        _camera.targetTexture = overdrawTexture;

        transform.position = main.transform.position;
        transform.rotation = main.transform.rotation;

        intervalTime += Time.deltaTime;
        if (intervalTime > SampleTime)
        {
            IntervalShadedFragments = accumulatedIntervalFragments;
            IntervalAverageShadedFragments = (float)accumulatedIntervalFragments / intervalFrames;
            IntervalAverageOverdraw = accumulatedIntervalOverdraw / intervalFrames;

            intervalTime -= SampleTime;

            accumulatedIntervalFragments = 0;
            accumulatedIntervalOverdraw = 0;
            intervalFrames = 0;
        }
    }

    // Checks if the overdraw texture should be updated. This needs to happen if the main camera configuration changes.
    void RecreateTexture(Camera main)
    {
        if (overdrawTexture == null || main.pixelWidth != overdrawTexture.width || main.pixelHeight != overdrawTexture.height)
        {
            ReleaseTexture();
            overdrawTexture = new RenderTexture(main.pixelWidth, main.pixelHeight, 24, RenderTextureFormat.RFloat);
        }
    }

    void ReleaseTexture()
    {
        if (_camera != null)
            _camera.targetTexture = null;

        if (overdrawTexture == null)
            return;

        overdrawTexture.Release();
        overdrawTexture = null;
    }

    void RecreateComputeBuffer()
    {
        if (resultBuffer == null)
            resultBuffer = new ComputeBuffer(resultData.Length, 4);
    }

    void OnDestroy()
    {
        ReleaseTexture();

        resultBuffer?.Release();

        instance = null;
    }

    void OnPostRender()
    {
        if (overdrawTexture == null)
            return;

        int kernel = computeShader.FindKernel("CSMain");

        RecreateComputeBuffer();

        // Setting up the data
        resultBuffer.SetData(inputData);
        computeShader.SetInt("BufferSizeX", DataDimension);
        computeShader.SetTexture(kernel, "Overdraw", overdrawTexture);
        computeShader.SetBuffer(kernel, "Output", resultBuffer);

        int xGroups = overdrawTexture.width / GroupDimension;
        int yGroups = overdrawTexture.height / GroupDimension;

        // Summing up the fragments
        computeShader.Dispatch(kernel, xGroups, yGroups, 1);
        resultBuffer.GetData(resultData);

        // Getting the results
        TotalShadedFragments = 0;
        for (int i = 0; i < resultData.Length; i++)
            TotalShadedFragments += resultData[i];

        OverdrawRatio = (float)TotalShadedFragments / (xGroups * GroupDimension * yGroups * GroupDimension);

        accumulatedIntervalFragments += TotalShadedFragments;
        accumulatedIntervalOverdraw += OverdrawRatio;
        intervalFrames++;

        if (OverdrawRatio > MaxOverdraw) MaxOverdraw = OverdrawRatio;
    }

    //
    // Measurement control methods
    //

    public void StartMeasurement()
    {
        enabled = true;
        GetComponent<Camera>().enabled = true;
    }

    public void Stop()
    {
        enabled = false;
        GetComponent<Camera>().enabled = false;
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

    public void ResetExtremes()
    {
        MaxOverdraw = 0;
    }
}
