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
    [Tooltip("Optional Physics Material (Friction/Bounciness) applied to all jelly colliders.")]
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

    [Header("Spring Settings")]
    [Tooltip("Spring force pulling inner corners toward the central core.")]
    public float centerToInnerSpringForce = 200f;

    [Tooltip("Spring force maintaining structural integrity within the inner cube.")]
    public float innerStructuralSpringForce = 150f;

    [Tooltip("Spring force maintaining structural integrity within the outer cube.")]
    public float outerStructuralSpringForce = 100f;

    [Tooltip("Spring force connecting the inner cube shell to the outer cube shell.")]
    public float innerToOuterSpringForce = 120f;

    [Tooltip("Damping across all inter-node springs to absorb kinetic energy.")]
    public float springDamper = 10f;

    private Rigidbody[] innerRbs = new Rigidbody[8];
    private Rigidbody[] outerRbs = new Rigidbody[8];

    // Local offset vectors relative to the centerPoint's initial rotation
    private Vector3[] innerLocalOffsets = new Vector3[8];
    private Vector3[] outerLocalOffsets = new Vector3[8];

    void Start()
    {
        if (centerPoint == null || innerCornerPoints.Length != 8 || outerCornerPoints.Length != 8)
        {
            Debug.LogError("NestedJellyRigSimulator requires 1 center transform, 8 inner corner transforms, and 8 outer corner transforms.");
            return;
        }

        // Cache local offsets relative to the center point
        for (int i = 0; i < 8; i++)
        {
            innerLocalOffsets[i] = centerPoint.InverseTransformPoint(innerCornerPoints[i].position);
            outerLocalOffsets[i] = centerPoint.InverseTransformPoint(outerCornerPoints[i].position);
        }

        List<Collider> jellyColliders = new List<Collider>();

        // 1. Setup Center Node
        Rigidbody centerRb = SetupNode(centerPoint, innerColliderRadius, jellyColliders);

        // 2. Setup Inner Corner Nodes & Connect to Center
        for (int i = 0; i < 8; i++)
        {
            innerRbs[i] = SetupNode(innerCornerPoints[i], innerColliderRadius, jellyColliders);
            AddSpring(innerRbs[i], centerRb, centerToInnerSpringForce);
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
                AddSpring(innerRbs[i], innerRbs[j], innerStructuralSpringForce);
            }
        }

        // 5. Structural Springs: Outer Mesh
        for (int i = 0; i < outerRbs.Length; i++)
        {
            for (int j = i + 1; j < outerRbs.Length; j++)
            {
                AddSpring(outerRbs[i], outerRbs[j], outerStructuralSpringForce);
            }
        }

        // 6. Cross-Bracing Springs: Inner to Outer
        for (int i = 0; i < innerRbs.Length; i++)
        {
            for (int j = 0; j < outerRbs.Length; j++)
            {
                AddSpring(innerRbs[i], outerRbs[j], innerToOuterSpringForce);
            }
        }

        // 7. Disable internal collisions across ALL 17 jelly nodes
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
    /// Applies directional spring forces so corners follow the center point when it rotates.
    /// </summary>
    private void ApplyRotationFollowForces()
    {
        if (centerPoint == null) return;

        for (int i = 0; i < 8; i++)
        {
            // Inner Corner Rotation Restoring Force
            if (innerRbs[i] != null)
            {
                Vector3 targetWorldPos = centerPoint.TransformPoint(innerLocalOffsets[i]);
                ApplyRestoringForce(innerRbs[i], targetWorldPos);
            }

            // Outer Corner Rotation Restoring Force
            if (outerRbs[i] != null)
            {
                Vector3 targetWorldPos = centerPoint.TransformPoint(outerLocalOffsets[i]);
                ApplyRestoringForce(outerRbs[i], targetWorldPos);
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

    private Rigidbody SetupNode(Transform t, float colRadius, List<Collider> colliderList)
    {
        Rigidbody rb = t.GetComponent<Rigidbody>();
        if (rb == null)
        {
            rb = t.gameObject.AddComponent<Rigidbody>();
        }

        rb.mass = massPerNode;
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

        if (jellyPhysicsMaterial != null)
        {
            col.sharedMaterial = jellyPhysicsMaterial;
        }

        colliderList.Add(col);
        return rb;
    }

    private void AddSpring(Rigidbody a, Rigidbody b, float springForce)
    {
        SpringJoint spring = a.gameObject.AddComponent<SpringJoint>();
        spring.connectedBody = b;
        spring.spring = springForce;
        spring.damper = springDamper;

        spring.autoConfigureConnectedAnchor = true;
        spring.enableCollision = false;
    }
}