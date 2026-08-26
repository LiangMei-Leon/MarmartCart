using UnityEngine;

/// <summary>
/// Experimental REAL physical trailing hinge.
///
/// Real leader RearHitch
///         ↓
/// Kinematic proxy Rigidbody
///         ↓
/// HingeJoint
///         ↓
/// Dynamic rigid arm
///         ↓
/// Probe point at rear end
///
/// The actual leading-cart Rigidbody is NOT connected to this,
/// so the experiment cannot affect the real cart physics.
/// </summary>
public class PhysicalChainJointProbe : MonoBehaviour
{
    [Header("Runtime Source")]
    [SerializeField]
    private Transform leaderRearHitch;

    [Header("Physical Arm")]

    [Tooltip("Distance from leader rear hitch to trailing probe.")]
    [Min(0.1f)]
    [SerializeField]
    private float armLength = 1.5f;

    [Min(0.01f)]
    [SerializeField]
    private float armMass = 1f;

    [Tooltip(
        "Translation damping. Keep this fairly low or the arm will feel dead.")]
    [Min(0f)]
    [SerializeField]
    private float linearDamping = 0.2f;

    [Tooltip(
        "Controls how quickly left/right swinging settles.")]
    [Min(0f)]
    [SerializeField]
    private float angularDamping = 1f;

    [Header("Optional Swing Limits")]

    [SerializeField]
    private bool limitSwingAngle = false;

    [Range(0f, 180f)]
    [SerializeField]
    private float maxSwingAngle = 100f;

    [Header("Debug")]

    [SerializeField]
    private bool showPhysicalArm = true;

    [SerializeField]
    private float probeSphereSize = 0.25f;

    [Header("Runtime - Read Only")]

    [SerializeField]
    private float currentHingeAngle;

    private GameObject proxyObject;
    private Rigidbody proxyBody;

    private GameObject armObject;
    private Rigidbody armBody;

    private HingeJoint hingeJoint;

    private Transform probePoint;

    public Vector3 ProbePosition =>
        probePoint != null
            ? probePoint.position
            : Vector3.zero;

    public Transform ProbeTransform =>
        probePoint;

    public void Initialize(Transform rearHitch)
    {
        if (rearHitch == null)
        {
            Debug.LogError(
                "[PhysicalChainJointProbe] RearHitch is null.",
                this
            );

            return;
        }

        leaderRearHitch = rearHitch;

        CreateSimulation();
    }

    private void CreateSimulation()
    {
        CleanupSimulation();

        if (leaderRearHitch == null)
            return;

        // =================================================
        // 1. ISOLATED KINEMATIC COPY OF THE REAL HITCH
        // =================================================

        proxyObject =
            new GameObject(
                "PhysicalJoint_HitchProxy"
            );

        proxyObject.transform.SetPositionAndRotation(
            leaderRearHitch.position,
            leaderRearHitch.rotation
        );

        proxyBody =
            proxyObject.AddComponent<Rigidbody>();

        proxyBody.isKinematic = true;
        proxyBody.useGravity = false;
        proxyBody.detectCollisions = false;

        // =================================================
        // 2. DYNAMIC RIGID ARM
        //
        // Local +Z points toward the leader.
        // Local -Z is the trailing direction.
        // =================================================

        armObject =
            GameObject.CreatePrimitive(
                PrimitiveType.Cube
            );

        armObject.name =
            "PhysicalJoint_TrailingArm";

        Vector3 forward =
            Vector3.ProjectOnPlane(
                leaderRearHitch.forward,
                Vector3.up
            );

        if (forward.sqrMagnitude < 0.0001f)
            forward = Vector3.forward;

        forward.Normalize();

        Quaternion startingRotation =
            Quaternion.LookRotation(
                forward,
                Vector3.up
            );

        // Center of arm sits half its length behind hitch.
        Vector3 startingPosition =
            leaderRearHitch.position -
            forward * (armLength * 0.5f);

        armObject.transform.SetPositionAndRotation(
            startingPosition,
            startingRotation
        );

        // Thin visual rod.
        armObject.transform.localScale =
            new Vector3(
                0.08f,
                0.08f,
                armLength
            );

        MeshRenderer renderer =
            armObject.GetComponent<MeshRenderer>();

        if (renderer != null)
            renderer.enabled = showPhysicalArm;

        Collider armCollider =
            armObject.GetComponent<Collider>();

        if (armCollider != null)
            Destroy(armCollider);

        armBody =
            armObject.AddComponent<Rigidbody>();

        armBody.mass =
            armMass;

        armBody.useGravity =
            false;

        armBody.detectCollisions =
            false;

        armBody.linearDamping =
            linearDamping;

        armBody.angularDamping =
            angularDamping;

        // =================================================
        // 3. REAL HINGE
        // =================================================

        hingeJoint =
            armObject.AddComponent<HingeJoint>();

        hingeJoint.connectedBody =
            proxyBody;

        hingeJoint.autoConfigureConnectedAnchor =
            false;

        // Hinge is at FRONT END of our rod.
        //
        // Cube length is along local Z.
        hingeJoint.anchor =
            new Vector3(
                0f,
                0f,
                0.5f
            );

        // Connected to center of proxy sitting at RearHitch.
        hingeJoint.connectedAnchor =
            Vector3.zero;

        // Rotate around vertical axis:
        //
        //       left ↔ right
        //
        // from top-down view.
        hingeJoint.axis =
            Vector3.up;

        hingeJoint.useMotor = false;
        hingeJoint.useSpring = false;
        hingeJoint.enableCollision = false;

        if (limitSwingAngle)
        {
            JointLimits limits =
                hingeJoint.limits;

            limits.min =
                -maxSwingAngle;

            limits.max =
                maxSwingAngle;

            limits.bounciness = 0f;

            hingeJoint.limits =
                limits;

            hingeJoint.useLimits =
                true;
        }
        else
        {
            hingeJoint.useLimits =
                false;
        }

        // =================================================
        // 4. VISIBLE PROBE ON REAR OF ARM
        // =================================================

        GameObject probeSphere =
            GameObject.CreatePrimitive(
                PrimitiveType.Sphere
            );

        probeSphere.name =
            "PhysicalJoint_Probe";

        Collider probeCollider =
            probeSphere.GetComponent<Collider>();

        if (probeCollider != null)
            Destroy(probeCollider);

        probeSphere.transform.SetParent(
            armObject.transform,
            false
        );

        // Because parent cube has Z scale = armLength,
        // local -0.5 corresponds to rear end.
        probeSphere.transform.localPosition =
            new Vector3(
                0f,
                0f,
                -0.5f
            );

        // Compensate for parent's non-uniform scale.
        probeSphere.transform.localScale =
            new Vector3(
                probeSphereSize / 0.08f,
                probeSphereSize / 0.08f,
                probeSphereSize / armLength
            );

        probePoint =
            probeSphere.transform;
    }

    private void FixedUpdate()
    {
        if (leaderRearHitch == null ||
            proxyBody == null)
        {
            return;
        }

        // Only copy the actual cart's rear hitch.
        //
        // Real cart receives NO forces from this experiment.
        proxyBody.MovePosition(
            leaderRearHitch.position
        );

        proxyBody.MoveRotation(
            leaderRearHitch.rotation
        );

        if (hingeJoint != null)
        {
            currentHingeAngle =
                hingeJoint.angle;
        }
    }

    private void CleanupSimulation()
    {
        if (armObject != null)
            Destroy(armObject);

        if (proxyObject != null)
            Destroy(proxyObject);

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
}