using UnityEngine;
using System.Collections.Generic;

public class JellyRigSimulator : MonoBehaviour
{
    [Header("Rig Nodes")]
    [Tooltip("The central transform of the rig.")]
    public Transform centerPoint;

    [Tooltip("The 8 corner transforms of the inner jelly cube.")]
    public Transform[] innerCornerPoints = new Transform[8];

    [Tooltip("The 8 corner transforms of the outer jelly cube.")]
    public Transform[] outerCornerPoints = new Transform[8];

    [Header("Physics Surface Material")]
    [Tooltip("Optional Physics Material (Friction/Bounciness) applied to all jelly colliders. If left unassigned, it will automatically adopt the Physics Material of transform.parent.parent.")]
    public PhysicsMaterial jellyPhysicsMaterial;

    [Header("Rotation & Orientation Following")]
    [Tooltip("How strongly the jelly corners follow the rotation of the center point.")]
    public float rotationFollowForce = 250f;

    [Tooltip("Damping to prevent excessive spinning/wobble when center rotates.")]
    public float rotationFollowDamping = 15f;

    [Header("Containment Settings")]
    [Tooltip("Minimum distance padding to keep inner corners inside outer corners.")]
    public float innerContainmentPadding = 0.05f;

    [Header("Node Settings")]
    public float massPerNode = 1f;
    public float innerColliderRadius = 0.15f;
    public float outerColliderRadius = 0.2f;
    public float drag = 0.5f;

    [Header("Spring Settings - Linear")]
    [Tooltip("Spring force pulling inner corners toward the central core.")]
    public float centerToInnerSpringForce = 200f;

    [Tooltip("Spring force maintaining structural integrity within the inner cube.")]
    public float innerStructuralSpringForce = 150f;

    [Tooltip("Spring force maintaining structural integrity within the outer cube.")]
    public float outerStructuralSpringForce = 100f;

    [Tooltip("Spring force connecting the inner cube shell to the outer cube shell.")]
    public float innerToOuterSpringForce = 120f;

    [Tooltip("Damping across all inter-node linear springs to absorb kinetic energy.")]
    public float springDamper = 10f;

    [Header("Spring Settings - Rotation Constraints")]
    [Tooltip("Rotational spring stiffness constraining nodes from twisting uncontrollably.")]
    public float angularSpringForce = 100f;

    [Tooltip("Rotational spring damping to absorb angular momentum and spinning.")]
    public float angularSpringDamper = 5f;

    private Rigidbody[] innerRbs = new Rigidbody[8];
    private Rigidbody[] outerRbs = new Rigidbody[8];

    // Local offset vectors and orientations relative to the centerPoint
    private Vector3[] innerLocalOffsets = new Vector3[8];
    private Vector3[] outerLocalOffsets = new Vector3[8];
    private Quaternion[] innerLocalRotations = new Quaternion[8];
    private Quaternion[] outerLocalRotations = new Quaternion[8];

