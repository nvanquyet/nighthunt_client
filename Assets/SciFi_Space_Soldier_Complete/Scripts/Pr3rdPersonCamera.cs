using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Pr3rdPersonCamera : PrTopDownCamera
{
    [Header("Third Person Camera Settings")]
    public float rotationSpeed = 1;
    [HideInInspector]
    public PrCharacterController playerController;
    public float playerDistance = 5.0f;
    public float cameraHeight = 0.9f;
    public float playerSideOffset = 0.0f;
    float cameraY, cameraZ;
    float mouseX, mouseY;
    public Transform aimingPos;
    public bool useCameraCollision = false;

    public Vector3 aimingCameraZoom = new Vector3(0, 0, 0);

    protected override void Start()
    {
        base.Start();
        if (TargetToFollow)
        {
            TargetToFollow.localPosition = new Vector3(0, 0, 0);
        }
        Cursor.visible = false;
        Cursor.lockState = CursorLockMode.Locked;
        cameraY = CameraOffset.transform.localPosition.y;
        cameraZ = CameraOffset.transform.localPosition.z;
        /*if (!useCameraCollision)
        {
            CameraOffset.GetComponent<Rigidbody>().isKinematic = true;
            CameraOffset.GetComponent<Collider>().enabled = false;
        }*/
    }

    // Start is called before the first frame update
    protected override void LateUpdate()
    {
        if (TargetToFollow)
        {
            if (playerController != null && playerController.insideVehicle == false)
            {
                TargetToFollow.position = playerController.characterUtils.transform.position;
                transform.position = Vector3.Lerp(transform.position, TargetToFollow.position + new Vector3(0, cameraHeight, 0), FollowSpeed * Time.deltaTime);

            }
            else if (playerController != null && playerController.insideVehicle)
            {
                TargetToFollow.position = playerController.vehicleToDrive.transform.position;
                transform.position = Vector3.Lerp(transform.position, TargetToFollow.position + new Vector3(0, 0, 0), FollowSpeed * Time.deltaTime);

            }
            //Check raycast to walls
            RaycastHit hitInfo;
            Vector3 camera_collision_pos = CameraOffset.transform.position;
            bool camera_collision = false;
            Vector3 direction = CameraOffset.transform.position - TargetToFollow.position + new Vector3(0, cameraHeight, 0);
            Debug.DrawLine(TargetToFollow.position + new Vector3(0, cameraHeight, 0), CameraOffset.transform.position);
            if (Physics.Raycast(TargetToFollow.position + new Vector3(0, cameraHeight, 0), direction, out hitInfo, playerDistance))
            {
                camera_collision_pos = hitInfo.point;
                camera_collision = true;
            }
            if (camera_collision == true && useCameraCollision == true)
            {
                Debug.Log("Camera Colliding");
                if (playerController && playerController.Aiming)
                {
                    //CameraOffset.transform.position = new Vector3(camera_collision_pos.x, Mathf.Lerp(CameraOffset.transform.position.y, camera_collision_pos.y, Time.deltaTime * FollowSpeed), Mathf.Lerp(CameraOffset.transform.position.z, camera_collision_pos.z + aimingCameraZoom.z, Time.deltaTime * FollowSpeed));
                    CameraOffset.transform.position = new Vector3(camera_collision_pos.x, CameraOffset.transform.position.y, camera_collision_pos.z + aimingCameraZoom.z);
                }
                else
                {
                    //CameraOffset.transform.position = new Vector3(camera_collision_pos.x, Mathf.Lerp(CameraOffset.transform.position.y, camera_collision_pos.y, Time.deltaTime * FollowSpeed), Mathf.Lerp(CameraOffset.transform.position.z, camera_collision_pos.z, Time.deltaTime * FollowSpeed));
                    CameraOffset.transform.position = new Vector3(camera_collision_pos.x, CameraOffset.transform.position.y, camera_collision_pos.z);
                }
            }
            else
            {
                Debug.Log("Camera Free");
                if (playerController && playerController.Aiming)
                {
                    CameraOffset.transform.localPosition = new Vector3(playerSideOffset, Mathf.Lerp(CameraOffset.transform.localPosition.y, cameraY, Time.deltaTime * FollowSpeed), Mathf.Lerp(CameraOffset.transform.localPosition.z, cameraZ + aimingCameraZoom.z, Time.deltaTime * FollowSpeed));
                }
                else
                {
                    CameraOffset.transform.localPosition = new Vector3(playerSideOffset, Mathf.Lerp(CameraOffset.transform.localPosition.y, cameraY, Time.deltaTime * FollowSpeed), Mathf.Lerp(CameraOffset.transform.localPosition.z, cameraZ, Time.deltaTime * FollowSpeed));

                }
            }
            if (playerController.JoystickEnabled)
            {
                mouseX += Input.GetAxis(playerController.playerCtrlMap[2]);
                mouseY -= Input.GetAxis(playerController.playerCtrlMap[3]);
            }
            else
            {
                mouseX += Input.GetAxis("Mouse X") * rotationSpeed;
                mouseY -= Input.GetAxis("Mouse Y") * rotationSpeed;
            }

           
            mouseY = Mathf.Clamp(mouseY, -35, 60);

            transform.rotation = Quaternion.Euler(mouseY, mouseX, 0);


        }
            if (isShaking && !isExpShaking)
        {
            if (actualShakeTimer >= 0.0f)
            {
                actualShakeTimer -= Time.deltaTime;
                Vector3 newPos = transform.localPosition + CalculateRandomShake(shakeFactor, false);
                transform.localPosition = Vector3.Lerp(transform.localPosition, newPos, shakeSmoothness * Time.deltaTime);
            }
            else
            {
                isShaking = false;
                actualShakeTimer = shakeTimer;
            }
        }

        else if (isExpShaking)
        {
            if (actualExpShakeTimer >= 0.0f)
            {
                actualExpShakeTimer -= Time.deltaTime;
                Vector3 newPos = transform.localPosition + CalculateRandomShake(shakeExpFactor, true);
                transform.localPosition = Vector3.Lerp(transform.localPosition, newPos, shakeExpSmoothness * Time.deltaTime);
            }
            else
            {
                isExpShaking = false;
                actualExpShakeTimer = shakeExpTimer;
            }
        }
    }

}
