using UnityEngine;
using System;

[Serializable]
public class PIDSettings
{
    [Header("Position PID")]
    public float posKp = 100f;
    public float posKi = 0f;
    public float posKd = 10f;
    [Range(0f, 1f)] public float posLowPassFactor = 0.9f;

    [Header("Rotation PID")]
    public float rotKp = 100f;
    public float rotKi = 0f;
    public float rotKd = 10f;
    [Range(0f, 1f)] public float rotLowPassFactor = 0.9f;

    public PIDSettings() { }

    public PIDSettings(PIDSettings other)
    {
        posKp = other.posKp;
        posKi = other.posKi;
        posKd = other.posKd;
        posLowPassFactor = other.posLowPassFactor;
        
        rotKp = other.rotKp;
        rotKi = other.rotKi;
        rotKd = other.rotKd;
        rotLowPassFactor = other.rotLowPassFactor;
    }
}

[Serializable]
public class BonePIDConfig
{
    public string boneName;
    public PIDBoneFollower physicalBone;
    public Transform animationBone;
    public PIDSettings settings;
    public bool isExpanded = true;

    public BonePIDConfig(string name, PIDBoneFollower physical, Transform animation)
    {
        boneName = name;
        physicalBone = physical;
        animationBone = animation;
        settings = new PIDSettings();
    }
}