    void Start()
    {
        // 0. Ensure target parent exists and retrieve its PhysicsMaterial if needed
        Transform grandParent = transform.parent != null ? transform.parent.parent : null;

        if (centerPoint == null) 
        {
            centerPoint = grandParent;
        }

        // Fetch the physics material from transform.parent.parent if none is assigned in the Inspector
        if (jellyPhysicsMaterial == null && grandParent != null)
        {
            Collider grandParentCollider = grandParent.GetComponent<Collider>();
            if (grandParentCollider != null && grandParentCollider.sharedMaterial != null)
            {
                jellyPhysicsMaterial = grandParentCollider.sharedMaterial;
            }
        }

        if (centerPoint == null || innerCornerPoints.Length != 8 || outerCornerPoints.Length != 8)
        {
            Debug.LogError("NestedJellyRigSimulator requires 1 center transform, 8 inner corner transforms, and 8 outer corner transforms.");
            return;
        }

        // Cache local offsets and initial orientations relative to center point
        for (int i = 0; i < 8; i++)
        {
            innerLocalOffsets[i] = centerPoint.InverseTransformPoint(innerCornerPoints[i].position);
            outerLocalOffsets[i] = centerPoint.InverseTransformPoint(outerCornerPoints[i].position);

            innerLocalRotations[i] = Quaternion.Inverse(centerPoint.rotation) * innerCornerPoints[i].rotation;
            outerLocalRotations[i] = Quaternion.Inverse(centerPoint.rotation) * outerCornerPoints[i].rotation;
        }

        List<Collider> jellyColliders = new List<Collider>();

        // 1. Setup Center Node
        Rigidbody centerRb = SetupNode(centerPoint, innerColliderRadius, jellyColliders);

        // 2. Setup Inner Corner Nodes & Connect to Center
        for (int i = 0; i < 8; i++)
        {
            innerRbs[i] = SetupNode(innerCornerPoints[i], innerColliderRadius, jellyColliders, false);
            AddSpringJoint(innerRbs[i], centerRb, centerToInnerSpringForce, angularSpringForce);
        }

        // 3. Setup Outer Corner Nodes
        for (int i = 0; i < 8; i++)
        {
            outerRbs[i] = SetupNode(outerCornerPoints[i], outerColliderRadius, jellyColliders);
        }

        // 4. Structural Springs: Inner Mesh
        for (int i = 0; i < innerRbs.Length; i++)
        {
            for (int j = i + 1; j < innerRbs.Length; j++)
            {
                AddSpringJoint(innerRbs[i], innerRbs[j], innerStructuralSpringForce, angularSpringForce);
            }
        }

        // 5. Structural Springs: Outer Mesh
        for (int i = 0; i < outerRbs.Length; i++)
        {
            for (int j = i + 1; j < outerRbs.Length; j++)
            {
                AddSpringJoint(outerRbs[i], outerRbs[j], outerStructuralSpringForce, angularSpringForce);
            }
        }

        // 6. Cross-Bracing Springs: Inner to Outer
        for (int i = 0; i < innerRbs.Length; i++)
        {
            for (int j = i + 1; j < outerRbs.Length; j++)
            {
                AddSpringJoint(innerRbs[i], outerRbs[j], innerToOuterSpringForce, angularSpringForce);
            }
        }

        // 7. Disable internal collisions across ALL jelly nodes
        for (int i = 0; i < jellyColliders.Count; i++)
        {
            for (int j = i + 1; j < jellyColliders.Count; j++)
            {
                Physics.IgnoreCollision(jellyColliders[i], jellyColliders[j]);
            }
        }
    }

    void FixedUpdate()
    {
        ApplyRotationFollowForces();
        EnforceInnerContainment();
    }

    /// <summary>
    /// Applies directional spring forces and rotational restoring torques to keep corners aligned.
    /// </summary>
    private void ApplyRotationFollowForces()
    {
        if (centerPoint == null) return;

        for (int i = 0; i < 8; i++)
        {
            // Inner Corner Position & Rotation Restoring Springs
            if (innerRbs[i] != null)
            {
                Vector3 targetWorldPos = centerPoint.TransformPoint(innerLocalOffsets[i]);
                Quaternion targetWorldRot = centerPoint.rotation * innerLocalRotations[i];

                ApplyRestoringForce(innerRbs[i], targetWorldPos);
                ApplyRestoringTorque(innerRbs[i], targetWorldRot);
            }

            // Outer Corner Position & Rotation Restoring Springs
            if (outerRbs[i] != null)
            {
                Vector3 targetWorldPos = centerPoint.TransformPoint(outerLocalOffsets[i]);
                Quaternion targetWorldRot = centerPoint.rotation * outerLocalRotations[i];

                ApplyRestoringForce(outerRbs[i], targetWorldPos);
                ApplyRestoringTorque(outerRbs[i], targetWorldRot);
            }
        }
    }

    private void ApplyRestoringForce(Rigidbody rb, Vector3 targetWorldPos)
    {
        Vector3 error = targetWorldPos - rb.position;
        Vector3 force = error * rotationFollowForce - rb.linearVelocity * rotationFollowDamping;
        rb.AddForce(force, ForceMode.Force);
    }

    /// <summary>
    /// Torque-based angular spring constraint driving the node to its local target orientation.
    /// </summary>
    private void ApplyRestoringTorque(Rigidbody rb, Quaternion targetWorldRot)
    {
        Quaternion rotationError = targetWorldRot * Quaternion.Inverse(rb.rotation);

        rotationError.ToAngleAxis(out float angleInDegrees, out Vector3 rotationAxis);

        if (angleInDegrees > 180f) angleInDegrees -= 360f;

        if (Mathf.Abs(angleInDegrees) > 0.01f)
        {
            Vector3 angularTorque = rotationAxis.normalized * (angleInDegrees * Mathf.Deg2Rad * angularSpringForce);
            Vector3 dampingTorque = rb.angularVelocity * angularSpringDamper;

            rb.AddTorque(angularTorque - dampingTorque, ForceMode.Force);
        }
    }

