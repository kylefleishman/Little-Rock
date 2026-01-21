using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[RequireComponent(typeof(Camera))]
public class OrbitCamera : MonoBehaviour
{
  [SerializeField]
	Transform focus = default;

	[SerializeField, Range(1f, 20f)]
	float distance = 5f;

	// relax camera movement by making the camera only move when its focus point differs to much from the ideal focus
	[SerializeField, Min(0f)]
	float focusRadius = 1f;
	Vector3 focusPoint, previousFocusPoint;

	[SerializeField, Range(0f, 1f)]
	float focusCentering = 0.5f;

	// orientation of camera can be described with two orbit angles
	// x defines vertical orientation, with y angle defining horizontal
	Vector2 orbitAngles = new Vector2(45f, 0f);

	// used for manual orbit control, add rotation speed, expressed in degrees per second
	[SerializeField, Range(1f, 360f)]
	float rotationSpeed = 90f;
	[SerializeField, Range(0f, 90f)]
	float alignSmoothRange = 45f;

	// constrain camera angle's to prevent it from becoming un-proper (mario 64 cam)
	[SerializeField, Range(-89f, 89f)]
	float minVerticalAngle = -30f, maxVerticalAngle = 60f;

	// automatically align camera behind player after set time
	[SerializeField, Min(0f)]
	float alignDelay = 5f;
	float lastManualRotationTime;

	// make it possible for camera to intersect some geometry by ignoring it when performing box cast (good for bypassing small detailed stuff)
	[SerializeField]
	LayerMask obstructionMask = -1;

	Camera regularCamera;

	// since we have orbit angles to control camera orbit and constrain so it can't go to far up or down, to keep this no matter orientation 
	// we apply second rotation that aligns the camera orbit rotation with gravity
	Quaternion gravityAlignment = Quaternion.identity;

	// must remain unaware of gravity alignment
	Quaternion orbitRotation;

	// box cast requires 3d vector that contains the half extends of a box ( half width, height, and depth )
	// half the height can be found by taking tanget of half the cameras FOV angle in radians, scaled by it's near clip plane Distance
	// half the width is that scaled by the camera's aspect ratio
	// the depth of the box is zero
	// we could cache this and recalculate only as necessary but oh well
	Vector3 CameraHalfExtends {
		get {
			Vector3 halfExtends;
			halfExtends.y = regularCamera.nearClipPlane * Mathf.Tan(0.5f * Mathf.Deg2Rad * regularCamera.fieldOfView);
			halfExtends.x = halfExtends.y * regularCamera.aspect;
			halfExtends.z = 0f;
			return halfExtends;
		}
	}

	// max should never drop below min
	void OnValidate () {
		if (maxVerticalAngle < minVerticalAngle) {
			maxVerticalAngle = minVerticalAngle;
		}
	}

	// initiliaze camera to the focus objects position
	// make sure initial rotation matches orbit angles
	void Awake () {
		regularCamera = GetComponent<Camera>();
		focusPoint = focus.position;
		transform.localRotation = orbitRotation = Quaternion.Euler(orbitAngles);
	}

	// camera's position is found by moving it away form the focus position in opposite direction
	// by an equal amount to the configured distance
	// use position instead of local position tp focus on child objects in hierarchy
	void LateUpdate () {
		// adjust alignment to sync with current direction that is up
		// to keep orbit from spazzing use minimal rotation (FromRotation) from current alignment to new alignment (last aligned up direction to current up direction)
		gravityAlignment = Quaternion.FromToRotation(gravityAlignment * Vector3.up, WorldPhysics.GetUpAxis(focusPoint)) * gravityAlignment;
		UpdateFocusPoint();
		// create four-tuple of real numbers defining the cameras look rotation {x,y,z,w}
		// convert our vector with z rotation set to zero
		// find look directiong based on quaternion multiplied with forward vector
		// invoke set position and rotate with look position and rotation in one sweep
		
		// only constrain angles when changed, invoke constrain angles based on what manualrotation returned
		// only recalculate rotation if there was change, otherwise retrieve existing one
		if ( ManualRotation() || AutomaticRotation() ) { 
			ConstrainAngles();
			orbitRotation = Quaternion.Euler(orbitAngles);
		}

		Quaternion lookRotation = gravityAlignment *orbitRotation;

		Vector3 lookDirection = lookRotation * Vector3.forward;
		Vector3 lookPosition = focusPoint - lookDirection * distance;

		// our current approach only works if focus radius is zero, causing a bug where we can end up with a focus point inside geometry
		// will have to use ideal focus point instead, cast from there to the near plane box position
		// found by moving fromthe camera position to the focus position until we reach the near plane
		Vector3 rectOffset = lookDirection * regularCamera.nearClipPlane;
		Vector3 rectPosition = lookPosition + rectOffset;
		Vector3 castFrom = focus.position;
		Vector3 castLine = rectPosition - castFrom;
		float castDistance = castLine.magnitude;
		Vector3 castDirection = castLine / castDistance;
		
		// replace raycast with box cast, add halfextends along with box rotation as lookrotation
		// since the near clip plane sits in front of camera position, we should only cast up to that distance
		// if we end up hitting something then the final distance is the hit distance + the near plane distance
		// if something is hit then position the box as far away as possible, then find offset to frind corresponding camera position
		// set explicity that triggers can not be detected by our physics queries (i think) (QueryTriggerInteraction.Ignore)
		if (Physics.BoxCast(castFrom, CameraHalfExtends, castDirection, out RaycastHit hit, lookRotation, castDistance, obstructionMask, QueryTriggerInteraction.Ignore)) {
			rectPosition = castFrom + castDirection * hit.distance;
			lookPosition = rectPosition - rectOffset;
		}

		transform.SetPositionAndRotation(lookPosition, lookRotation);
	}

