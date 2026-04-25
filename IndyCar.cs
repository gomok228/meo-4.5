using UnityEngine;

public class IndyCar : MonoBehaviour
{
    [Header("Wheel Colliders")]
    public WheelCollider frontLeftWheel;
    public WheelCollider frontRightWheel;
    public WheelCollider rearLeftWheel;
    public WheelCollider rearRightWheel;

    [Header("IndyCar Settings")]
    public float motorTorque = 2200f; // Мощность чуть меньше, но машина легкая
    public float steerAngle = 20f;    // Резкий руль
    public float brakeForce = 3500f;  // Отличные тормоза
    public float downforce = 90f;     // Средняя прижимная сила (настроено на макс. скорость)

    [Header("Stability")]
    [SerializeField] private Vector3 centerOfMassOffset = new Vector3(0, -0.25f, 0);
    [SerializeField] private float suspensionDistance = 0.04f; // Очень короткий ход подвески

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
            spring.spring = 70000f;
            spring.damper = 8000f;
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

        if (rb.linearVelocity.magnitude > 5f)
        {
            rb.AddForce(-transform.up * downforce * rb.linearVelocity.magnitude);
        }
    }
}
