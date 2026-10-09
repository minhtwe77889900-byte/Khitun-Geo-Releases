'use strict';
const fs = require('node:fs');
const assert = require('node:assert/strict');
const quality = require('../../src/KhitunGeo/wwwroot/workspace-core.js').quality;
const fixture = JSON.parse(fs.readFileSync(process.argv[2], 'utf8'));
assert.deepEqual(quality(fixture.points, fixture.geographic), fixture.issues);
console.log('PASS native quality parity with unchanged JavaScript');