    /// <summary>
    /// Clamps inner corner positions so they can never extend beyond outer corner boundaries.
    /// </summary>
    private void EnforceInnerContainment()
    {
        if (centerPoint == null) return;
        Vector3 centerPos = centerPoint.position;

        for (int i = 0; i < 8; i++)
        {
            if (innerRbs[i] == null || outerRbs[i] == null) continue;

            Vector3 outerPos = outerCornerPoints[i].position;
            Vector3 innerPos = innerCornerPoints[i].position;

            Vector3 centerToOuter = outerPos - centerPos;
            float maxAllowedDistance = centerToOuter.magnitude - innerContainmentPadding;

            if (maxAllowedDistance <= 0.01f) maxAllowedDistance = 0.01f;

            Vector3 centerToInner = innerPos - centerPos;
            float currentInnerDistance = centerToInner.magnitude;

            if (currentInnerDistance > maxAllowedDistance)
            {
                Vector3 clampedDir = centerToInner.normalized;

                // Hard-clamp transform position
                innerCornerPoints[i].position = centerPos + clampedDir * maxAllowedDistance;

                // Neutralize outward velocity component
                Vector3 currentVel = innerRbs[i].linearVelocity;
                float outwardSpeed = Vector3.Dot(currentVel, clampedDir);
                if (outwardSpeed > 0)
                {
                    innerRbs[i].linearVelocity -= clampedDir * outwardSpeed;
                }
            }
        }
    }

    /// <summary>
    /// Updates the PhysicsMaterial assigned to all jelly node colliders at runtime.
    /// </summary>
    /// <param name="newMaterial">The new PhysicsMaterial to apply.</param>
    public void UpdateJellyMaterial(PhysicsMaterial newMaterial)
    {
        jellyPhysicsMaterial = newMaterial;

        foreach(Transform t in outerCornerPoints)
        {
            if (t.gameObject.GetComponent<Collider>() != null)
            {
                t.gameObject.GetComponent<Collider>().sharedMaterial = jellyPhysicsMaterial;
            }
        }
    }

    private Rigidbody SetupNode(Transform t, float colRadius, List<Collider> colliderList, bool enableMaterial = true)
    {
        Rigidbody rb = t.GetComponent<Rigidbody>();
        if (rb == null)
        {
            rb = t.gameObject.AddComponent<Rigidbody>();
        }

        if(t != centerPoint){
            rb.mass = massPerNode;
        }
        rb.linearDamping = drag;
        rb.angularDamping = drag;
        rb.useGravity = true;
        rb.interpolation = RigidbodyInterpolation.Interpolate;

        Collider col = t.GetComponent<Collider>();
        if (col == null)
        {
            SphereCollider sphere = t.gameObject.AddComponent<SphereCollider>();
            sphere.radius = colRadius;
            col = sphere;
        }

        // Assign physics material to child colliders
        if (jellyPhysicsMaterial != null && enableMaterial)
        {
            col.sharedMaterial = jellyPhysicsMaterial;
        }

        colliderList.Add(col);
        return rb;
    }

    /// <summary>
    /// Configures full 6-DOF springs (Linear + Angular) using ConfigurableJoint.
    /// </summary>
    private void AddSpringJoint(Rigidbody a, Rigidbody b, float linearSpring, float angularSpring)
    {
        ConfigurableJoint joint = a.gameObject.AddComponent<ConfigurableJoint>();
        joint.connectedBody = b;

        // Allow joint axes to move/rotate freely within drive limits
        joint.xMotion = ConfigurableJointMotion.Free;
        joint.yMotion = ConfigurableJointMotion.Free;
        joint.zMotion = ConfigurableJointMotion.Free;

        joint.angularXMotion = ConfigurableJointMotion.Free;
        joint.angularYMotion = ConfigurableJointMotion.Free;
        joint.angularZMotion = ConfigurableJointMotion.Free;

        joint.rotationDriveMode = RotationDriveMode.Slerp;

        // Position drive setup (Linear spring)
        JointDrive linDrive = new JointDrive
        {
            positionSpring = linearSpring,
            positionDamper = springDamper,
            maximumForce = float.MaxValue
        };
        joint.xDrive = linDrive;
        joint.yDrive = linDrive;
        joint.zDrive = linDrive;

        // Angular drive setup (Rotational spring constraint)
        JointDrive angDrive = new JointDrive
        {
            positionSpring = angularSpring,
            positionDamper = angularSpringDamper,
            maximumForce = float.MaxValue
        };
        joint.slerpDrive = angDrive;

        joint.enableCollision = false;
    }
}