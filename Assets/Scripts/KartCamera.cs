using UnityEngine;

namespace KartRacer
{
    /// <summary>
    /// Chase camera behind the target kart, with speed-dependent FOV, look-back (hold Q/B)
    /// and an orbit mode used on the title screen and after the race.
    /// </summary>
    [RequireComponent(typeof(Camera))]
    public class KartCamera : MonoBehaviour
    {
        public KartController target;
        public bool orbit;
        public Vector3 orbitCenter;

        Camera cam;
        float heading;
        float orbitAngle;

        void Awake()
        {
            cam = GetComponent<Camera>();
        }

        public void Snap()
        {
            if (target == null) return;
            heading = target.transform.eulerAngles.y;
            Quaternion rot = Quaternion.Euler(0f, heading, 0f);
            transform.position = target.transform.position + rot * new Vector3(0f, 3f, -7f);
            transform.LookAt(target.transform.position + Vector3.up * 1.2f);
        }

        void LateUpdate()
        {
            float dt = Time.deltaTime;
            if (orbit || target == null)
            {
                orbitAngle += dt * 12f;
                Vector3 focus = target != null ? target.transform.position : orbitCenter;
                Quaternion r = Quaternion.Euler(0f, orbitAngle, 0f);
                Vector3 desiredPos = focus + r * new Vector3(0f, 7f, -16f);
                transform.position = Vector3.Lerp(transform.position, desiredPos, 1f - Mathf.Exp(-3f * dt));
                transform.LookAt(focus + Vector3.up * 1.5f);
                cam.fieldOfView = Mathf.Lerp(cam.fieldOfView, 60f, dt * 2f);
                return;
            }

            // Follow the kart's heading smoothly; this hides the jitter of drift / spin rotations.
            float targetHeading = target.transform.eulerAngles.y;
            heading = Mathf.LerpAngle(heading, targetHeading, 1f - Mathf.Exp(-6f * dt));

            bool lookBack = Input.GetKey(KeyCode.Q) || Input.GetKey(KeyCode.B) || Input.GetKey(KeyCode.JoystickButton3);
            Quaternion rot = Quaternion.Euler(0f, heading + (lookBack ? 180f : 0f), 0f);

            float speed01 = Mathf.Clamp01(target.ForwardSpeed / (target.maxSpeed * KartController.BoostFactor));
            Vector3 offset = new Vector3(0f, 2.6f + speed01 * 0.4f, -6.2f - speed01 * 1.2f);
            Vector3 desired = target.transform.position + rot * offset;
            transform.position = Vector3.Lerp(transform.position, desired, 1f - Mathf.Exp(-12f * dt));
            transform.LookAt(target.transform.position + Vector3.up * 1.3f + rot * Vector3.forward * 3f);

            float fov = 62f + speed01 * 10f + (target.IsBoosting ? 8f : 0f);
            cam.fieldOfView = Mathf.Lerp(cam.fieldOfView, fov, 1f - Mathf.Exp(-5f * dt));
        }
    }
}
