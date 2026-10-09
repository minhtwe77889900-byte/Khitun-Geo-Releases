import json
import pathlib
import re
import sys
from pyproj import CRS, Transformer

root = pathlib.Path(sys.argv[1]) if len(sys.argv) > 1 else pathlib.Path(__file__).resolve().parent.parent / 'src' / 'KhitunGeo' / 'wwwroot'
controls = []
updates = json.loads((root / 'crs' / 'regional-updates.json').read_text(encoding='utf-8'))
for family in ['msk', 'sk63']:
    def key(record):
        return record['id'] if family == 'sk63' else (str(record['code']), record.get('variant', ''), record.get('zone') or 1)
    effective = {key(r): r for r in json.loads((root / 'crs' / (family + '.json')).read_text(encoding='utf-8'))}
    effective.update({key(r): r for r in updates.get(family, [])})
    for record in effective.values():
        parts = dict(re.findall(r'\+(\w+)=([^\s]+)', record['proj']))
        lon0 = float(parts['lon_0'])
        # Promote both ends to 3D so ellipsoidal heights participate in Helmert.
        transformer = Transformer.from_crs(CRS.from_epsg(4979), CRS.from_proj4(record['proj']).to_3d(), always_xy=True)
        for lat, offset in [(30, -1), (55, 0.5), (70, 2.5)]:
            lon = (lon0 + offset + 180) % 360 - 180
            east, north, h = transformer.transform(lon, lat, 100)
            controls.append({'family': family, 'record': record['name'], 'input': [lat, lon, 100], 'expected': [north, east, h]})
output = pathlib.Path(sys.argv[2]) if len(sys.argv) > 2 else pathlib.Path(__file__).parent / 'crs-proj-controls.json'
output.write_text(json.dumps(controls, ensure_ascii=False), encoding='utf-8')
print(f'{len(controls)} independent PROJ controls generated')
