using UnityEngine;
using System.Collections;

public class PrAutoRotate : MonoBehaviour {

    public float speed = 1.0f;
	public float speedMinimum = 15.0f;
	public bool startUsingRandomRotation = false;
	public float acceleration = 0.0f;
    public Vector3 direction = Vector3.up;

	// Use this for initialization
	void Start () {
		if (startUsingRandomRotation)
        {
			transform.Rotate(
				direction.x * Random.Range(0,360),
				direction.y * Random.Range(0, 360),
				direction.z * Random.Range(0, 360)
				);
		}
	}
	
	// Update is called once per frame
	void Update () {
		if (acceleration != 0.0f && speed != speedMinimum)
        {
			speed += acceleration;
		}
		transform.Rotate(direction.x * Time.deltaTime * speed, direction.y * Time.deltaTime * speed, direction.z * Time.deltaTime * speed);
	}
}
