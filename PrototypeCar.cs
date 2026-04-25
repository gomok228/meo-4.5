using UnityEngine;

public class PrototypeCar : MonoBehaviour
{
    [Header("Wheel Colliders")]
    public WheelCollider frontLeftWheel;
    public WheelCollider frontRightWheel;
    public WheelCollider rearLeftWheel;
    public WheelCollider rearRightWheel;

    [Header("LMP Settings")]
    public float motorTorque = 2500f; // Огромная мощность
    public float steerAngle = 22f;    // Острый, но короткий руль
    public float brakeForce = 4000f;  // Мощнейшие тормоза
    public float downforce = 150f;    // Экстремальная прижимная сила

    [Header("Stability")]
    [SerializeField] private Vector3 centerOfMassOffset = new Vector3(0, -0.3f, 0);
    [SerializeField] private float suspensionDistance = 0.05f; // Почти нет хода подвески

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
            
            // Очень жесткая пружина
            JointSpring spring = wheel.suspensionSpring;
            spring.spring = 80000f;
            spring.damper = 6000f;
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
        // Задний привод
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

        // Аэродинамика (прижимная сила от скорости)
        if (rb.linearVelocity.magnitude > 5f)
        {
            rb.AddForce(-transform.up * downforce * rb.linearVelocity.magnitude);
        }
    }
}
