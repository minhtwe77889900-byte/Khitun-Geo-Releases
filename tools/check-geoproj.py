"""Compare public GeoProj outputs with the offline catalogue; cache synthetic controls.

No requests run in CI. Run manually with --refresh to update the reference results.
Requires pyproj only for this diagnostic tool; the application and offline tests do not.
"""
import argparse
import concurrent.futures
import datetime
import hashlib
from html.parser import HTMLParser
import json
import math
from pathlib import Path
import re
import time
import urllib.parse
import urllib.request
from pyproj import CRS, Transformer

ROOT = Path(__file__).resolve().parent.parent
DATA = ROOT / 'src/KhitunGeo/wwwroot/crs'


class CatalogueParser(HTMLParser):
    def __init__(self):
        super().__init__()
        self.select = ''
        self.option = None
        self.catalogue = {'msk': [], 'sk63': []}
        self.token = ''

    def handle_starttag(self, tag, attrs):
        a = dict(attrs)
        if tag == 'input' and a.get('name') == 'b0':
            self.token = a.get('value', '')
        if tag == 'select':
            self.select = {'msk-select-1': 'msk', 'sk63-select-1': 'sk63'}.get(a.get('id'), '')
        if tag == 'option' and self.select:
            self.option = {'value': a['value'], 'zones': [int(z) for z in a.get('data-z', '1').split(',')], 'name': ''}
            self.catalogue[self.select].append(self.option)

    def handle_data(self, data):
        if self.option is not None:
            self.option['name'] += data

    def handle_endtag(self, tag):
        if tag == 'option':
            self.option = None
        if tag == 'select':
            self.select = ''


def match_definition(family, option, zone, lists):
    if family == 'sk63':
        group = option['value']
        if group not in ['W3', 'W6']:
            group += '6' if '(6°)' in option['name'] else '3'
        return next((r for r in lists['sk63'] if r['id'] == f'SK63{group}_{zone}'), None)
    key = option['value']
    code = {'mos': 77, '1964': 78}.get(key, key.split('_')[0])
    candidates = [r for r in lists['msk'] if str(r['code']) == str(int(code) if str(code).isdigit() else code) and (r['zone'] or 1) == zone]
    if '_' in key:
        variant = {'1': '1.5-градусная', '3': '', '6': '6-градусная'}[key.split('_')[1]]
        candidates = [r for r in candidates if r['variant'] == variant]
    if len(candidates) > 1:
        candidates = [r for r in candidates if r['variant'] == 'от СК-63']
    return candidates[0] if len(candidates) == 1 else None


def make_cases(parser, lists):
    cases = []
    for family, groups in parser.catalogue.items():
        for group in groups:
            for zone in group['zones']:
                record = match_definition(family, group, zone, lists)
                key = f'{family}-{group["value"]}-{zone}'
                if record is None:
                    cases.append({'key': key, 'family': family, 'group': group, 'zone': zone, 'missing': True, 'input': [[44, 40.98333333333]]})
                    continue
                params = dict(re.findall(r'\+(\w+)=([^\s]+)', record['proj']))
                lon0 = float(params['lon_0'])
                code = str(record.get('code', ''))
                lat = 55
                if code in ['1', '5', '6', '7', '9', '15', '20', '23', '26']:
                    lat = 44
                if code in ['10', '11', '14', '29', '51', '83', '86', '87', '89']:
                    lat = 65
                if code == '77':
                    lat = 55.7
                if code == '78':
                    lat = 60
                if code in ['163', '164', '165', '170']:
                    lat = 68
                if family == 'sk63' and group['value'] in ['J', 'S', 'W6']:
                    lat = 60
                width = 2.8 if '(6°)' in group['name'] else 1.3
                if code == '77':
                    width = 0.25
                points = [[lat - 0.5, lon0 - 0.75], [lat, lon0], [lat + 0.5, lon0 + 0.75], [lat - 0.25, lon0 - width], [lat + 0.25, lon0 + width]]
                for p in points:
                    p[1] = (p[1] + 180) % 360 - 180
                cases.append({'key': key, 'family': family, 'group': group, 'zone': zone, 'record': record, 'input': points})
    return cases


