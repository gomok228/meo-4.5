using UnityEngine;

public class GT3Car : MonoBehaviour
{
    [Header("Wheel Colliders")]
    public WheelCollider frontLeftWheel;
    public WheelCollider frontRightWheel;
    public WheelCollider rearLeftWheel;
    public WheelCollider rearRightWheel;

    [Header("GT3 Settings")]
    public float motorTorque = 1800f;
    public float steerAngle = 28f;
    public float brakeForce = 3000f;
    public float downforce = 75f; // Умеренная аэродинамика

    [Header("Stability")]
    [SerializeField] private Vector3 centerOfMassOffset = new Vector3(0, -0.15f, 0);
    [SerializeField] private float suspensionDistance = 0.12f; // Позволяет атаковать поребрики

    private float moveInput;
    private float steerInput;
    private Rigidbody rb;

    void Start()
    {
        rb = GetComponent<Rigidbody>();
        if (rb != null) rb.centerOfMass = centerOfMassOffset;

        WheelCollider[] allWheels = GetComponentsInChildren<WheelCollider>();
        foreach (WheelCollider wheel in allWheels)
        {
            wheel.suspensionDistance = suspensionDistance;
            
            JointSpring spring = wheel.suspensionSpring;
            spring.spring = 50000f;
            spring.damper = 4000f;
            wheel.suspensionSpring = spring;

            wheel.enabled = false; wheel.enabled = true;
        }
    }

    void Update()
    {
        moveInput = Input.GetAxis("Vertical");
        steerInput = Input.GetAxis("Horizontal");
    }

    void FixedUpdate()
    {
        // RWD
        rearLeftWheel.motorTorque = moveInput * motorTorque;
        rearRightWheel.motorTorque = moveInput * motorTorque;

        frontLeftWheel.steerAngle = steerInput * steerAngle;
        frontRightWheel.steerAngle = steerInput * steerAngle;

        bool isBraking = Input.GetKey(KeyCode.Space);
        float brake = isBraking ? brakeForce : 0f;
        rearLeftWheel.brakeTorque = brake;
        rearRightWheel.brakeTorque = brake;
        frontLeftWheel.brakeTorque = brake;
        frontRightWheel.brakeTorque = brake;

        // Аэродинамика
        if (rb.linearVelocity.magnitude > 10f)
        {
            rb.AddForce(-transform.up * downforce * rb.linearVelocity.magnitude);
        }
    }
}
