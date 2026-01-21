using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class PlayerMovement : MonoBehaviour
{
	// we have a lot of variables but we are free
	[SerializeField, Range(0f, 100f)]
	float maxSpeed = 6f, maxClimbSpeed = 2f, maxSwimSpeed = 4f;

	// used for running / climbing ( maybe overall exertion like stardew? )
	[SerializeField, Range(0f, 100f)]
	float playerStamina = 60f;

	[SerializeField, Range(0f, 100f)]
	float maxAcceleration = 10f, maxAirAcceleration = 4f, maxClimbAcceleration = 8f, maxSwimAcceleration = 8f;

	[SerializeField, Range(0f, 10f)]
	float jumpHeight = 1f;

	[SerializeField, Range(0f, 90f)]
	// when horizontal, y = 0, when vertical y = 1 
	// componenet varies between those extremes based on slope angle (it's cosign of the angle)
	// looking at the dot product of the up vector and the surface normal
	// dot product: A*B = ||A|| ||B|| cosθ (it is the cosine of the angle between vectors, * by their lengths)
	float maxGroundAngle = 40f;

	// allow just a bit beyond a 45 degree overhang, no ceilings 
	[SerializeField, Range(90, 180)]
	float maxClimbAngle = 140f;

	// whether player is in water, submergance value of 0 = no water touched while value of 1 = completely underwater
	bool InWater => submergence > 0f;
	float submergence;

	// configured angle defines minimum result that still counts as ground
	float minGroundDotProduct, minClimbDotProduct;

	Vector3 playerInput;
	Vector3 velocity, connectionVelocity;

	// since all dynamic objects should have rigidbody component, we can find connected bodies
	// not enough to know connectedBody is there, need to know if we've remained in contact with same body if so we should be moving with it
	Rigidbody rigidbody, connectedBody, previousConnectedBody;
	// if connected body is free moving phys object, then it would have velocity

	// but if using kinematic animations, will have to derive connected velocity ourselves by tracking pos
	// in case object is rotating tracking pos is not enough, as its not affected by rotation
	// keep track of local position, as that point is "orbiting" the rigidbodys local origin
	Vector3 connectionWorldPosition, connectionLocalPosition;

	bool desiredJump, desiresClimbing;
	int groundContactCount, steepContactCount, climbContactCount;
	int stepsSinceLastGrounded, stepsSinceLastJump;

	// checks if groundcontact count is greater than 0
	// check for contacts that are way to steep to count as ground too
	bool OnGround => groundContactCount > 0;
	bool OnSteep => steepContactCount > 0;
	// check if we are climbing, like ground snapping, turn off climbers grip if it slows down jumps away from the wall
	bool Climbing => climbContactCount > 0 && stepsSinceLastJump > 2; 
	// indicates whether swimThreshold has been reached
	bool Swimming => submergence >= swimThreshold;

	int jumpPhase;

	// current contact normal
	Vector3 contactNormal, steepNormal, climbNormal, lastClimbNormal;
	
	// camera
	[SerializeField]
	Transform playerInputSpace = default;

	// snap to ground speed, 100 = snapping always happens when possible
	[SerializeField, Range(0f, 100f)]
	float maxSnapSpeed = 100f;

	// limit range of raycast to make sure we are not always snapping
	// create probe mask for other player models to ignore raycast's on them, set to -1 to match all layers ( set all layers in editor EXCEPT Ignore raycast & Agent )
	// use watermask to check whether we're inside a trigger zone with a layer tagged water
	[SerializeField, Min(0f)]
	float probeDistance = 1f;
	[SerializeField]
	LayerMask probeMask = -1, climbMask = -1, waterMask = 0;

	// upaxis relavent to player local position, no longer world position
	Vector3 upAxis, rightAxis, forwardAxis;

	// control when our player counts as being in water and when their fully submerged
	// measure a point offset above players center, straight down to the maximum range.
	[SerializeField]
	float submergenceOffset = 0.5f;
	[SerializeField, Min(0.1f)]
	float submergenceRange = 1f;

	[SerializeField, Range(0f, 10f)]
	float waterDrag = 1f;
	[SerializeField, Range(0f, 10f)]
	float buoyancy = 1f;

	// minimum submergence required to be classified as swimming, 0.5 meaning at least players bottom half is underwater
	[SerializeField, Range(0.01f, 1f)]
	float swimThreshold = 0.5f;

	// debug materials
	[SerializeField]
	Material normalMaterial = default, climbingMaterial = default, swimmingMaterial = default;
	MeshRenderer meshRenderer;

	// specify angle in degrees, but multiply by deg2rad to change it radians
	// dot products are tricky, maybe research more
	void OnValidate () {
		minGroundDotProduct = Mathf.Cos(maxGroundAngle * Mathf.Deg2Rad);
		minClimbDotProduct = Mathf.Cos(maxClimbAngle * Mathf.Deg2Rad);
	}

	void Awake () {
		rigidbody = GetComponent<Rigidbody>();
		rigidbody.useGravity = false;
		meshRenderer = GetComponent<MeshRenderer>();
		OnValidate();
	}

  void Update () {
		playerInput.x = Input.GetAxis("Horizontal");
		playerInput.y = Input.GetAxis("Vertical");

		// if swimming, let SPACE have us go up and E go down - currently unused, better flow to swimming without it
		// (calculating swimaccel against buoyancy is hard but it is possible if we go down this route)
		// playerInput.z = Swimming ? Input.GetAxis("UpDown") : 0f;

		// combine check with previous value with OR
		// remains true once enabled until explicity set back to false
		desiredJump |= Input.GetButtonDown("Jump");
		desiresClimbing = Input.GetButton("Climb");
		
		// if input space not set keep world space, otherwise convert from provided space to world space
		// since forward speed is affected by vertical orbit angle, the further it deviates from horizontal the slower the sphere moves
		// happens because we expect desired velocity to lie in xz plane, by retrieving forward and right vectors from player input space
		// discarding their y components and normalizing them, then the player input? becomes the sum of those vectors scale by player input (wahoo)
		if (playerInputSpace) {
			rightAxis = ProjectDirectionOnPlane(playerInputSpace.right, upAxis);
			forwardAxis = ProjectDirectionOnPlane(playerInputSpace.forward, upAxis);
			}
			else {
				rightAxis = ProjectDirectionOnPlane(Vector3.right, upAxis);
				forwardAxis = ProjectDirectionOnPlane(Vector3.forward, upAxis);
			}

		meshRenderer.material = Climbing ? climbingMaterial : Swimming ? swimmingMaterial : normalMaterial;
		// ty cat like coding
	}

	// adjust velocity, gets invoked at start of each phycics simulation step
	// how often this happens depends on the time step, (50 fps default )
	// set upaxis as opposite direction of gravity pull to make it equal to negated normalized gravity vector
	void FixedUpdate() {
		// grab gravity and upaxis from worldphysics.cs methods
		Vector3 gravity = WorldPhysics.GetGravity(rigidbody.position, out upAxis);
		UpdateState();

		// use linear damping to apply waterDrag, apply drag first so some acceleration is always possible
		// if not completely submerged, then dont apply maximum drag
		if (InWater) {
			velocity *= 1f - waterDrag * submergence * Time.deltaTime;
		}

		AdjustVelocity();

		if (desiredJump) {
			desiredJump = false;
			Jump(gravity);
		}

		// to overcome out corners and slipping, always accelerate the player towards the surface it's climbing (climber grip)
		// reduce to 90% to make sure we dont have to much and have player get stuck around corners
		if (Climbing) {
			velocity -= contactNormal * (maxClimbAcceleration * 0.9f * Time.deltaTime);
		}

		// if not climbing but in water, apply gravity scaled by 1 - buoyancy
		// if not fully sumberged dont apply full buoyancy
		// overrides all other applications of gravity
		else if (InWater) {
			velocity += gravity * ((1f - buoyancy * submergence) * Time.deltaTime);
		}

		// apply force to counter gravity to allow for standing still on slopes and such
		// simulate this by projecting gravity to contact normal when on ground /w low velocity
		else if (OnGround && velocity.sqrMagnitude < 0.01f) {
			velocity += contactNormal * (Vector3.Dot(gravity, contactNormal) * Time.deltaTime);
		}

		else {
			velocity += gravity * Time.deltaTime;
		}

		// assign velocity to rigidbody
		rigidbody.velocity = velocity;
		ClearState();
	}

	// clears accumulation of normal vectors, and set contact normal to zero
	void ClearState () {
		groundContactCount = steepContactCount = climbContactCount = 0;
		contactNormal = steepNormal = connectionVelocity = climbNormal = Vector3.zero;
		previousConnectedBody = connectedBody;
		connectedBody = null;
		submergence = 0f;
	}

	void UpdateState() {
		stepsSinceLastGrounded += 1;
		stepsSinceLastJump += 1;
		// collisions effect velocity, so retrieve it from rigidbody before adjusting to match
		velocity = rigidbody.velocity;

		// snaptoground keeps us stuck to ground if needed, if true then grounded, only called when onground is false
		if (CheckClimbing() || CheckSwimming() || OnGround || SnapToGround() || CheckSteepContacts()) {
			stepsSinceLastGrounded = 0;
			jumpPhase = 0;
			if (groundContactCount > 1) {
			contactNormal.Normalize();
			}
		}

		// but if we don't touch ground, use up axis to find contactNormal of ground
		else {
			contactNormal = upAxis;
		}

		if (connectedBody) {
			// don't stick to light bodies, only update connection state if connected body is kinematic or larger than player
			if (connectedBody.isKinematic || connectedBody.mass >= rigidbody.mass) { 
			UpdateConnectionState();
			}
		}
	}

	void UpdateConnectionState () {
		// movement of connection is found by - the connection position we had from the connections current position - no longer like this
		// find it's velocity by dividing movement by delta time
		// only use if current and previous connected bodies are same, otherwise remain zero
		// connection movement is now found by converting connection local position back into world space, then subtract stored world space -  now like this
		if (connectedBody == previousConnectedBody) {
		Vector3 connectionMovement = connectedBody.transform.TransformPoint(connectionLocalPosition) - connectionWorldPosition;
		connectionVelocity = connectionMovement / Time.deltaTime;
		}

		// use player's position as connection position in world space
		connectionWorldPosition = rigidbody.position;
		// the same point as above, but in local space instead
		connectionLocalPosition = connectedBody.transform.InverseTransformPoint(connectionWorldPosition);
	}

	// if we have taken any steps abort, as we have contact OR if too soon after jumping (determined by 2 steps since last jump)
	// if raycast hit something below us (retrieves tag from out raycast hit to confirm ground), then there is ground, if not abort
	// check normal vector from hit to check if ground
	// if we have not aborted yet, we've lost contact w/ ground but we are still above it, so snap to it by mapping found normal as contact normal
	// adjust velocity to align with ground by aligned desired velocity but making sure we keep current speed (calculated as velocity)
	// set explicity that triggers can not be detected by our physics queries (i think) (QueryTriggerInteraction.Ignore)
	bool SnapToGround() {
		if (stepsSinceLastGrounded > 1 || stepsSinceLastJump <= 2) {
			return false;
		}

		// if current speed exceeds max snap speed abort
		float speed = velocity.magnitude;
		if (speed > maxSnapSpeed) {
			return false;
		}

		if (!Physics.Raycast(rigidbody.position, -upAxis, out RaycastHit hit, probeDistance, probeMask, QueryTriggerInteraction.Ignore )) {
			return false;
		}

		float upDot = Vector3.Dot(upAxis, hit.normal);
		if (upDot < minGroundDotProduct) {
			return false;
		}

		groundContactCount = 1;
		contactNormal = hit.normal;

		float dot = Vector3.Dot(velocity, hit.normal);
		// only recalculate velocity if it doesnt slow down falling to the ground, other words only recalculate when dot product and surface normal is positive
		if (dot > 0f) {
		velocity = (velocity - hit.normal * dot).normalized * speed;
		}
		// keep track of connected body if ground
		connectedBody = hit.rigidbody;
		return true;
	}

	// return status of converting steep contacts to ground (crevasse prevention), if multiple steep contacts normalize and check if ground
	bool CheckSteepContacts() {
		if (steepContactCount > 1) {
			steepNormal.Normalize();
			float upDot = Vector3.Dot(upAxis, steepNormal);
			if (upDot >= minGroundDotProduct) {
				groundContactCount = 1;
				contactNormal = steepNormal;
				return true;
			}
		}
		return false;
	}

	// for climbing, we dont want to change the camera's up vector because it should always match gravity
	// so make movement relative to the wall and gravity, and ignore camera orientation
	void AdjustVelocity() {
		// give us vectors aligned with ground, return normalized to get proper directions
		// dont use default inputs for right and forward for x and z axes before projection onto the contact plane
		// instead use the up axis for Z, and cross product of the contact normal and up axis for X (swaps orientation when climbing)
		float acceleration, speed;
		Vector3 xAxis, zAxis;

		// if climbing us climb acceleration and speed
		if (Climbing) {
			acceleration = maxClimbAcceleration;
			speed = maxClimbSpeed;
			xAxis = Vector3.Cross(contactNormal, upAxis);
			zAxis = upAxis;
		}

		// if not climbing and in water, use swim acceleration and speed
		// deeper we are in water use swim acceleration and speed
		// so interpolate between regular and swim values based on swim factor (subermegence / swim threshold) constrained to a max of 1
		// acceleration depends on whether we're on the ground
		else if (InWater) {
			float swimFactor = Mathf.Min(1f, submergence / swimThreshold);
			acceleration = Mathf.LerpUnclamped(OnGround ? maxAcceleration : maxAirAcceleration, maxSwimAcceleration, swimFactor);
			speed = Mathf.LerpUnclamped(maxSpeed, maxSwimSpeed, swimFactor);
			xAxis = rightAxis;
			zAxis = forwardAxis;
		}

		// start with camera directions, and then project directions onto the ground to align with ground / climbed wall
		else {
			acceleration = OnGround ? maxAcceleration : maxAirAcceleration;
			speed = maxSpeed;
			xAxis = rightAxis;
			zAxis = forwardAxis;
			xAxis = ProjectDirectionOnPlane(rightAxis, contactNormal);
			zAxis = ProjectDirectionOnPlane(forwardAxis, contactNormal);
		}

		// dont calculate zAxis outside of else statement? breaks climbing because .. 

		// project current velocity to get relative x and z speeds - no longer do this
		// with the new implementation of connectedBodies, we make sphere accelerate to match speed of connectedBody
		// while also accelerating toward desired velocity relative to connection velocity
		Vector3 relativeVelocity = velocity - connectionVelocity;
		float currentX = Vector3.Dot(relativeVelocity, xAxis);
		float currentZ = Vector3.Dot(relativeVelocity, zAxis);

		// which acceleration we use depends whether player is on ground or not
		// when FixedUpdate gets invoked Time.deltaTime is equal to Time.fixedDeltaTime.
		// now calculated relative to ground
		float maxSpeedChange = acceleration * Time.deltaTime;

		float newXSpeed = Mathf.MoveTowards(currentX, playerInput.x * speed, maxSpeedChange);
		float newZSpeed = Mathf.MoveTowards(currentZ, playerInput.y * speed, maxSpeedChange);

		// adjust velocity by adding differences between new and old speeds along relative axes
		velocity += xAxis * (newXSpeed - currentX) + zAxis * (newZSpeed - currentZ);

		// ignored for now / permenently gameplay is pretty cool without it
		// only when swimming, find the current and new x,y,z velocity and use them to adjust velocity for diving and surfacing ( i believe ) 
		//if (Swimming) {
		//	float currentY = Vector3.Dot(relativeVelocity, upAxis);
			//float newY = Mathf.MoveTowards(currentY, playerInput.z * speed, maxSpeedChange);

		//	velocity += upAxis * (newY - currentY);
		// }

	}

	void Jump(Vector3 gravity) {
		// vy=√2gh, where g is gravity and h is desired height, -2 * gravity no longer works due to using magnitude of gravity vector
		// g is assumed to be negative ( as gravity is )
		if (OnGround) { 
			stepsSinceLastJump = 0;
			float jumpSpeed = Mathf.Sqrt(2f * gravity.magnitude * jumpHeight);
			
			// while in shallow water, make it harder to jump
			if (InWater) {
				jumpSpeed *= Mathf.Max(0f, 1f - submergence / swimThreshold);
			}

			// if we already have upward speed, then subtract from jumpspeed before adding to players y velocity.
			// but if we are already going faster than jump speed, then we do not want jump to slow use
			
			// find aligned speed by projecting velocity onto contact normal, found by calculating their dot product
			float alignedSpeed = Vector3.Dot(velocity, contactNormal);
			if ( alignedSpeed > 0f ) {
				jumpSpeed = Mathf.Max(jumpSpeed - alignedSpeed, 0f); 
			}
			// velocity.y += jumpSpeed;
			// add jump contact normal scaled by the jump speed to the velocity when jumping (yeah!)
			velocity += contactNormal * jumpSpeed;
		}
	}

	// like onCollisionEnter & onCollisionStay, but they're for they're for colliders and handle the collider parameter instead
	// use waterCollider to pass the collider to evaluate submergence
	// use its attached rigidbody for the connected body if we are swimming, if water is too shallow ignore it
	// is calling the collider waterCollider correct since it's a trigger (perhaps)
	void OnTriggerEnter (Collider waterCollider) {
		if ((waterMask & (1 << waterCollider.gameObject.layer)) != 0) {
			EvaluateSubmergence(waterCollider);
		}
	}

	void OnTriggerStay (Collider waterCollider) {
		if ((waterMask & (1 << waterCollider.gameObject.layer)) != 0) {
			EvaluateSubmergence(waterCollider);
		}
	}

	// use raycast from offset point (on player?) straight down up to the submergance range using the water mask
	// use querytrigger collide to hit water and then set the submergence value equal to 1 minus hit distance / range
	// if we dont hit anything we're fully submerged so just hard set to 1 
	// gaurd against invalid submergence when moving out of water by increasing length of raycast by one unity
	void EvaluateSubmergence (Collider collider) {
		if (Physics.Raycast (rigidbody.position + upAxis * submergenceOffset, -upAxis, out RaycastHit hit, submergenceRange + 1f, waterMask, QueryTriggerInteraction.Collide)) {
			submergence = 1f - hit.distance / submergenceRange;
		}

		else {
			submergence = 1f;
		}

		if (Swimming) {
			connectedBody = collider.attachedRigidbody;
		}
		
	}

	void OnCollisionEnter (Collision collision) {
		EvaluateCollision(collision);
	}

	// invoked on each physics step as long as collision remains alive
	void OnCollisionStay (Collision collision) {
		EvaluateCollision(collision);
	}

	// retrieve collision data and evaluate 
	void EvaluateCollision (Collision collision) {
	
	// if we are swimming and connected to a water rigidbody already, then do not replace with another rigidbody
	// we can just skip as we don't need connection information at all
	// no longer true we need climbing contacts for style and clout
	//if (Swimming) {
	//		return;
	//	}

	// the amount of contact points are determined via contactCount property
	// use that to loop through all points, pass to index and retrieve points normals
	// the normal being the direction the player should be pushed away from collision surface
	// pass over max ground angle as minGroundDotProduct (40 degree angle) 
	// determine if "slope" is ground based off this
	// store ground contact into contactNormal
	// check only for climb contacts while swimming, ignore rest to bypass snap to ground
		for (int i = 0; i < collision.contactCount; i++) {
				int layer = collision.gameObject.layer;
				Vector3 normal = collision.GetContact(i).normal;
				float upDot = Vector3.Dot(upAxis, normal);
				//onGround |= normal.y >= minGroundDotProduct;
				if (upDot >= minGroundDotProduct) {
					if (!Swimming) {
					groundContactCount += 1;
					// accumulate instead of overriding
					contactNormal += normal;
					// assign property of rigidbody collision to our field
					connectedBody = collision.rigidbody;
					}
				}
				// if no ground contact check wheter ground if may be steep contact instead 
				// dot product of perfecticly vertical wall should be 0, but accept -0.01f for a bit of give
				else { 
					//if contact doesnt count as ground, check for steep and climb contact seperately
					// always use climb contacts connected body to have player climb surfaces in motion
					if ( upDot > -0.01f) {
						if (!Swimming) {
					steepContactCount += 1;
					steepNormal += normal;
						}
					}
					
					// only include climb contact if it is masked
					// besides accumulating climbNormal's find lastClimbNormal to help with crevass detection
					if (desiresClimbing && upDot >= minClimbDotProduct && (climbMask & (1 << layer)) !=0) {
					climbContactCount += 1;
					climbNormal += normal;
					lastClimbNormal = normal;
					connectedBody = collision.rigidbody;
					}
				}
			}
		}

	// return whether we are climbing, make ground contact count and normal equal to climbing equivalents
	// also determine if there are multiple climbing contacts, if so normalize and check if ground, which indicates crevasse
	// to get out of crevasse use last climb normal instead of aggregate, so we climb one of the walls to get out
	bool CheckClimbing() {
		if (Climbing) {
			if (climbContactCount > 1 ) {
				climbNormal.Normalize();
				float upDot = Vector3.Dot(upAxis, climbNormal);
				if (upDot >= minGroundDotProduct) {
					climbNormal = lastClimbNormal;
				}
			}
			groundContactCount = 1;
			contactNormal = climbNormal;
			return true;
		}
		return false;
	}

	// if swimming set ground contact to zero and make contact normal equal to up axis
	bool CheckSwimming() {
		if (Swimming) {
			groundContactCount = 0;
			contactNormal = upAxis;
			return true;
		}
		return false;
	}

	// project velocity onto a plane to align our desired velocity with the ground
	// take dot product of the vector and normal before, then subtract the normal scaled by that from the original velocity vector3.
	// we need directions on a plane for our upAxis implementation, replace oncontact plane with directiononplane 
	Vector3 ProjectDirectionOnPlane (Vector3 direction, Vector3 normal) {
		return (direction - normal * Vector3.Dot(direction, normal)).normalized;
	}

}
