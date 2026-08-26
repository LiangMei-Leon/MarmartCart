using UnityEngine;

/// <summary>
/// Experimental physical trailing hinge used as the path source for chained carts.
///
/// Real RearHitch
///      ↓
/// Kinematic proxy Rigidbody
///      ↓
/// Free HingeJoint
///      ↓
/// Dynamic trailing arm
///      ↓
/// Probe point
///
/// The actual leading-cart Rigidbody is never physically connected to this simulation,
/// so this probe cannot affect the actual cart movement.
///
/// During turns the hinge is free to swing naturally.
/// When the cart starts travelling mostly straight again, an optional spring quickly
/// recenters the hinge behind the cart.
/// </summary>
public class PhysicalChainJointProbe : MonoBehaviour
{
    #region Source

    [Header("Runtime Source")]
    [Tooltip("Assigned at runtime by SnakeCartManager.")]
    [SerializeField] private Transform leaderRearHitch;

    #endregion

    #region Physical Arm

    [Header("Physical Arm")]

    [Tooltip("Distance from the RearHitch to the trailing probe point.")]
    [Min(0.1f)]
    [SerializeField] private float armLength = 1.5f;

    [Tooltip("Mass of the simulated trailing arm.")]
    [Min(0.01f)]
    [SerializeField] private float armMass = 1f;

    [Tooltip("Translation damping. Keep fairly low so the arm remains responsive.")]
    [Min(0f)]
    [SerializeField] private float linearDamping = 0.2f;

    [Tooltip("Natural angular damping. Higher values make free swinging settle faster, but do NOT pull the arm back to center.")]
    [Min(0f)]
    [SerializeField] private float angularDamping = 1f;

    [Tooltip("Maximum angular velocity allowed for the simulated arm.")]
    [Min(1f)]
    [SerializeField] private float maxArmAngularVelocity = 25f;

    #endregion

    #region Swing Limits

    [Header("Optional Swing Limits")]

    [Tooltip("If enabled, prevents the arm from jackknifing beyond Max Swing Angle.")]
    [SerializeField] private bool limitSwingAngle = false;

    [Range(0f, 180f)]
    [SerializeField] private float maxSwingAngle = 120f;

    #endregion

    #region Auto Recenter

    [Header("Auto Recenter")]

    [Tooltip("When enabled, the hinge is actively pulled back behind the cart once the cart is moving mostly straight again.")]
    [SerializeField] private bool enableAutoRecenter = true;

    [Tooltip("RearHitch must be moving at least this fast before recentering can activate.")]
    [Min(0f)]
    [SerializeField] private float minSpeedForRecenter = 2f;

    [Tooltip("If the cart is rotating slower than this many degrees per second, it is treated as travelling mostly straight.")]
    [Min(0f)]
    [SerializeField] private float maxYawRateForRecenter = 20f;

    [Tooltip("If the hinge is already within this angle of center, the recenter spring switches off.")]
    [Min(0f)]
    [SerializeField] private float recenterDeadAngle = 1.5f;

    [Tooltip("Strength pulling the hinge back to 0 degrees.")]
    [Min(0f)]
    [SerializeField] private float recenterSpring = 250f;

    [Tooltip("Damping applied while the hinge is being pulled back to center.")]
    [Min(0f)]
    [SerializeField] private float recenterDamper = 25f;

    #endregion

    #region Debug Visual

    [Header("Debug Visual")]

    [Tooltip("Shows the simulated rigid arm as a thin cube.")]
    [SerializeField] private bool showPhysicalArm = true;

    [Tooltip("Shows the probe sphere at the end of the arm.")]
    [SerializeField] private bool showProbeSphere = true;

    [Min(0.01f)]
    [SerializeField] private float probeSphereSize = 0.25f;

    #endregion

    #region Runtime Debug

    [Header("Runtime Debug - Read Only")]

    [SerializeField] private float currentHingeAngle;
    [SerializeField] private float currentDistanceToHitch;
    [SerializeField] private float currentProbeSpeed;

    [Header("Recenter Debug - Read Only")]

    [SerializeField] private bool isRecentering;
    [SerializeField] private float currentHitchSpeed;
    [SerializeField] private float currentHitchYawRate;

    #endregion

    #region Runtime Objects

    private GameObject proxyObject;
    private Rigidbody proxyBody;

    private GameObject armObject;
    private Rigidbody armBody;

    private HingeJoint hingeJoint;

    private Transform probePoint;

    private Vector3 previousProbePosition;
    private Vector3 previousHitchPosition;
    private Vector3 previousHitchForward;

    #endregion

    #region Public API

    public Transform ProbeTransform => probePoint;

