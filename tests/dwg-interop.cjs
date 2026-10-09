const fs=require('node:fs'),assert=require('node:assert/strict'),core=require('../src/KhitunGeo/wwwroot/workspace-core.js');
for(const version of ['AC1015','AC1024','AC1032']){
 const result=core.parseDxf(fs.readFileSync(`test-artifacts/dwg-${version}.dxf`,'utf8'));
 assert.deepEqual(result.paths,[{layer:'Контур',vertices:[[450200,7000100],[450300,7000200]],closed:false}]);
 console.log('PASS native DWG reaches JavaScript overlay with correct axes and layer: '+version);
}
