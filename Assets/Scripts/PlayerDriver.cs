using UnityEngine;

namespace KartRacer
{
    /// <summary>
    /// Keyboard / gamepad input (legacy Input Manager).
    /// W/Up = accelerate, S/Down = brake/reverse, A/D or Left/Right = steer,
    /// Space = hop/drift, E or Ctrl = use item, T = respawn on track.
    /// </summary>
    [RequireComponent(typeof(KartController))]
    public class PlayerDriver : MonoBehaviour
    {
        KartController kart;

        void Awake()
        {
            kart = GetComponent<KartController>();
        }

        void Update()
        {
            float steer = Input.GetAxis("Horizontal");
            float throttle = Input.GetAxisRaw("Vertical");

            // Gamepad: A = accelerate, B = brake (Xbox layout on most platforms).
            if (Input.GetKey(KeyCode.JoystickButton0)) throttle = 1f;
            if (Input.GetKey(KeyCode.JoystickButton1)) throttle = -1f;

            kart.steer = steer;
            kart.throttle = throttle;
            kart.driftHeld = Input.GetKey(KeyCode.Space) || Input.GetKey(KeyCode.LeftShift) ||
                             Input.GetKey(KeyCode.JoystickButton5);

            if (Input.GetKeyDown(KeyCode.E) || Input.GetKeyDown(KeyCode.LeftControl) ||
                Input.GetKeyDown(KeyCode.RightControl) || Input.GetKeyDown(KeyCode.JoystickButton4) ||
                Input.GetKeyDown(KeyCode.JoystickButton2))
                kart.RequestUseItem();

            if (Input.GetKeyDown(KeyCode.T) && kart.controlsEnabled) kart.Respawn();
        }
    }
}
