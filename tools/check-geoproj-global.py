"""Capture synthetic public controls for the non-regional GeoProj choices.

Run manually; offline application tests consume the resulting fixture.
"""
import argparse
import datetime
import json
from pathlib import Path
import re
import time
import urllib.parse
import urllib.request

ROOT = Path(__file__).resolve().parent.parent
SYSTEMS = {
    'wgs': 'EPSG:4326', 'gskgeo': 'EPSG:7683',
    'sk42geo': 'EPSG:4284', 'sk95geo': 'EPSG:4200',
    'pz90geo': 'EPSG:4740', 'pz02geo': 'EPSG:7678', 'pzgeo': 'EPSG:7680',
    'gsk6': 'SET:76830N', 'sk426': 'EPSG:2840N', 'sk956': 'SET:42000N',
}


def main():
    parser = argparse.ArgumentParser()
    parser.add_argument('--html', type=Path, required=True)
    parser.add_argument('--refresh', action='store_true')
    args = parser.parse_args()
    token = re.search(r'name="b0" value="([^"]+)"', args.html.read_text()).group(1)
    output = ROOT / 'tests/geoproj-global-controls.json'
    existing = json.loads(output.read_text()) if output.exists() and not args.refresh else {}
    controls = {c['id']: c for c in existing.get('cases', [])}
    points = [[55, 37.5], [65, 85], [45, 132]]
    for name, reference in SYSTEMS.items():
        if name in controls:
            continue
        parameters = [('b0', token), ('b3', 'EPSG:4326'), ('b5', reference), ('b2', 'gg'), ('b6', 'gg')]
        for lat, lon in points:
            parameters += [('b1[x]', str(lat)), ('b1[y]', str(lon))]
        request = urllib.request.Request('https://geoproj.ru/geocalc', data=urllib.parse.urlencode(parameters).encode(), headers={'User-Agent': 'KhitunGeo compatibility verification'})
        with urllib.request.urlopen(request, timeout=25) as response:
            reply = json.loads(response.read())
        if not reply.get('status') or len(reply.get('points', [])) != len(points):
            raise RuntimeError(f'Unexpected response for {name}: {reply}')
        controls[name] = {'id': name, 'reference': reference, 'points': [
            {'input': p, 'geoproj': [float(r['x']), float(r['y'])]}
            for p, r in zip(points, reply['points'])]}
        output.write_text(json.dumps({'source': 'https://geoproj.ru/', 'date': datetime.datetime.now(datetime.timezone.utc).isoformat(), 'cases': list(controls.values())}, ensure_ascii=False, indent=2) + '\n')
        print(name, flush=True)
        time.sleep(0.5)


if __name__ == '__main__':
    main()
