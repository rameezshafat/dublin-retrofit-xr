using System.Collections;
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit.Interactables;

namespace DublinRetrofit
{
    // XR counterpart of SelectionController. Once the city is built, every building gets an
    // XRSimpleInteractable (it reuses the building's MeshCollider), so the controller ray can
    // hover and select it. The events call the same ScenarioState methods as the mouse, so
    // the colours and panels behave identically in desktop and XR.
    public class XrBuildingInteraction : MonoBehaviour
    {
        IEnumerator Start()
        {
            ScenarioState state = ScenarioState.Instance;
            while (state.Dataset == null) yield return null;
            yield return null; // CityBuilder creates the buildings in response to DatasetLoaded

            foreach (BuildingView view in GameObject.Find("City").GetComponentsInChildren<BuildingView>())
            {
                var interactable = view.gameObject.AddComponent<XRSimpleInteractable>();
                interactable.hoverEntered.AddListener(_ => state.SetHovered(view));
                interactable.hoverExited.AddListener(_ =>
                {
                    // Only clear the hover if another building hasn't taken it already.
                    if (state.Hovered == view) state.SetHovered(null);
                });
                interactable.selectEntered.AddListener(_ => state.SetSelected(view));
            }
        }
    }
}