def fetch_case(case, token, cache, refresh):
    filename = cache / (case['key'] + '.json')
    parameters = [('b0', token), ('b3', 'EPSG:4326'), ('b5', 'SET:MSK' if case['family'] == 'msk' else 'SET:SK63'), ('b2', 'gg'), ('b6', 'gg')]
    if case['family'] == 'msk':
        parameters += [('msk2', case['group']['value']), ('mskz2', str(case['zone']))]
    else:
        parameters += [('sk63r2', case['group']['value']), ('sk63z2', str(case['zone']))]
    for lat, lon in case['input']:
        parameters += [('b1[x]', str(lat)), ('b1[y]', str(lon))]
    signature = hashlib.sha256(urllib.parse.urlencode(parameters[1:]).encode()).hexdigest()
    cached = json.loads(filename.read_text(encoding='utf-8')) if filename.exists() and not refresh else {}
    if cached.get('signature') == signature:
        reply = cached['reply']
    else:
        request = urllib.request.Request('https://geoproj.ru/geocalc', data=urllib.parse.urlencode(parameters).encode(), headers={'User-Agent': 'KhitunGeo compatibility verification', 'Content-Type': 'application/x-www-form-urlencoded'})
        with urllib.request.urlopen(request, timeout=25) as response:
            reply = json.loads(response.read())
        if not reply.get('status') and 'Неверно задана СК' in reply.get('message', ''):
            return {**case, 'status': 'unavailable', 'message': reply['message']}
        if not reply.get('status') or len(reply.get('points', [])) != len(case['input']):
            raise RuntimeError('Unexpected response for ' + case['key'])
        filename.write_text(json.dumps({'signature': signature, 'reply': reply}, ensure_ascii=False), encoding='utf-8')
        time.sleep(0.4)  # Limited two-worker diagnostic traffic, not a CI dependency.
    if case.get('missing'):
        return {**case, 'status': 'missing', 'reply': reply}
    transformer = Transformer.from_crs(CRS.from_epsg(4979), CRS.from_proj4(case['record']['proj']).to_3d(), always_xy=True)
    points = []
    for source, result in zip(case['input'], reply['points']):
        east, north, height = transformer.transform(source[1], source[0], 0)
        expected = [float(result['x']), float(result['y'])]
        points.append({'input': source, 'geoproj': expected, 'proj': [north, east], 'error': math.hypot(north - expected[0], east - expected[1])})
    error = max(p['error'] for p in points)
    return {**case, 'status': 'match' if error <= 0.003 else 'difference', 'maxError': error, 'points': points}


def main():
    p = argparse.ArgumentParser()
    p.add_argument('--html', type=Path, required=True)
    p.add_argument('--refresh', action='store_true')
    p.add_argument('--only', default='')
    p.add_argument('--holdout', action='store_true', help='Use additional points not used to identify compatibility corrections')
    p.add_argument('--output', type=Path, default=ROOT / 'tests/geoproj-audit.json')
    args = p.parse_args()
    parser = CatalogueParser()
    parser.feed(args.html.read_text(encoding='utf-8'))
    if not parser.token or any(not parser.catalogue[family] for family in ['msk','sk63']):
        raise RuntimeError('The reference page does not contain the expected coordinate form')
    lists = {family: json.loads((DATA / (family + '.json')).read_text(encoding='utf-8')) for family in ['msk', 'sk63']}
    updates = json.loads((DATA / 'regional-updates.json').read_text(encoding='utf-8'))
    for family in lists:
        def record_key(r):
            return (str(r['code']), r['variant'], r['zone'] or 1) if family == 'msk' else r['id']
        merged = {record_key(r): r for r in lists[family]}
        merged.update({record_key(r): r for r in updates[family]})
        lists[family] = list(merged.values())
    cases = make_cases(parser, lists)
    if args.only:
        cases = [c for c in cases if any(key in c['key'] for key in args.only.split(','))]
    if args.holdout:
        for case in cases:
            lon0 = float(re.search(r'\+lon_0=([^ ]+)', case['record']['proj']).group(1))
            case['key'] += '-holdout'
            case['input'] = [[45, lon0 - 0.9], [57, lon0 + 0.3], [65, lon0 + 0.9]]
    cache = ROOT / 'tools/geoproj-cache'
    cache.mkdir(parents=True, exist_ok=True)
    results = []
    with concurrent.futures.ThreadPoolExecutor(max_workers=2) as executor:
        futures = {executor.submit(fetch_case, c, parser.token, cache, args.refresh): c for c in cases}
        try:
            for future in concurrent.futures.as_completed(futures):
                result = future.result()
                results.append(result)
                if len(results) % 20 == 0 or result['status'] != 'match':
                    print(json.dumps({'completed': len(results), 'total': len(cases), 'key': result['key'], 'status': result['status'], 'maxError': result.get('maxError')}, ensure_ascii=False), flush=True)
        except Exception:
            for future in futures:
                future.cancel()
            raise
    results.sort(key=lambda r: r['key'])
    report = {'source': 'https://geoproj.ru/', 'date': datetime.datetime.now(datetime.timezone.utc).isoformat(), 'toleranceMetres': 0.003, 'summary': {status: sum(r['status'] == status for r in results) for status in ['match', 'difference', 'missing', 'unavailable']}, 'cases': results}
    args.output.parent.mkdir(parents=True, exist_ok=True)
    args.output.write_text(json.dumps(report, ensure_ascii=False, indent=2), encoding='utf-8')
    print(json.dumps(report['summary']), flush=True)


if __name__ == '__main__':
    main()
