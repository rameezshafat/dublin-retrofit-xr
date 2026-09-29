using UnityEngine;
using UnityEngine.InputSystem;

namespace DublinRetrofit
{
    // Map-style camera: it orbits a pivot point on the ground.
    // Right-drag = orbit, middle-drag = pan, scroll = zoom.
    // The camera state is just (pivot, yaw, pitch, distance); the transform is rebuilt from
    // those every frame, which avoids drift and makes clamping trivial.
    public class OrbitCamera : MonoBehaviour
    {
        [SerializeField] Vector3 pivot = Vector3.zero;
        [SerializeField] float yaw = 20f;          // degrees around the vertical axis
        [SerializeField] float pitch = 50f;        // degrees above the horizon
        [SerializeField] float distance = 650f;    // metres from pivot

        [SerializeField] float orbitSpeed = 0.25f; // degrees per pixel dragged
        [SerializeField] float panSpeed = 0.0015f; // fraction of distance per pixel dragged
        [SerializeField] float zoomStep = 0.1f;    // 10 % closer/further per scroll step
        [SerializeField] Vector2 pitchLimits = new Vector2(10f, 89f);
        [SerializeField] Vector2 distanceLimits = new Vector2(20f, 1500f);

        void Start() => Apply();

        void LateUpdate()
        {
            Mouse mouse = Mouse.current;
            if (mouse == null) return; // e.g. no mouse on this platform

            Vector2 delta = mouse.delta.ReadValue();

            if (mouse.rightButton.isPressed)
            {
                yaw += delta.x * orbitSpeed;
                pitch = Mathf.Clamp(pitch - delta.y * orbitSpeed, pitchLimits.x, pitchLimits.y);
            }

            if (mouse.middleButton.isPressed)
            {
                // Pan along the ground plane, not the view plane, so the pivot stays at y = 0.
                // Scaling by distance keeps the drag speed feeling the same at any zoom.
                Vector3 right = transform.right;
                Vector3 forward = Vector3.ProjectOnPlane(transform.forward, Vector3.up).normalized;
                pivot -= (right * delta.x + forward * delta.y) * panSpeed * distance;
            }

            // F focuses the selected building, a common shortcut in 3D tools.
            ScenarioState state = ScenarioState.Instance;
            Keyboard keyboard = Keyboard.current;
            if (keyboard != null && keyboard.fKey.wasPressedThisFrame && state != null && state.Selected != null)
                FocusOn(state.Selected.GetComponent<Renderer>().bounds.center, 150f);

            float scroll = mouse.scroll.ReadValue().y;
            if (Mathf.Abs(scroll) > 0.01f)
            {
                // Multiplicative zoom: equal scroll steps feel equal whether close or far.
                distance *= 1f - Mathf.Sign(scroll) * zoomStep;
                distance = Mathf.Clamp(distance, distanceLimits.x, distanceLimits.y);
            }

            Apply();
        }

        // Moves the pivot to a point on the ground and sets the viewing distance.
        public void FocusOn(Vector3 point, float newDistance)
        {
            pivot = new Vector3(point.x, 0f, point.z);
            distance = Mathf.Clamp(newDistance, distanceLimits.x, distanceLimits.y);
            Apply();
        }

        void Apply()
        {
            Quaternion rotation = Quaternion.Euler(pitch, yaw, 0f);
            transform.SetPositionAndRotation(pivot - rotation * Vector3.forward * distance, rotation);
        }
    }
}
