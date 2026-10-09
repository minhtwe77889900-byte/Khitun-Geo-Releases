const assert=require('node:assert/strict'),fs=require('node:fs'),path=require('node:path'),vm=require('node:vm');
const root=path.resolve(__dirname,'../../src/KhitunGeo/wwwroot');
assert(fs.existsSync(path.join(root,'coordinate-adapter.js')),'Browser-free batch adapter must exist');
const adapter=require(path.join(root,'coordinate-adapter.js'));
const geo={kind:'geo',datum:'wgs'},local={kind:'fixedtm',datum:'sk42',lon0:84,lat0:0,fe:86209.8,fn:-6542783.5,k:1};
const options={auto:true,manual:28,sourcePrefix:true,targetPrefix:true};
const request={version:1,source:geo,target:local,zone:options,points:[{name:'001',x:65,y:84,height:-12,description:'Глубина'},{name:'002',x:65.01,y:84.02,height:null,description:''}]};
const frozen=JSON.stringify(request);
const converted=adapter.convertBatch(request);
assert.equal(JSON.stringify(request),frozen,'Conversion must never mutate input');
assert.equal(converted.points[0].height,-12);assert.equal(converted.points[1].height,null);
assert.equal(converted.points[0].name,'001');assert.equal(converted.points[0].description,'Глубина');
assert.equal(converted.points.length,2);assert.deepEqual(converted.targetZones,[]);
const bad=structuredClone(request);bad.points[1].x=null;
assert.throws(()=>adapter.convertBatch(bad),/2/);assert.equal(JSON.stringify(request),frozen);
assert.throws(()=>adapter.convertBatch({...request,version:2}),/верси/i);
assert.throws(()=>adapter.convertBatch({...request,target:{...local,k:0}}),/масштаб/i);
assert.throws(()=>adapter.convertBatch({...request,target:{...local,towgs:[1,2,3]}}),/переход/i);
assert.throws(()=>adapter.convertBatch({...request,source:{kind:'regional',datum:'sk42'}}),/СК/);
assert.throws(()=>adapter.convertBatch({...request,source:{kind:'utm',datum:'wgs'},points:[{name:'1',x:-10,y:500000,height:0,description:''}]}),/полушар/i);
assert.throws(()=>adapter.convertBatch({...request,target:{kind:'utm',datum:'wgs'},points:[{name:'1',x:85,y:84,height:0,description:''}]}),/84/);
assert.deepEqual(adapter.convertBatch({...request,source:local,target:local}).points,request.points,'Exact identity must retain points');
assert.deepEqual(adapter.convertBatch({...request,points:[]}).points,[]);
const manual=adapter.convertBatch({...request,target:{kind:'gk6',datum:'sk42'},zone:{...options,auto:false,manual:14}});
assert.deepEqual(manual.targetZones,[14]);
const automatic=adapter.convertBatch({...request,target:{kind:'gk6',datum:'sk42'}});assert.deepEqual(automatic.targetZones,[15]);
assert.throws(()=>adapter.convertBatch({...request,target:{kind:'gk6',datum:'sk42'},zone:{...options,auto:false,manual:0}}),/Зона/);
const gkSource={...request,source:{kind:'gk6',datum:'sk42'},target:geo,zone:{...options,sourcePrefix:false},points:manual.points};
assert.throws(()=>adapter.convertBatch(gkSource),/вручную/);
for(const manualZone of [0,61,1.5,null]){
 const identity={...request,source:{kind:'gk6',datum:'sk42'},target:{kind:'gk6',datum:'sk42'},zone:{...options,auto:false,manual:manualZone}};
 assert.throws(()=>adapter.convertBatch(identity),/Зона/,'Identity must validate manual zone');
 assert.throws(()=>adapter.convertBatch({...identity,points:[]}),/Зона/,'Empty batch must validate manual zone');
}
assert.throws(()=>adapter.convertBatch({...request,target:{...local,kind:'custom',lat0:60}}),/lat0/,'Ignored custom latitude must not be accepted');
assert.throws(()=>adapter.convertBatch({...request,target:{...local,datum:'wgs',ellipsoid:'krass'}}),/эллипсоид/i,'Ignored ellipsoid must not be accepted');
assert.throws(()=>adapter.convertBatch({...request,target:{...local,ellipsoid:'bessel'}}),/переход/,'Bessel requires explicit datum operation');
const output=adapter.convertJson(JSON.stringify(request));assert.deepEqual(JSON.parse(output),converted);
assert.throws(()=>adapter.convertJson('{'),SyntaxError);
const sandbox=vm.createContext({});vm.runInContext(fs.readFileSync(path.join(root,'coordinate-core.js'),'utf8'),sandbox);vm.runInContext(fs.readFileSync(path.join(root,'coordinate-adapter.js'),'utf8'),sandbox);
assert.deepEqual(JSON.parse(vm.runInContext(`KhitunCoordinateAdapter.convertJson(${JSON.stringify(JSON.stringify(request))})`,sandbox)),converted);
console.log('PASS browser-free batch adapter: validation, atomicity, Z/metadata, zones, JSON and plain-script host');
// Reuse the established catalogue resolver, not a second implementation of PROJ parsing.
const html=fs.readFileSync(path.join(root,'index.html'),'utf8');
const parserSource=html.slice(html.indexOf('function parseRegionalRecord('),html.indexOf('function installRegionalCatalogues('));
const parseRecord=new Function(parserSource+';return parseRegionalRecord')();
const catalogue={};
for(const family of ['msk','sk63'])catalogue[family]=JSON.parse(fs.readFileSync(path.join(root,'crs',family+'.json'),'utf8')).map((r,i)=>parseRecord(r,family,i));
const updates=JSON.parse(fs.readFileSync(path.join(root,'crs/regional-updates.json'),'utf8'));
for(const family of ['msk','sk63']){const effective=new Map(catalogue[family].map(r=>[r.id,r]));for(const r of (updates[family]||[])){const c=parseRecord(r,family);effective.set(c.id,c);}catalogue[family]=[...effective.values()];}
const controls=JSON.parse(fs.readFileSync(path.resolve(__dirname,'../crs-proj-controls.json'),'utf8'));
let maximumPlanError=0;
for(const control of controls){
 const target=catalogue[control.family].find(r=>r.name===control.record);assert(target,control.record);
 const point={name:'001',x:control.input[0],y:control.input[1],height:control.input[2],description:'Контроль'};
 const result=adapter.convertBatch({version:1,source:geo,target,zone:options,points:[point]}).points[0];
 const error=Math.hypot(result.x-control.expected[0],result.y-control.expected[1]);maximumPlanError=Math.max(maximumPlanError,error);
 assert(error<0.002,control.record+': '+error+' m');assert.equal(result.height,point.height);
}
console.log(`PASS batch adapter against ${controls.length} independent PROJ controls; maximum planar error ${maximumPlanError} m; Z unchanged`);

const nativeSystems=JSON.parse(fs.readFileSync(path.join(root,'crs/native-systems.json'),'utf8')).systems;
for(const system of nativeSystems)adapter.convertBatch({version:1,source:geo,target:system.definition,zone:options,points:[]});
console.log(`PASS all ${nativeSystems.length} native definitions accepted by calculation adapter`);
