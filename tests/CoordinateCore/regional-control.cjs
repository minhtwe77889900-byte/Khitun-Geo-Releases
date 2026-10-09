const assert = require('node:assert/strict');
const fs = require('node:fs');
const path = require('node:path');
const adapter = require('../../src/KhitunGeo/wwwroot/coordinate-adapter.js');

const root = path.resolve(__dirname, '../../src/KhitunGeo/wwwroot');
const control = JSON.parse(fs.readFileSync(path.join(__dirname, 'gsk2011-zone14-msk164-control.json'), 'utf8'));
const systems = JSON.parse(fs.readFileSync(path.join(root, 'crs/native-systems.json'), 'utf8')).systems;
const gsk = systems.find(system => system.id === 'gsk6').definition;

const html = fs.readFileSync(path.join(root, 'index.html'), 'utf8');
const parserSource = html.slice(html.indexOf('function parseRegionalRecord('), html.indexOf('function installRegionalCatalogues('));
const parseRecord = new Function(parserSource + ';return parseRegionalRecord')();
const base = JSON.parse(fs.readFileSync(path.join(root, 'crs/msk.json'), 'utf8'));
const updates = JSON.parse(fs.readFileSync(path.join(root, 'crs/regional-updates.json'), 'utf8')).msk || [];
const mskRecord = [...base, ...updates].map(record => parseRecord(record, 'msk')).find(record => record.id === '164||1');
assert(mskRecord, 'MSK-164 zone 1 record must exist');
const msk = mskRecord;
const options = { auto: true, manual: 14, sourcePrefix: true, targetPrefix: true };
const tolerance = control.observations.acceptedToleranceMeters;

function checkPlanar(result, expected, label) {
  const error = Math.hypot(result.x - expected.xNorthMeters, result.y - expected.yEastMeters);
  assert(error <= tolerance, `${label}: planar error ${error} m exceeds ${tolerance} m`);
}

const forward = adapter.convertBatch({
  version: 1,
  source: gsk,
  target: msk,
  zone: options,
  points: [{ name: control.id, x: control.source.xNorthMeters, y: control.source.yWithZonePrefixMeters, height: null, description: control.description }]
});
assert.deepEqual(forward.targetZones, []);
checkPlanar(forward.points[0], control.target, 'GSK-2011 zone 14 to MSK-164 zone 1');

const reverse = adapter.convertBatch({
  version: 1,
  source: msk,
  target: gsk,
  zone: options,
  points: [{ name: control.id, x: control.target.xNorthMeters, y: control.target.yEastMeters, height: null, description: control.description }]
});
assert.deepEqual(reverse.targetZones, [control.reverseResult.zone]);
checkPlanar(reverse.points[0], control.reverseResult, 'MSK-164 zone 1 to GSK-2011 zone 14');

console.log(`PASS ${control.id}: forward and reverse planar results within ${tolerance} m`);