    public Vector3 ProbePosition => probePoint != null ? probePoint.position : Vector3.zero;

    /// <summary>
    /// Arm forward points from the trailing probe toward the hitch / leading cart.
    /// This is useful for blending Cart 1 rotation with the path tangent.
    /// </summary>
    public Vector3 ProbeForward
    {
        get
        {
            if (probePoint == null) return Vector3.forward;

            Vector3 forward = Vector3.ProjectOnPlane(probePoint.forward, Vector3.up);

            if (forward.sqrMagnitude < 0.0001f) return Vector3.forward;

            return forward.normalized;
        }
    }

    #endregion

    #region Initialization

    /// <summary>
    /// Called by SnakeCartManager after the runtime leading cart has been instantiated.
    /// </summary>
    public void Initialize(Transform rearHitch)
    {
        if (rearHitch == null)
        {
            Debug.LogError("[PhysicalChainJointProbe] Cannot initialize because RearHitch is null.", this);
            return;
        }

        leaderRearHitch = rearHitch;
        CreateSimulation();
    }

    #endregion

    #region Simulation Creation

    private void CreateSimulation()
    {
        CleanupSimulation();

        if (leaderRearHitch == null) return;

        CreateKinematicProxy();
        CreateTrailingArm();
        CreateHinge();
        CreateProbeVisual();

        if (probePoint != null) previousProbePosition = probePoint.position;

        previousHitchPosition = leaderRearHitch.position;

        Vector3 initialForward = Vector3.ProjectOnPlane(leaderRearHitch.forward, Vector3.up);
        previousHitchForward = initialForward.sqrMagnitude > 0.0001f ? initialForward.normalized : Vector3.forward;

        currentHitchSpeed = 0f;
        currentHitchYawRate = 0f;
        isRecentering = false;
    }

    private void CreateKinematicProxy()
    {
        proxyObject = new GameObject("PhysicalJoint_HitchProxy");
        proxyObject.transform.SetPositionAndRotation(leaderRearHitch.position, leaderRearHitch.rotation);

        proxyBody = proxyObject.AddComponent<Rigidbody>();
        proxyBody.isKinematic = true;
        proxyBody.useGravity = false;
        proxyBody.detectCollisions = false;
        proxyBody.interpolation = RigidbodyInterpolation.Interpolate;
    }

    private void CreateTrailingArm()
    {
        armObject = GameObject.CreatePrimitive(PrimitiveType.Cube);
        armObject.name = "PhysicalJoint_TrailingArm";

        Vector3 forward = Vector3.ProjectOnPlane(leaderRearHitch.forward, Vector3.up);

        if (forward.sqrMagnitude < 0.0001f) forward = Vector3.forward;

        forward.Normalize();

        Quaternion startingRotation = Quaternion.LookRotation(forward, Vector3.up);
        Vector3 startingPosition = leaderRearHitch.position - forward * (armLength * 0.5f);

        armObject.transform.SetPositionAndRotation(startingPosition, startingRotation);
        armObject.transform.localScale = new Vector3(0.08f, 0.08f, armLength);

        MeshRenderer renderer = armObject.GetComponent<MeshRenderer>();
        if (renderer != null) renderer.enabled = showPhysicalArm;

        Collider armCollider = armObject.GetComponent<Collider>();
        if (armCollider != null) Destroy(armCollider);

        armBody = armObject.AddComponent<Rigidbody>();
        armBody.mass = armMass;
        armBody.useGravity = false;
        armBody.detectCollisions = false;
        armBody.linearDamping = linearDamping;
        armBody.angularDamping = angularDamping;
        armBody.maxAngularVelocity = maxArmAngularVelocity;
        armBody.interpolation = RigidbodyInterpolation.Interpolate;
    }

    private void CreateHinge()
    {
        hingeJoint = armObject.AddComponent<HingeJoint>();

        hingeJoint.connectedBody = proxyBody;
        hingeJoint.autoConfigureConnectedAnchor = false;

        // Front of the arm is attached to RearHitch.
        hingeJoint.anchor = new Vector3(0f, 0f, 0.5f);
        hingeJoint.connectedAnchor = Vector3.zero;

        // Vertical axis means free left/right swing in top-down view.
        hingeJoint.axis = Vector3.up;

        hingeJoint.useMotor = false;
        hingeJoint.useSpring = false;
        hingeJoint.enableCollision = false;

        if (limitSwingAngle)
        {
            JointLimits limits = hingeJoint.limits;
            limits.min = -maxSwingAngle;
            limits.max = maxSwingAngle;
            limits.bounciness = 0f;

            hingeJoint.limits = limits;
            hingeJoint.useLimits = true;
        }
        else
        {
            hingeJoint.useLimits = false;
        }
    }

