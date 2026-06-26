using UnityEngine;

/// <summary>
/// GPU instance simulation plugin contract.
/// Each simulation type (rain, boids, particles, etc.) implements this interface
/// as a MonoBehaviour on the same GameObject as UniversalInstanceManager.
/// </summary>
public interface IUniversalInstanceSimulator
{
    /// <summary>Create compute buffers and bind them to the simulation's compute shader.</summary>
    void Initialize(int instanceCount);

    /// <summary>Set per-frame shader parameters and dispatch all simulation kernels.</summary>
    void Dispatch(float deltaTime);

    /// <summary>Bind simulation data buffers to the instance rendering material.</summary>
    void BindMaterial(Material material);

    /// <summary>Release all compute buffers owned by this simulation.</summary>
    void Release();

    /// <summary>
    /// Optional append/consume buffer used for per-frame instance culling.
    /// Return null to render all instances without culling.
    /// If non-null, UniversalInstanceManager calls ComputeBuffer.CopyCount()
    /// from this buffer to the indirect args buffer before draw.
    /// </summary>
    ComputeBuffer VisibleCountBuffer { get; }
}
