using System;

namespace DublinRetrofit
{
    // Plain [Serializable] classes that mirror buildings.json field-for-field.
    // JsonUtility matches by field name, so these names must not be renamed.
    // Fields in the JSON that are not declared here (e.g. meta.bbox) are simply ignored.

    [Serializable]
    public class Dataset
    {
        public DatasetMeta meta;
        public BerBand[] berBands;
        public BuildingRecord[] buildings;
    }

    [Serializable]
    public class DatasetMeta
    {
        public string area;
        public double originLat;
        public double originLon;
        public string units;
        public string footprintSource;
        public string energyData;
        public string retrofitAssumption;
        public string retrofitTarget;
        public float retrofitShare;
        public int seed;
        public int buildingCount;
        public BasemapExtent basemap;
    }

    // Rectangle in local metres covered by Assets/Textures/basemap.png (see data/build_basemap.py).
    [Serializable]
    public class BasemapExtent
    {
        public float minX, minZ, maxX, maxZ;

        public bool IsValid => maxX > minX && maxZ > minZ;
    }

    [Serializable]
    public class BerBand
    {
        public string label;
        public float maxKwhPerM2Yr;
    }

    [Serializable]
    public class FootprintPoint
    {
        public float x; // metres east of the area centre
        public float z; // metres north of the area centre
    }

    [Serializable]
    public class BuildingRecord
    {
        public int id;
        public long osmId;
        public string type;
        public string address;
        public float footprintArea;
        public float floorArea;            // footprint x storeys; weights area averages
        public float height;
        public string heightSource;
        public FootprintPoint[] footprint; // counter-clockwise seen from above, not closed
        public bool rated;                 // false for sheds/garages, which get no BER
        public string ber;
        public float kwhPerM2Yr;
        public bool retrofitted;           // chosen for upgrade in the retrofit scenario
        public string retrofitBer;
        public float retrofitKwhPerM2Yr;

        // Scenario-aware accessors so views never need their own if/else on the scenario.
        public string Ber(Scenario s) => s == Scenario.Retrofit ? retrofitBer : ber;
        public float Kwh(Scenario s) => s == Scenario.Retrofit ? retrofitKwhPerM2Yr : kwhPerM2Yr;

        public float ReductionPercent =>
            kwhPerM2Yr > 0f ? 100f * (kwhPerM2Yr - retrofitKwhPerM2Yr) / kwhPerM2Yr : 0f;

        public string DisplayName => string.IsNullOrEmpty(address) ? "Unnamed building" : address;

        // OSM uses building=yes when nobody tagged the type.
        public string DisplayType => type == "yes" ? "building (type not recorded)" : type.Replace('_', ' ');

        public string HeightNote => heightSource switch
        {
            "osm:height" => "from OpenStreetMap",
            "osm:levels" => "from OSM storey count x 3 m",
            _ => "estimated from building type",
        };
    }
}
