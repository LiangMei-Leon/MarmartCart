using UnityEngine;

public class DistanceJoint3D : MonoBehaviour
{
    [SerializeField] Rigidbody connectedRigidbody;
    [SerializeField] bool determineDistanceOnStart = true;
    [SerializeField] float determinedDistance;
    [SerializeField] float damper = 5f;

    protected Rigidbody _rigidbody;

    void Awake()
    {
        _rigidbody = GetComponent<Rigidbody>(); 
    }
    void Start()
    {
        if(determineDistanceOnStart)
        {
            determinedDistance = Vector3.Distance(_rigidbody.position, connectedRigidbody.position);
        }
    }

    // Update is called once per frame
    void FixedUpdate()
    {
        var connectionVec = _rigidbody.position - connectedRigidbody.position;
        var distanceDiscrepency = determinedDistance - connectionVec.magnitude;
        _rigidbody.position += distanceDiscrepency * connectionVec.normalized;

        var velocityTarget = connectionVec + _rigidbody.linearVelocity;
        var projectOnConnection = Vector3.Project(velocityTarget, connectionVec);
        _rigidbody.linearVelocity = (velocityTarget - projectOnConnection) / (1 + damper * Time.fixedDeltaTime);

        // Rotation alignment based on connection direction
        if (connectionVec != Vector3.zero) // Check to avoid NaN rotation
        {
            Quaternion targetRotation = Quaternion.LookRotation(connectionVec.normalized) * Quaternion.Euler(0, 180, 0);
            _rigidbody.MoveRotation(targetRotation);
        }
    }
}