    private void CreateProbeVisual()
    {
        GameObject probeSphere = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        probeSphere.name = "PhysicalJoint_Probe";

        Collider probeCollider = probeSphere.GetComponent<Collider>();
        if (probeCollider != null) Destroy(probeCollider);

        probeSphere.transform.SetParent(armObject.transform, false);
        probeSphere.transform.localPosition = new Vector3(0f, 0f, -0.5f);

        float safeArmLength = Mathf.Max(0.01f, armLength);
        probeSphere.transform.localScale = new Vector3(probeSphereSize / 0.08f, probeSphereSize / 0.08f, probeSphereSize / safeArmLength);

        MeshRenderer renderer = probeSphere.GetComponent<MeshRenderer>();
        if (renderer != null) renderer.enabled = showProbeSphere;

        probePoint = probeSphere.transform;
    }

    #endregion

    #region Physics

    private void FixedUpdate()
    {
        if (leaderRearHitch == null || proxyBody == null || hingeJoint == null) return;

        // Kinematic proxy follows the real RearHitch.
        // The actual cart still receives zero forces from this experiment.
        proxyBody.MovePosition(leaderRearHitch.position);
        proxyBody.MoveRotation(leaderRearHitch.rotation);

        currentHingeAngle = hingeJoint.angle;

        UpdateHitchMotionState();
        UpdateAutoRecenter();
        UpdateProbeDebug();
    }

    /// <summary>
    /// Measures how quickly the physical cart is translating and rotating.
    /// RearHitch should now be parented under the actual physics/Rigidbody hierarchy,
    /// not under the fake drift visual.
    /// </summary>
    private void UpdateHitchMotionState()
    {
        float dt = Mathf.Max(Time.fixedDeltaTime, 0.00001f);

        Vector3 currentPosition = leaderRearHitch.position;
        Vector3 positionDelta = Vector3.ProjectOnPlane(currentPosition - previousHitchPosition, Vector3.up);

        currentHitchSpeed = positionDelta.magnitude / dt;

        Vector3 currentForward = Vector3.ProjectOnPlane(leaderRearHitch.forward, Vector3.up);

        if (currentForward.sqrMagnitude > 0.0001f)
        {
            currentForward.Normalize();

            currentHitchYawRate = Mathf.Abs(Vector3.SignedAngle(previousHitchForward, currentForward, Vector3.up)) / dt;
            previousHitchForward = currentForward;
        }
        else
        {
            currentHitchYawRate = 0f;
        }

        previousHitchPosition = currentPosition;
    }

    /// <summary>
    /// Leaves the hinge free during meaningful turning.
    ///
    /// Once the cart is travelling mostly straight again, a temporary spring
    /// pulls the arm quickly back to 0 degrees behind the cart.
    /// </summary>
    private void UpdateAutoRecenter()
    {
        if (!enableAutoRecenter)
        {
            hingeJoint.useSpring = false;
            isRecentering = false;
            return;
        }

        bool movingEnough = currentHitchSpeed >= minSpeedForRecenter;
        bool movingMostlyStraight = currentHitchYawRate <= maxYawRateForRecenter;
        bool hingeNeedsCorrection = Mathf.Abs(currentHingeAngle) > recenterDeadAngle;

        isRecentering = movingEnough && movingMostlyStraight && hingeNeedsCorrection;

        if (!isRecentering)
        {
            hingeJoint.useSpring = false;
            return;
        }

        JointSpring spring = hingeJoint.spring;

        spring.spring = recenterSpring;
        spring.damper = recenterDamper;
        spring.targetPosition = 0f;

        hingeJoint.spring = spring;
        hingeJoint.useSpring = true;
    }

    private void UpdateProbeDebug()
    {
        if (probePoint == null) return;

        currentDistanceToHitch = Vector3.Distance(leaderRearHitch.position, probePoint.position);

        Vector3 probeDelta = Vector3.ProjectOnPlane(probePoint.position - previousProbePosition, Vector3.up);
        currentProbeSpeed = probeDelta.magnitude / Mathf.Max(Time.fixedDeltaTime, 0.00001f);

        previousProbePosition = probePoint.position;
    }

    #endregion

    #region Cleanup

    private void CleanupSimulation()
    {
        if (armObject != null) Destroy(armObject);
        if (proxyObject != null) Destroy(proxyObject);

        armObject = null;
        proxyObject = null;

        armBody = null;
        proxyBody = null;

        hingeJoint = null;
        probePoint = null;
    }

    private void OnDestroy()
    {
        CleanupSimulation();
    }

    #endregion
}