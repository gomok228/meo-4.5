using UnityEngine;

public class F1Car : MonoBehaviour
{
    [Header("Wheel Colliders")]
    public WheelCollider frontLeftWheel;
    public WheelCollider frontRightWheel;
    public WheelCollider rearLeftWheel;
    public WheelCollider rearRightWheel;

    [Header("F1 Settings")]
    public float motorTorque = 2800f; // Бешеное ускорение
    public float steerAngle = 25f;    // Максимально острый руль для шикан
    public float brakeForce = 5000f;  // Карбоновые тормоза (останавливается как об стену)
    public float downforce = 200f;    // Монструозный "держак" в поворотах

    [Header("DRS System (Arcade Boost)")]
    public float drsTorqueBoost = 1000f; // Добавка к скорости на прямых
    public float drsDownforceReduction = 150f; // Снижаем прижим для скорости

    [Header("Stability")]
    [SerializeField] private Vector3 centerOfMassOffset = new Vector3(0, -0.2f, 0);
    [SerializeField] private float suspensionDistance = 0.03f; // Подвески "нет"

    private float moveInput;
    private float steerInput;
    private bool isDRSActive;
    private Rigidbody rb;

    void Start()
    {
        rb = GetComponent<Rigidbody>();
        if (rb != null) rb.centerOfMass = centerOfMassOffset;

        WheelCollider[] allWheels = GetComponentsInChildren<WheelCollider>();
        foreach (WheelCollider wheel in allWheels)
        {
            wheel.suspensionDistance = suspensionDistance;
            
            // Самая жесткая подвеска из всех
            JointSpring spring = wheel.suspensionSpring;
            spring.spring = 100000f;
            spring.damper = 12000f;
            wheel.suspensionSpring = spring;

            wheel.enabled = false; wheel.enabled = true;
        }
    }

    void Update()
    {
        moveInput = Input.GetAxis("Vertical");
        steerInput = Input.GetAxis("Horizontal");
        
        // Аркадная фишка: DRS на левый Shift
        isDRSActive = Input.GetKey(KeyCode.LeftShift);
    }

    void FixedUpdate()
    {
        // Если активирован DRS, даем буст мощности
        float currentTorque = isDRSActive ? motorTorque + drsTorqueBoost : motorTorque;

        rearLeftWheel.motorTorque = moveInput * currentTorque;
        rearRightWheel.motorTorque = moveInput * currentTorque;

        frontLeftWheel.steerAngle = steerInput * steerAngle;
        frontRightWheel.steerAngle = steerInput * steerAngle;

        bool isBraking = Input.GetKey(KeyCode.Space);
        float brake = isBraking ? brakeForce : 0f;
        rearLeftWheel.brakeTorque = brake;
        rearRightWheel.brakeTorque = brake;
        frontLeftWheel.brakeTorque = brake;
        frontRightWheel.brakeTorque = brake;

        // Аэродинамика F1 (с учетом DRS)
        if (rb.linearVelocity.magnitude > 5f)
        {
            float currentDownforce = isDRSActive ? (downforce - drsDownforceReduction) : downforce;
            // Убеждаемся, что прижимная сила не стала отрицательной
            currentDownforce = Mathf.Max(currentDownforce, 10f); 
            
            rb.AddForce(-transform.up * currentDownforce * rb.linearVelocity.magnitude);
        }
    }
}
