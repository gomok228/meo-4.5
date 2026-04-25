using UnityEngine;

public class RallyCar : MonoBehaviour
{
    [Header("Wheel Colliders")]
    public WheelCollider frontLeftWheel;
    public WheelCollider frontRightWheel;
    public WheelCollider rearLeftWheel;
    public WheelCollider rearRightWheel;

    [Header("Rally Settings")]
    public float motorTorque = 1200f; // Мощность делится на 4 колеса
    public float steerAngle = 35f;    // Большой выворот руля для шпилек
    public float brakeForce = 2000f;
    public float handbrakeForce = 4000f; // Усилие для срыва в занос

    [Header("Stability")]
    [SerializeField] private Vector3 centerOfMassOffset = new Vector3(0, -0.2f, 0);
    [SerializeField] private float suspensionDistance = 0.35f; // Длинный ход для трамплинов

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
            
            // Мягкая подвеска для неровностей
            JointSpring spring = wheel.suspensionSpring;
            spring.spring = 25000f;
            spring.damper = 2500f;
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
        // ПОЛНЫЙ ПРИВОД (AWD)
        frontLeftWheel.motorTorque = moveInput * motorTorque;
        frontRightWheel.motorTorque = moveInput * motorTorque;
        rearLeftWheel.motorTorque = moveInput * motorTorque;
        rearRightWheel.motorTorque = moveInput * motorTorque;

        frontLeftWheel.steerAngle = steerInput * steerAngle;
        frontRightWheel.steerAngle = steerInput * steerAngle;

        // Ручник блокирует только заднюю ось для заноса
        bool isHandbrake = Input.GetKey(KeyCode.Space);
        
        if (isHandbrake)
        {
            rearLeftWheel.brakeTorque = handbrakeForce;
            rearRightWheel.brakeTorque = handbrakeForce;
            frontLeftWheel.brakeTorque = 0f;
            frontRightWheel.brakeTorque = 0f;
        }
        else
        {
            // Обычное торможение двигателем/тормозом (упрощенно)
            rearLeftWheel.brakeTorque = 0f;
            rearRightWheel.brakeTorque = 0f;
            frontLeftWheel.brakeTorque = 0f;
            frontRightWheel.brakeTorque = 0f;

            if (moveInput < 0 && rb.linearVelocity.magnitude > 1f)
            {
                frontLeftWheel.brakeTorque = brakeForce;
                frontRightWheel.brakeTorque = brakeForce;
                rearLeftWheel.brakeTorque = brakeForce;
                rearRightWheel.brakeTorque = brakeForce;
            }
        }
    }
}
