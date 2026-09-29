using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;

namespace DublinRetrofit
{
    // Casts a ray from the mouse into the scene every frame.
    // Whatever BuildingView it hits becomes "hovered"; a left click makes it "selected".
    // It only writes to ScenarioState; the buildings and UI react through events.
    [RequireComponent(typeof(Camera))]
    public class SelectionController : MonoBehaviour
    {
        [SerializeField] float maxDistance = 5000f;

        Camera cam;

        void Awake() => cam = GetComponent<Camera>();

        void Update()
        {
            Mouse mouse = Mouse.current;
            ScenarioState state = ScenarioState.Instance;
            if (mouse == null || state == null) return;

            // Ignore the scene while the pointer is over a UI panel, otherwise clicking the
            // toggle button would also select or deselect the building behind it.
            if (EventSystem.current != null && EventSystem.current.IsPointerOverGameObject())
            {
                state.SetHovered(null);
                return;
            }

            BuildingView hit = null;
            Ray ray = cam.ScreenPointToRay(mouse.position.ReadValue());
            if (Physics.Raycast(ray, out RaycastHit info, maxDistance))
                hit = info.collider.GetComponent<BuildingView>(); // null for the ground

            state.SetHovered(hit);

            // Clicking empty ground clears the selection, which is the usual map behaviour.
            if (mouse.leftButton.wasPressedThisFrame)
                state.SetSelected(hit);
        }
    }
}
