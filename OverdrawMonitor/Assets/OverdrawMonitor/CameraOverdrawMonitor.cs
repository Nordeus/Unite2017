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

using UnityEngine;

[DisallowMultipleComponent]
public class CameraOverdrawMonitor : MonoBehaviour
{
    const int GroupDimension = 32;
    const int DataDimension = 128;
    const int DataSize = DataDimension * DataDimension;
    const float SampleTime = 1f;

    // The number of shaded fragments in the last frame
    public long totalShadedFragments { get; private set; }

    // The overdraw ration in the last frame
    public float overdrawRatio { get; private set; }

    // Number of shaded fragments in the measured time span
    public long IntervalShadedFragments { get; private set; }

    // The average number of shaded fragments in the measured time span
    public float IntervalAverageShadedFragments { get; private set; }

    // The average overdraw in the measured time span
    public float IntervalAverageOverdraw { get; private set; }
    public float AccumulatedAverageOverdraw => _accumulatedIntervalOverdraw / _intervalFrames;

    // The maximum overdraw measured
    public float MaxOverdraw { get; private set; }

    Camera _computeCamera;
    RenderTexture _overdrawTexture;
    ComputeShader _computeShader;
    ComputeBuffer _resultBuffer;
    Shader _replacementShader;
    int[] _inputData;
    int[] _resultData;
    long _accumulatedIntervalFragments;
    float _accumulatedIntervalOverdraw;
    float _intervalTime;
    long _intervalFrames;

    void Awake()
    {
        // Compute shader
        _computeShader = Resources.Load<ComputeShader>("OverdrawParallelReduction");

        // Replacement shader
        _replacementShader = Shader.Find("Debug/OverdrawInt");
        Shader.SetGlobalFloat("OverdrawFragmentWeight", 1f / (GroupDimension * GroupDimension));

        _inputData = new int[DataSize];
        _resultData = new int[DataSize];
        for (int i = 0; i < _inputData.Length; i++)
            _inputData[i] = 0;
    }

    void OnDestroy()
    {
        ReleaseTexture();
        _resultBuffer?.Release();
    }

    public void SetComputeCamera(Camera computeCamera)
    {
        _computeCamera = computeCamera;
    }

    public void ResetStats()
    {
        _accumulatedIntervalOverdraw = 0;
        _accumulatedIntervalFragments = 0;
        _intervalTime = 0;
        _intervalFrames = 0;
        MaxOverdraw = 0;
    }

    void RecreateTexture(Camera main)
    {
        if (_overdrawTexture == null || main.pixelWidth != _overdrawTexture.width || main.pixelHeight != _overdrawTexture.height)
        {
            ReleaseTexture();
            _overdrawTexture = new RenderTexture(main.pixelWidth, main.pixelHeight, 24, RenderTextureFormat.RFloat);
        }
    }

    // Todo: refactor ReleaseTexture
    void ReleaseTexture()
    {
        if (_overdrawTexture != null)
        {
            _overdrawTexture.Release();
            _overdrawTexture = null;
        }
    }

    void RecreateComputeBuffer()
    {
        if (_resultBuffer == null)
            _resultBuffer = new ComputeBuffer(_resultData.Length, 4);
    }

    void LateUpdate()
    {
        if (_computeCamera == null)
            return;

        CameraClearFlags originalClearFlags = _computeCamera.clearFlags;
        Color originalClearColor = _computeCamera.backgroundColor;
        RenderTexture originalTargetTexture = _computeCamera.targetTexture;
        bool originalIsCameraEnabled = _computeCamera.enabled;

        _computeCamera.clearFlags = CameraClearFlags.SolidColor;
        _computeCamera.backgroundColor = Color.clear;

        RecreateTexture(_computeCamera);
        _computeCamera.targetTexture = _overdrawTexture;

        _intervalTime += Time.deltaTime;
        if (_intervalTime > SampleTime)
        {
            IntervalShadedFragments = _accumulatedIntervalFragments;
            IntervalAverageShadedFragments = (float)_accumulatedIntervalFragments / _intervalFrames;
            IntervalAverageOverdraw = _accumulatedIntervalOverdraw / _intervalFrames;

            _intervalTime -= SampleTime;

            _accumulatedIntervalFragments = 0;
            _accumulatedIntervalOverdraw = 0;
            _intervalFrames = 0;
        }

        _computeCamera.enabled = false;

        _computeCamera.RenderWithShader(_replacementShader, null);

        int kernel = _computeShader.FindKernel("CSMain");

        RecreateComputeBuffer();

        // Setting up the data
        _resultBuffer.SetData(_inputData);
        _computeShader.SetInt("BufferSizeX", DataDimension);
        _computeShader.SetTexture(kernel, "Overdraw", _overdrawTexture);
        _computeShader.SetBuffer(kernel, "Output", _resultBuffer);

        int xGroups = _overdrawTexture.width / GroupDimension;
        int yGroups = _overdrawTexture.height / GroupDimension;

        // Summing up the fragments
        _computeShader.Dispatch(kernel, xGroups, yGroups, 1);
        _resultBuffer.GetData(_resultData);

        // Getting the results
        totalShadedFragments = 0;
        foreach (int res in _resultData)
            totalShadedFragments += res;

        overdrawRatio = (float)totalShadedFragments / (xGroups * GroupDimension * yGroups * GroupDimension);

        _accumulatedIntervalFragments += totalShadedFragments;
        _accumulatedIntervalOverdraw += overdrawRatio;
        _intervalFrames++;

        if (overdrawRatio > MaxOverdraw)
            MaxOverdraw = overdrawRatio;

        _computeCamera.targetTexture = originalTargetTexture;
        _computeCamera.clearFlags = originalClearFlags;
        _computeCamera.backgroundColor = originalClearColor;
        _computeCamera.enabled = originalIsCameraEnabled;
    }
}
