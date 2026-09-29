using UnityEngine;

namespace DublinRetrofit
{
    // Waits for the dataset, then creates one GameObject per building under a "City" parent:
    // mesh from BuildingMeshFactory, one shared material, a MeshCollider for mouse picking,
    // and a BuildingView that handles colour. Also lays the OSM street map on the ground.
    public class CityBuilder : MonoBehaviour
    {
        [SerializeField] Material buildingMaterial; // one shared material for every building
        [SerializeField] Material groundMaterial;   // unlit, textured with basemap.png

        const float GroundMargin = 60f; // metres of ground beyond the outermost building

        void Start()
        {
            ScenarioState state = ScenarioState.Instance;
            state.DatasetLoaded += Build;
            // In case the loader finished before we subscribed.
            if (state.Dataset != null) Build(state.Dataset);
        }

        void OnDestroy()
        {
            if (ScenarioState.Instance != null) ScenarioState.Instance.DatasetLoaded -= Build;
        }

        void Build(Dataset dataset)
        {
            var city = new GameObject("City").transform;
            var bounds = new Bounds(Vector3.zero, Vector3.zero);
            int built = 0;

            foreach (BuildingRecord record in dataset.buildings)
            {
                if (record.footprint == null || record.footprint.Length < 3) continue;

                Mesh mesh = BuildingMeshFactory.Build(record.footprint, record.height);
                var go = new GameObject($"Building {record.id}");
                go.transform.SetParent(city, false);
                go.AddComponent<MeshFilter>().sharedMesh = mesh;
                go.AddComponent<MeshRenderer>().sharedMaterial = buildingMaterial;
                // MeshCollider matches the exact footprint, so clicks hit the right building
                // even for L-shaped or terraced outlines.
                go.AddComponent<MeshCollider>().sharedMesh = mesh;
                go.AddComponent<BuildingView>().Init(record, ScenarioState.Instance);

                bounds.Encapsulate(mesh.bounds);
                built++;
            }

            CreateGround(city, dataset.meta.basemap, bounds);
            Debug.Log($"Built {built} building meshes; area extent {bounds.size.x:F0} x {bounds.size.z:F0} m");
        }

        // The ground is a quad showing the OSM street map. The Python script cropped the map
        // image to exactly meta.basemap, so stretching the quad over that rectangle lines the
        // printed streets up with the building footprints.
        void CreateGround(Transform parent, BasemapExtent extent, Bounds bounds)
        {
            Rect rect = extent != null && extent.IsValid
                ? Rect.MinMaxRect(extent.minX, extent.minZ, extent.maxX, extent.maxZ)
                : Rect.MinMaxRect(bounds.min.x - GroundMargin, bounds.min.z - GroundMargin,
                                  bounds.max.x + GroundMargin, bounds.max.z + GroundMargin);

            // A Quad faces -Z; rotating 90 degrees about X makes it face up, with texture
            // u along +X (east) and v along +Z (north), matching the image's orientation.
            GameObject ground = GameObject.CreatePrimitive(PrimitiveType.Quad);
            ground.name = "Ground";
            ground.transform.SetParent(parent, false);
            ground.transform.SetPositionAndRotation(
                new Vector3(rect.center.x, -0.02f, rect.center.y), Quaternion.Euler(90f, 0f, 0f));
            ground.transform.localScale = new Vector3(rect.width, rect.height, 1f);
            if (groundMaterial != null) ground.GetComponent<MeshRenderer>().sharedMaterial = groundMaterial;
        }
    }
}
