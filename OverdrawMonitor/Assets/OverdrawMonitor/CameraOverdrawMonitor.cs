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

public class CameraOverdrawMonitor : MonoBehaviour
{
    const int GroupDimension = 32;
    const int DataDimension = 128;
    const int DataSize = DataDimension * DataDimension;
    const float SampleTime = 1f;

    public Camera targetCamera => _targetCamera;

    // The number of shaded fragments in the last frame
    public long totalShadedFragments { get; private set; }

    // The overdraw ratio in the last frame
    public float overdrawRatio { get; private set; }

    // Number of shaded fragments in the measured time span
    public long intervalShadedFragments { get; private set; }

    // The average number of shaded fragments in the measured time span
    public float intervalAverageShadedFragments { get; private set; }

    // The average overdraw in the measured time span
    public float intervalAverageOverdraw { get; private set; }
    public float accumulatedAverageOverdraw => _accumulatedIntervalOverdraw / _intervalFrames;

    // The maximum overdraw measured
    public float maxOverdraw { get; private set; }

    Camera _targetCamera;
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
        for (int i = 0; i < _inputData.Length; i++)
            _inputData[i] = 0;

        _resultData = new int[DataSize];
        _resultBuffer = new ComputeBuffer(_resultData.Length, 4);
    }

    void OnDestroy()
    {
        ReleaseTexture();
        _resultBuffer?.Release();
    }

    public void SetTargetCamera(Camera targetCamera)
    {
        _targetCamera = targetCamera;
    }

    public void ResetStats()
    {
        _accumulatedIntervalOverdraw = 0;
        _accumulatedIntervalFragments = 0;
        _intervalTime = 0;
        _intervalFrames = 0;
        maxOverdraw = 0;
    }

    void ReleaseTexture()
    {
        if (_overdrawTexture != null)
        {
            _overdrawTexture.Release();
            _overdrawTexture = null;
        }
    }

    void LateUpdate()
    {
        if (_targetCamera == null)
            return;

        CameraClearFlags originalClearFlags = _targetCamera.clearFlags;
        Color originalClearColor = _targetCamera.backgroundColor;
        RenderTexture originalTargetTexture = _targetCamera.targetTexture;
        bool originalIsCameraEnabled = _targetCamera.enabled;

        _targetCamera.clearFlags = CameraClearFlags.SolidColor;
        _targetCamera.backgroundColor = Color.clear;

        // Recreate texture if needed
        if (_overdrawTexture == null || _targetCamera.pixelWidth != _overdrawTexture.width || _targetCamera.pixelHeight != _overdrawTexture.height)
        {
            ReleaseTexture();
            _overdrawTexture = new RenderTexture(_targetCamera.pixelWidth, _targetCamera.pixelHeight, 24, RenderTextureFormat.RFloat);
        }
        _targetCamera.targetTexture = _overdrawTexture;

        _intervalTime += Time.deltaTime;
        if (_intervalTime > SampleTime)
        {
            intervalShadedFragments = _accumulatedIntervalFragments;
            intervalAverageShadedFragments = (float)_accumulatedIntervalFragments / _intervalFrames;
            intervalAverageOverdraw = _accumulatedIntervalOverdraw / _intervalFrames;

            _intervalTime -= SampleTime;

            _accumulatedIntervalFragments = 0;
            _accumulatedIntervalOverdraw = 0;
            _intervalFrames = 0;
        }

        _targetCamera.enabled = false;

        _targetCamera.RenderWithShader(_replacementShader, null);

        int kernel = _computeShader.FindKernel("CSMain");

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

        if (overdrawRatio > maxOverdraw)
            maxOverdraw = overdrawRatio;

        _targetCamera.targetTexture = originalTargetTexture;
        _targetCamera.clearFlags = originalClearFlags;
        _targetCamera.backgroundColor = originalClearColor;
        _targetCamera.enabled = originalIsCameraEnabled;
    }
}
