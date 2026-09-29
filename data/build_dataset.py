"""Build the building dataset for the Dublin Retrofit XR prototype.

Fetches building footprints for a small Dublin area from OpenStreetMap
(Overpass API), projects them to local metres, estimates heights, and
attaches SAMPLE energy ratings. Output is a JSON file that Unity reads
with JsonUtility.

Energy ratings are synthetic. They follow the SEAI BER scale (A1 to G),
but they are NOT real ratings for these buildings.

Usage:
    python3 build_dataset.py            # fetch from Overpass, then build
    python3 build_dataset.py --offline  # rebuild from the cached raw file
"""

import json
import math
import random
import sys
import urllib.parse
import urllib.request
from pathlib import Path

HERE = Path(__file__).parent
RAW_PATH = HERE / "osm_raw.json"
# Unity reads the dataset from StreamingAssets so it is copied verbatim into builds
OUT_PATH = HERE.parent / "Assets" / "StreamingAssets" / "buildings.json"

# Glasnevin, Dublin 9 (residential streets beside the Botanic Gardens). south, west, north, east
BBOX = (53.3690, -6.2700, 53.3735, -6.2610)
AREA_NAME = "Glasnevin, Dublin 9"

OVERPASS_URLS = [
    "https://overpass-api.de/api/interpreter",
    "https://overpass.private.coffee/api/interpreter",
    "https://overpass.kumi.systems/api/interpreter",
]
SEED = 42
METRES_PER_LEVEL = 3.0
GROUND_MARGIN = 60  # metres of map shown beyond the outermost building

# SEAI BER bands: label -> upper bound in kWh/m2/yr (G is open-ended)
BER_BANDS = [
    ("A1", 25), ("A2", 50), ("A3", 75),
    ("B1", 100), ("B2", 125), ("B3", 150),
    ("C1", 175), ("C2", 200), ("C3", 225),
    ("D1", 260), ("D2", 300),
    ("E1", 340), ("E2", 380),
    ("F", 450), ("G", 550),
]
# Sample distribution weighted towards older housing stock (mostly C and D)
BER_WEIGHTS = {
    "A1": 0, "A2": 1, "A3": 2, "B1": 2, "B2": 3, "B3": 5,
    "C1": 9, "C2": 11, "C3": 12, "D1": 13, "D2": 12,
    "E1": 8, "E2": 6, "F": 5, "G": 4,
}
RETROFIT_TARGET = "B2"  # typical deep-retrofit target, used as a sample assumption
# Share of eligible buildings (rated worse than the target) upgraded in the scenario.
# 100 % uptake is unrealistic; grant-driven programmes reach a fraction of homes.
RETROFIT_SHARE = 0.4

# Outbuildings are not assessed for a BER, so they get no energy values at all.
UNRATED_TYPES = {"shed", "garage", "garages", "carport", "hut", "greenhouse", "roof"}

DEFAULT_LEVELS = {
    "house": 2, "detached": 2, "semidetached_house": 2, "terrace": 2,
    "residential": 2, "apartments": 4, "commercial": 3, "retail": 1,
    "school": 2, "church": 3, "garage": 1, "garages": 1, "shed": 1,
}


def fetch_osm():
    s, w, n, e = BBOX
    query = f"""
    [out:json][timeout:60];
    way["building"]({s},{w},{n},{e});
    out tags geom;
    """
    data = urllib.parse.urlencode({"data": query}).encode()
    last_error = None
    for url in OVERPASS_URLS:
        req = urllib.request.Request(
            url, data=data,
            headers={"User-Agent": "dublin-retrofit-xr/0.1 (research prototype)"},
        )
        try:
            with urllib.request.urlopen(req, timeout=90) as resp:
                raw = json.load(resp)
        except Exception as err:  # busy mirrors return 429/504; try the next one
            print(f"{url}: {err}")
            last_error = err
            continue
        RAW_PATH.write_text(json.dumps(raw))
        return raw
    raise RuntimeError(f"all Overpass mirrors failed: {last_error}")


def to_local_metres(lat, lon, lat0, lon0):
    """Equirectangular projection around the area centre. Accurate to well
    under 1 m across a few hundred metres, which is fine for visualisation."""
    x = math.radians(lon - lon0) * 6371000 * math.cos(math.radians(lat0))
    z = math.radians(lat - lat0) * 6371000
    return round(x, 2), round(z, 2)


def polygon_area(points):
    a = 0.0
    for (x1, z1), (x2, z2) in zip(points, points[1:] + points[:1]):
        a += x1 * z2 - x2 * z1
    return a / 2.0


def estimate_height(tags):
    if "height" in tags:
        try:
            return float(tags["height"].replace("m", "").strip()), "osm:height"
        except ValueError:
            pass
    if "building:levels" in tags:
        try:
            levels = float(tags["building:levels"])
            return levels * METRES_PER_LEVEL, "osm:levels"
        except ValueError:
            pass
    levels = DEFAULT_LEVELS.get(tags.get("building", ""), 2)
    return levels * METRES_PER_LEVEL, "default"


def ber_for_kwh(kwh):
    for label, upper in BER_BANDS:
        if kwh <= upper:
            return label
    return "G"


def sample_ber(rng):
    labels = list(BER_WEIGHTS)
    label = rng.choices(labels, weights=[BER_WEIGHTS[l] for l in labels])[0]
    idx = [b[0] for b in BER_BANDS].index(label)
    lower = BER_BANDS[idx - 1][1] if idx > 0 else 0
    upper = BER_BANDS[idx][1]
    kwh = round(rng.uniform(lower + 1, upper), 1)
    return label, kwh


