using UnityEngine;

[RequireComponent(typeof(Rigidbody))]
public class PIDBoneFollower : MonoBehaviour
{
    [Header("Target Bone")]
    public Transform target;

    [Header("Position PID Parameters")]
    public float posKp = 100f;
    public float posKi = 0f;
    public float posKd = 10f;
    [Range(0f, 1f)] public float posLowPassFactor = 0.9f;

    [Header("Rotation PID Parameters")]
    public float rotKp = 100f;
    public float rotKi = 0f;
    public float rotKd = 10f;
    [Range(0f, 1f)] public float rotLowPassFactor = 0.9f;

    [Header("Info")]
    [SerializeField] private string boneName;

    private Vector3 posIntegral;
    private Vector3 posLastError;
    private Vector3 posFilteredOutput;

    private Vector3 rotIntegral;
    private Vector3 rotLastError;
    private Vector3 rotFilteredOutput;

    private Rigidbody rb;

    public string BoneName 
    { 
        get => boneName; 
        set => boneName = value; 
    }

    void Awake()
    {
        rb = GetComponent<Rigidbody>();
        if (string.IsNullOrEmpty(boneName))
            boneName = gameObject.name;
    }

    void FixedUpdate()
    {
        if (target == null) return;
        float deltaTime = Time.fixedDeltaTime;

        // ---------------------
        // 位置 PID 控制 + 滤波
        // ---------------------
        Vector3 posError = target.position - transform.position;
        posIntegral += posError * deltaTime;
        Vector3 posDerivative = (posError - posLastError) / deltaTime;
        posLastError = posError;

        Vector3 posPIDOutput = posKp * posError + posKi * posIntegral + posKd * posDerivative;
        posFilteredOutput = Vector3.Lerp(posFilteredOutput, posPIDOutput, 1f - posLowPassFactor);
        rb.AddForce(posFilteredOutput, ForceMode.Acceleration);

        // ---------------------
        // 旋转 PID 控制 + 滤波
        // ---------------------
        Quaternion rotDelta = target.rotation * Quaternion.Inverse(transform.rotation);
        rotDelta.ToAngleAxis(out float angle, out Vector3 axis);
        if (angle > 180f) angle -= 360f;
        if (float.IsNaN(axis.x)) axis = Vector3.zero;

        Vector3 rotError = axis.normalized * angle * Mathf.Deg2Rad;
        rotIntegral += rotError * deltaTime;
        Vector3 rotDerivative = (rotError - rotLastError) / deltaTime;
        rotLastError = rotError;

        Vector3 rotPIDOutput = rotKp * rotError + rotKi * rotIntegral + rotKd * rotDerivative;
        rotFilteredOutput = Vector3.Lerp(rotFilteredOutput, rotPIDOutput, 1f - rotLowPassFactor);
        rb.AddTorque(rotFilteredOutput, ForceMode.Acceleration);
    }

    public void UpdatePIDParameters(float posKp, float posKi, float posKd, float posFilter,
                                   float rotKp, float rotKi, float rotKd, float rotFilter)
    {
        this.posKp = posKp;
        this.posKi = posKi;
        this.posKd = posKd;
        this.posLowPassFactor = posFilter;
        
        this.rotKp = rotKp;
        this.rotKi = rotKi;
        this.rotKd = rotKd;
        this.rotLowPassFactor = rotFilter;
    }
}
