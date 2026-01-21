using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class WorldPhysics : MonoBehaviour
{

	// return default unity gravity value (9.81)
	public static Vector3 GetGravity (Vector3 position) {
		return Physics.gravity;
	}

	// provide both position upAxis of player and camera
	public static Vector3 GetUpAxis (Vector3 position) {
		return -Physics.gravity.normalized;
	}

	// provide both position upAxis of player and camera, and return default unity gravity value (9.81)
	// since we used OUT, method must always replace its previous upAxis value
	public static Vector3 GetGravity (Vector3 position, out Vector3 upAxis) {
		upAxis = -Physics.gravity.normalized;
		return Physics.gravity;
	}

}