def build(raw):
    rng = random.Random(SEED)
    # A separate generator picks retrofitted buildings, so changing RETROFIT_SHARE
    # never changes the sampled current ratings.
    retrofit_rng = random.Random(SEED + 1)
    s, w, n, e = BBOX
    lat0, lon0 = (s + n) / 2, (w + e) / 2
    target_idx = [b[0] for b in BER_BANDS].index(RETROFIT_TARGET)
    target_kwh = BER_BANDS[target_idx][1] - 10

    buildings = []
    for el in raw.get("elements", []):
        geom = el.get("geometry")
        if el.get("type") != "way" or not geom or len(geom) < 4:
            continue
        pts = [to_local_metres(p["lat"], p["lon"], lat0, lon0) for p in geom]
        if pts[0] == pts[-1]:
            pts = pts[:-1]  # drop closing duplicate
        area = polygon_area(pts)
        if abs(area) < 8:  # skip slivers and tiny sheds
            continue
        if area < 0:  # store counter-clockwise (seen from above) consistently
            pts.reverse()

        tags = el.get("tags", {})
        btype = tags.get("building", "yes")
        height, height_source = estimate_height(tags)
        levels = max(1, round(height / METRES_PER_LEVEL))

        rated = btype not in UNRATED_TYPES
        if rated:
            ber, kwh = sample_ber(rng)
            retrofitted = kwh > target_kwh and retrofit_rng.random() < RETROFIT_SHARE
        else:
            ber, kwh, retrofitted = "", 0.0, False

        buildings.append({
            "id": len(buildings),
            "osmId": el["id"],
            "type": btype,
            "address": " ".join(
                v for v in (tags.get("addr:housenumber"), tags.get("addr:street")) if v
            ),
            "footprintArea": round(abs(area), 1),
            # Floor area (footprint x storeys) is what BER kWh/m2 figures are per,
            # so area-wide averages are weighted by it.
            "floorArea": round(abs(area) * levels, 1),
            "height": round(height, 1),
            "heightSource": height_source,
            "footprint": [{"x": x, "z": z} for x, z in pts],
            "rated": rated,
            "ber": ber,
            "kwhPerM2Yr": kwh,
            "retrofitted": retrofitted,
            "retrofitBer": RETROFIT_TARGET if retrofitted else ber,
            "retrofitKwhPerM2Yr": target_kwh if retrofitted else kwh,
        })

    # Ground rectangle (local metres) = building bounds plus a margin. The basemap image
    # is cropped to exactly this rectangle, and Unity places the ground quad on it.
    xs = [p["x"] for b in buildings for p in b["footprint"]]
    zs = [p["z"] for b in buildings for p in b["footprint"]]
    basemap = {
        "minX": round(min(xs) - GROUND_MARGIN), "minZ": round(min(zs) - GROUND_MARGIN),
        "maxX": round(max(xs) + GROUND_MARGIN), "maxZ": round(max(zs) + GROUND_MARGIN),
    }

    return {
        "meta": {
            "area": AREA_NAME,
            "bbox": list(BBOX),
            "originLat": lat0,
            "originLon": lon0,
            "units": "metres; x = east, z = north",
            "footprintSource": "OpenStreetMap contributors (ODbL), via Overpass API",
            "energyData": "SAMPLE values on the SEAI BER scale, not real ratings",
            "retrofitAssumption": (
                f"{RETROFIT_SHARE:.0%} of rated buildings worse than {RETROFIT_TARGET}, "
                f"chosen at random, move to {RETROFIT_TARGET}"),
            "retrofitTarget": RETROFIT_TARGET,
            "retrofitShare": RETROFIT_SHARE,
            "seed": SEED,
            "buildingCount": len(buildings),
            "basemap": basemap,
        },
        "berBands": [{"label": l, "maxKwhPerM2Yr": u} for l, u in BER_BANDS],
        "buildings": buildings,
    }


def main():
    raw = json.loads(RAW_PATH.read_text()) if "--offline" in sys.argv else fetch_osm()
    dataset = build(raw)
    OUT_PATH.write_text(json.dumps(dataset, indent=1))
    b = dataset["buildings"]
    print(f"{len(b)} buildings written to {OUT_PATH.name}")
    if b:
        xs = [p["x"] for bb in b for p in bb["footprint"]]
        zs = [p["z"] for bb in b for p in bb["footprint"]]
        print(f"extent x {min(xs):.0f}..{max(xs):.0f} m, z {min(zs):.0f}..{max(zs):.0f} m")
        src = {}
        for bb in b:
            src[bb["heightSource"]] = src.get(bb["heightSource"], 0) + 1
        print("height sources:", src)
        rated = [bb for bb in b if bb["rated"]]
        counts = {}
        for bb in rated:
            counts[bb["ber"]] = counts.get(bb["ber"], 0) + 1
        print("sample BER mix:", dict(sorted(counts.items())))

        # The same area summary the Unity InfoPanel shows, for cross-checking.
        labels = [band[0] for band in BER_BANDS]
        poor = labels.index("D1")
        floor = sum(bb["floorArea"] for bb in rated)
        now = sum(bb["kwhPerM2Yr"] * bb["floorArea"] for bb in rated) / floor
        after = sum(bb["retrofitKwhPerM2Yr"] * bb["floorArea"] for bb in rated) / floor
        poor_now = sum(labels.index(bb["ber"]) >= poor for bb in rated) / len(rated)
        poor_after = sum(labels.index(bb["retrofitBer"]) >= poor for bb in rated) / len(rated)
        print(f"rated {len(rated)}, unrated {len(b) - len(rated)}, "
              f"retrofitted {sum(bb['retrofitted'] for bb in b)}")
        print(f"floor-area weighted kWh/m2/yr: now {now:.0f} -> after {after:.0f}; "
              f"D or worse: {poor_now:.0%} -> {poor_after:.0%}")


if __name__ == "__main__":
    main()
