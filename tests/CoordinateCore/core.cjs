const fs = require('node:fs');
const path = require('node:path');
const vm = require('node:vm');
const assert = require('node:assert/strict');
const root = path.resolve(__dirname, '../../src/KhitunGeo/wwwroot');
const moduleFile = path.join(root, 'coordinate-core.js');
assert(fs.existsSync(moduleFile), 'Coordinate mathematics must load from a standalone module');
const core = require(moduleFile);
const controls = JSON.parse(fs.readFileSync(path.join(__dirname, 'controls.json'), 'utf8'));
const sandbox = vm.createContext({});
vm.runInContext(fs.readFileSync(moduleFile, 'utf8'), sandbox);
assert.equal(vm.runInContext('typeof KhitunCoordinates.fwdTM', sandbox), 'function');
assert.equal(vm.runInContext('typeof document + ":" + typeof window + ":" + typeof fetch', sandbox), 'undefined:undefined:undefined');
for (const c of controls) {
  const actual = core[c.operation](...c.args.map(v => v && typeof v === 'object' && v.ellipsoid ? core.E[v.ellipsoid] : v));
  assert.deepEqual(actual, c.expected, c.name);
  const browserResult = vm.runInContext(`KhitunCoordinates.${c.operation}(...${JSON.stringify(c.args)}.map(v=>v&&typeof v==='object'&&v.ellipsoid?KhitunCoordinates.E[v.ellipsoid]:v))`, sandbox);
  assert.deepEqual(Array.isArray(browserResult) ? Array.from(browserResult) : browserResult, c.expected, c.name + ' script host');
}
assert.throws(() => core.datumToWgs([65, 84, -12], 'missing'), /Неизвестный датум/);
assert.throws(() => core.wgsToDatum([65, 84, -12], 'missing'), /Неизвестный датум/);
assert.equal(core.zoneFromPrefixedY(500000), null);
assert.equal(core.zoneFromPrefixedY(Infinity), null);
assert.equal(core.zoneFromPrefixedY(284500000), null);
assert.equal(core.zoneFromPrefixedY(28500000), 28);
const html = fs.readFileSync(path.join(root, 'index.html'), 'utf8');
assert(html.indexOf('src="coordinate-core.js"') >= 0 && html.indexOf('src="coordinate-core.js"') < html.indexOf('<script>'), 'Math must load before UI startup');
assert(!html.includes('function fwdTM('), 'UI must use the extracted formulas');
console.log(`PASS standalone coordinate module: ${controls.length} frozen controls, Node/script-host parity, unknown datum and zone guards`);
