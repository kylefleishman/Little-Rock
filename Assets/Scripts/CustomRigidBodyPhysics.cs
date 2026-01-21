using System.Collections;
using System.Collections.Generic;
using UnityEngine;

// to make it easier to append to existing objects with rigidbodies (disables "regular" gravity)
// TODO
// add logic to turn on interpolation only when needed, will make object interaction smoother and optimized
[RequireComponent(typeof(Rigidbody))]
public class CustomRigidBodyPhysics : MonoBehaviour
{
	Rigidbody customRigidBody;
	
	// rigidbody may not be perfectly in sync with ground so we assume it may be floating and could fall
	// delay time to allow object to fall and come to rest
	float floatDelay;

	// make it optional whether a rigidbody should even float to sleep or remain floated ( moving platforms i think )
	[SerializeField]
	bool floatToSleep = false;

	// water phyics
	float submergence;

	[SerializeField]
	float submergenceOffset = 0.5f;
	[SerializeField, Min(0.1f)]
	float submergenceRange = 1f;
	[SerializeField, Min(0f)]
	float buoyancy = 1f;
	[SerializeField, Range(0f, 10f)]
	float waterDrag = 1f;

	// to prevent arbitrary rotations while floating, simulate objects floating with their lightest side upwards with buoyancy offest (initially 0)
	// buoyancy will be applied at this point instead of objects origin
	[SerializeField]
	Vector3 buoyancyOffset = Vector3.zero;

	// water layer mask
	[SerializeField]
	LayerMask waterMask = 0;

	Vector3 gravity;

	// Debug Materials
	[SerializeField]
	Material isAwakeMaterial = default, isAsleepMaterial = default, isFloatingMaterial = default;
	MeshRenderer meshRenderer;

	void Awake () {
		customRigidBody = GetComponent<Rigidbody>();
		customRigidBody.useGravity = false;
		meshRenderer = GetComponent<MeshRenderer>();
	}

	// fall towards origin
	void FixedUpdate() {
		// since we apply gravity ourselves, rigidbody no longer goes to sleep (constant acceleration is being applied) 
		// use IsSleeping method to determine if awake / sleep for efficiency
		if (floatToSleep) {
			if (customRigidBody.IsSleeping()) {
				floatDelay = 0f;
				meshRenderer.material = isAsleepMaterial;
				return;
			}
		}

		// set threshold to determine when rigidbody is considered at rest (if slower than 0.01 units per second dont apply gravity)
		// set floatDelay to 0 except when below threshold
		if (customRigidBody.velocity.sqrMagnitude < 0.0001f) {
			floatDelay += Time.deltaTime;

			if (floatDelay >= 1f) {
				meshRenderer.material = isFloatingMaterial;
				return;
			}
		}

		else {
			floatDelay = 0f;
			meshRenderer.material = isAwakeMaterial;
		}

		gravity = WorldPhysics.GetGravity(customRigidBody.position);

		// if in water, apply respective water drag and buoyancy
		// instead of combining buoyancy with normal gravity, just apply via AddForceAtPosition transforming the offest into world space 
		if (submergence > 0f) {
			float drag = Mathf.Max(0f, 1f - waterDrag * submergence * Time.deltaTime);

			customRigidBody.velocity *= drag;
			customRigidBody.angularVelocity *= drag;
			customRigidBody.AddForceAtPosition(gravity * -(buoyancy * submergence), transform.TransformPoint(buoyancyOffset), ForceMode.Acceleration);

			submergence = 0f;
		}

		customRigidBody.AddForce( gravity, ForceMode.Acceleration );
	}

	// copied from PlayerMovement.cs, same logic except calulate the up axis only when needed, and don't support connected bodies ( used for water physics )
	// like onCollisionEnter & onCollisionStay, but they're for they're for colliders and handle the collider parameter instead
	// use waterCollider to pass the collider to evaluate submergence
	// use its attached rigidbody for the connected body if we are swimming, if water is too shallow ignore it
	// is calling the collider waterCollider correct since it's a trigger (perhaps)
	void OnTriggerEnter (Collider waterCollider) {
		if ((waterMask & (1 << waterCollider.gameObject.layer)) != 0) {
			EvaluateSubmergence(waterCollider);
		}
	}

	// we can sleep even while floating so account for this
	void OnTriggerStay (Collider waterCollider) {
		if (!customRigidBody.IsSleeping() && ( waterMask & (1 << waterCollider.gameObject.layer )) != 0) {
			EvaluateSubmergence(waterCollider);
		}
	}

	// use raycast from offset point (on player?) straight down up to the submergance range using the water mask
	// use querytrigger collide to hit water and then set the submergence value equal to 1 minus hit distance / range
	// if we dont hit anything we're fully submerged so just hard set to 1 
	void EvaluateSubmergence (Collider collider) {
		Vector3 upAxis = -gravity.normalized;
		if (Physics.Raycast (customRigidBody.position + upAxis * submergenceOffset, -upAxis, out RaycastHit hit, submergenceRange + 1f, waterMask, QueryTriggerInteraction.Collide)) {
			submergence = 1f - hit.distance / submergenceRange;
		}
		else {
			submergence = 1f;
		}
	}
}