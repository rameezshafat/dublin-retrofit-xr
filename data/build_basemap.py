"""Build the ground map image for the Dublin Retrofit XR prototype.

Downloads OpenStreetMap standard tiles covering the ground rectangle stored in
buildings.json (meta.basemap, in local metres), stitches them, crops them to exactly
that rectangle and lightens them so the BER colours on the buildings stand out.

Tiles are cached in data/tiles/ so they are only downloaded once. The OSM tile usage
policy allows this (under 250 tiles, valid User-Agent, attribution shown in the app):
https://operations.osmfoundation.org/policies/tiles/

Usage:
    python3 build_basemap.py     # run after build_dataset.py
"""

import json
import math
import time
import urllib.request
from pathlib import Path

from PIL import Image, ImageEnhance

HERE = Path(__file__).parent
DATASET = HERE.parent / "Assets" / "StreamingAssets" / "buildings.json"
OUT_PATH = HERE.parent / "Assets" / "Textures" / "basemap.png"
TILE_CACHE = HERE / "tiles"

ZOOM = 18          # ~0.36 m per pixel at Dublin's latitude
TILE_SIZE = 256
MAX_TILES = 250    # OSM policy limit for downloading an area for later use
TILE_URL = "https://tile.openstreetmap.org/{z}/{x}/{y}.png"
USER_AGENT = "dublin-retrofit-xr/0.1 (research prototype; one-off basemap)"
EARTH_RADIUS = 6371000


def local_to_latlon(x, z, lat0, lon0):
    """Inverse of build_dataset.to_local_metres (equirectangular around the centre)."""
    lat = lat0 + math.degrees(z / EARTH_RADIUS)
    lon = lon0 + math.degrees(x / (EARTH_RADIUS * math.cos(math.radians(lat0))))
    return lat, lon


def latlon_to_pixel(lat, lon):
    """Web Mercator: global pixel coordinates at ZOOM (y grows southwards)."""
    world = TILE_SIZE * 2 ** ZOOM
    px = (lon + 180.0) / 360.0 * world
    lat_r = math.radians(lat)
    py = (1.0 - math.log(math.tan(lat_r) + 1.0 / math.cos(lat_r)) / math.pi) / 2.0 * world
    return px, py


def get_tile(tx, ty):
    path = TILE_CACHE / f"{ZOOM}_{tx}_{ty}.png"
    if not path.exists():
        req = urllib.request.Request(TILE_URL.format(z=ZOOM, x=tx, y=ty),
                                     headers={"User-Agent": USER_AGENT})
        with urllib.request.urlopen(req, timeout=30) as resp:
            path.write_bytes(resp.read())
        time.sleep(0.2)  # be gentle with the volunteer-run tile servers
    return Image.open(path).convert("RGB")


def main():
    meta = json.loads(DATASET.read_text())["meta"]
    lat0, lon0, rect = meta["originLat"], meta["originLon"], meta["basemap"]

    # Rectangle corners -> lat/lon -> Mercator pixels. North-west gives the top-left.
    left, top = latlon_to_pixel(*local_to_latlon(rect["minX"], rect["maxZ"], lat0, lon0))
    right, bottom = latlon_to_pixel(*local_to_latlon(rect["maxX"], rect["minZ"], lat0, lon0))

    tx0, ty0 = int(left // TILE_SIZE), int(top // TILE_SIZE)
    tx1, ty1 = int(right // TILE_SIZE), int(bottom // TILE_SIZE)
    count = (tx1 - tx0 + 1) * (ty1 - ty0 + 1)
    if count > MAX_TILES:
        raise SystemExit(f"{count} tiles needed; over the OSM limit of {MAX_TILES}. Lower ZOOM.")
    print(f"zoom {ZOOM}: {count} tiles")

    TILE_CACHE.mkdir(exist_ok=True)
    mosaic = Image.new("RGB", ((tx1 - tx0 + 1) * TILE_SIZE, (ty1 - ty0 + 1) * TILE_SIZE))
    for tx in range(tx0, tx1 + 1):
        for ty in range(ty0, ty1 + 1):
            mosaic.paste(get_tile(tx, ty), ((tx - tx0) * TILE_SIZE, (ty - ty0) * TILE_SIZE))

    # Crop to the exact rectangle (pixel offsets relative to the mosaic's top-left tile).
    ox, oy = tx0 * TILE_SIZE, ty0 * TILE_SIZE
    image = mosaic.crop((round(left - ox), round(top - oy), round(right - ox), round(bottom - oy)))

    # Mute the map (less saturation, lighter) so it reads as context, not content.
    image = ImageEnhance.Color(image).enhance(0.55)
    image = Image.blend(image, Image.new("RGB", image.size, (255, 255, 255)), 0.25)

    OUT_PATH.parent.mkdir(parents=True, exist_ok=True)
    image.save(OUT_PATH)
    w = rect["maxX"] - rect["minX"]
    h = rect["maxZ"] - rect["minZ"]
    print(f"{OUT_PATH.name}: {image.width} x {image.height} px for {w} x {h} m "
          f"({w / image.width:.2f} m/px)")


if __name__ == "__main__":
    main()
