using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

[System.Serializable, VolumeComponentMenu("SSL")]
public class SSLVolume : VolumeComponent, IPostProcessComponent
{
    public ClampedIntParameter   maxSteps     = new ClampedIntParameter(32, 1, 256);
    public ClampedFloatParameter maxDistance  = new ClampedFloatParameter(10f, 0.1f, 100f);
    public ClampedFloatParameter intensity    = new ClampedFloatParameter(1f, 0f, 5f);
    public ClampedFloatParameter jitterScale  = new ClampedFloatParameter(0.5f, 0f, 1f);
    public ClampedFloatParameter blurScale    = new ClampedFloatParameter(0.5f, 0f, 1f);
    public ClampedIntParameter   blurLevels   = new ClampedIntParameter(1, 0, 8);
    public ClampedIntParameter   blurIterations = new ClampedIntParameter(1, 0, 8);
    public BoolParameter         enabled      = new BoolParameter(true);

    public bool IsActive() => enabled.value && intensity.value > 0f;
    public bool IsTileCompatible() => false;
}