	void UpdateFocusPoint () {
		previousFocusPoint = focusPoint;
		Vector3 targetPoint = focus.position;
		// interpolate between target and current focus points, using (1 - focuscenter)^time as interpolater
		// only need to do this if distance is large enough, and centering factor is positive
		// to center and enforce focus radius we use minimum of both interpolaters for final interpolation
		if (focusRadius > 0f) {
			float distance = Vector3.Distance(targetPoint, focusPoint);
			float t = 1f;
			if (distance > 0.01f && focusCentering > 0f) {
				t = Mathf.Pow(1f - focusCentering, Time.unscaledDeltaTime);
			}

		if (distance > focusRadius) {
			t = Mathf.Min(t, focusRadius / distance);
		}
		focusPoint = Vector3.Lerp(targetPoint, focusPoint, t);
		}
	}

	// retrieves an input vector, use vertical and horizontal input axes for this
	// if there's an input exceeding a small epsilon value like 0.001, then just add the input to the orbit angles scaled by rotation speed and time delta
	bool ManualRotation () {
		Vector2 input = new Vector2(
			Input.GetAxis("Vertical Camera"),
			Input.GetAxis("Horizontal Camera")
		);
		const float e = 0.001f;
		if (input.x <- e || input.x > e || input.y < -e || input.y > e) {
			orbitAngles += rotationSpeed * Time.unscaledDeltaTime * input;
			lastManualRotationTime = Time.unscaledTime;
			return true;
		}
		return false;
	}

	// return whether it changed the orbit, abort if current time minus last manual rotation time is less than align delay
	// calculate movement vector for c urrent frame, only need 2d movement in zx plane as we are rotating horizontally
	// if the square magnitude movement vector is less than a small threshold like 0.0001, then dont bother rotating
	bool AutomaticRotation() {
		if (Time.unscaledTime - lastManualRotationTime < alignDelay) {
			return false;
		}

		// undo gravity alignment before determining correct angles
		// apply inverse gravity alignment to movement delta
		Vector3 alignedDelta = Quaternion.Inverse(gravityAlignment) * (focusPoint - previousFocusPoint);
		Vector2 movement = new Vector2( alignedDelta.x, alignedDelta.z );

		float movementDeltaSqr = movement.sqrMagnitude;
		if (movementDeltaSqr < 0.0001f) {
			return false;
		}

		// get heading angle, pass it to normalized movement vector, result is our new horizontal orbit angle
		// slow down snapping by using rotation speed for automation rotation, use math towards angle, to deal with 0 - 360 range of angles
		// find angle delta in automatic rotation, can be found by passing current and desired angle to mathf.deltaangle and taking absolute
		float headingAngle = GetAngle(movement / Mathf.Sqrt(movementDeltaSqr));
		float deltaAbs = Mathf.Abs(Mathf.DeltaAngle(orbitAngles.y, headingAngle));
		// dampen rotation of tiny anglesa bit more by scaling rotation speed by the minimum of the time delta and square movement delta
		float rotationChange = rotationSpeed * Mathf.Min(Time.unscaledDeltaTime, movementDeltaSqr);
		if (deltaAbs < alignSmoothRange) {
			rotationChange *= deltaAbs / alignSmoothRange;
		}
		// prevent camera from rotating away at full speed 
		else if (180f - deltaAbs < alignSmoothRange) {
			rotationChange *= (180f - deltaAbs) / alignSmoothRange;
		}
		orbitAngles.y = Mathf.MoveTowardsAngle(orbitAngles.y, headingAngle, rotationChange);

		return true;
	}

	// clamp veritcal orbit angle to configured range
	// horizontal orbit has no limits, but ensures angle stays within 0 - 360 range
	void ConstrainAngles() {
		orbitAngles.x = Mathf.Clamp(orbitAngles.x, minVerticalAngle, maxVerticalAngle);

	if (orbitAngles.y < 0f) {
		orbitAngles.y += 360f;
	}
	else if (orbitAngles.y >= 360f) {
		orbitAngles.y -= 360f;
	}
}
	
	// convert 2d direction to find horizontal angle matching the current direction for AutomaticRotation()
	// y component of the direction is the cosine of the angle we need, convert it from radians to degrees for use
	// the angle could represent either clockwise or counterclockwise, so if x is negative its counterclockwise and subtract it from 360
	static float GetAngle (Vector2 direction) {
		float angle = Mathf.Acos(direction.y) * Mathf.Rad2Deg;
		return direction.x < 0f ? 360f - angle : angle;
	}
}

