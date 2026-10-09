'use strict';
const assert=require('node:assert/strict'),crypto=require('node:crypto');
const core=require('../src/KhitunGeo/wwwroot/workspace-core.js');
let failures=0;
function test(name,fn){try{fn();console.log('PASS '+name);}catch(e){failures++;console.error('FAIL '+name+': '+e.stack);}}
function dxf(pairs){return ['0','SECTION','2','ENTITIES',...pairs.flat().map(String),'0','ENDSEC','0','EOF',''].join('\n');}
test('SHA-256 matches independent Node implementation including Unicode and multiple blocks',()=>{
 for(const v of ['', 'abc', 'Точки −2,5 🗺', 'a'.repeat(10000)])assert.equal(core.sha256(v),crypto.createHash('sha256').update(v).digest('hex'));
});
test('quality is non-mutating and supports comma and Unicode minus',()=>{
 const points=[{no:'1',x:'65',y:'84',z:'−2,5'},{no:'1',x:'65',y:'84',z:''},{no:'3',x:'999',y:'84',z:'-'}],before=JSON.stringify(points);
 const report=core.quality(points,true);assert.equal(JSON.stringify(points),before);assert.equal(report.filter(q=>q.row===0).length,0);assert(report.some(q=>q.message.includes('Повтор')));assert.equal(report.filter(q=>q.severity==='error').length,2);
});
test('ASCII DXF supports lines, points and circles with layers',()=>{
 const r=core.parseDxf(dxf([[0,'LINE'],[8,'Контур'],[10,10],[20,20],[11,30],[21,40],[0,'POINT'],[10,5],[20,6],[0,'CIRCLE'],[10,0],[20,0],[40,2]]));
 assert.equal(r.paths.length,3);assert.deepEqual(r.paths[0].vertices,[[10,20],[30,40]]);assert(r.layers.includes('Контур'));assert(r.paths[2].closed);
});
test('LWPOLYLINE closes and unsupported objects are explicitly counted',()=>{
 const r=core.parseDxf(dxf([[0,'LWPOLYLINE'],[70,1],[10,0],[20,0],[10,1],[20,1],[0,'INSERT'],[2,'BLOCK'],[0,'LWPOLYLINE'],[10,0],[20,0],[42,.5]]));
 assert(r.paths[0].closed);assert.equal(r.skipped.INSERT,1);assert.equal(r.skipped['LWPOLYLINE (недостаточно вершин)'],1);
});
test('2D POLYLINE accepts vertices, 3D geometry is not silently flattened',()=>{
 const r=core.parseDxf(dxf([[0,'POLYLINE'],[70,1],[0,'VERTEX'],[10,0],[20,0],[0,'VERTEX'],[10,1],[20,2],[0,'SEQEND'],[0,'POLYLINE'],[70,8],[0,'VERTEX'],[10,2],[20,3],[0,'SEQEND']]));
 assert.equal(r.paths.length,1);assert.equal(r.skipped['POLYLINE (3D/кривые/сетка)'],1);
});
test('malformed, binary, tilted OCS and invalid coordinate DXF fail clearly',()=>{
 assert.throws(()=>core.parseDxf('AutoCAD Binary DXF'),/ASCII/);assert.throws(()=>core.parseDxf('0\nSECTION\n2'),/пара/);
 assert.throws(()=>core.parseDxf(dxf([[0,'LINE'],[10,'oops'],[20,0],[11,1],[21,2]])),/координаты/);
 assert.throws(()=>core.parseDxf(dxf([[0,'LINE'],[210,1],[10,0],[20,0],[11,1],[21,2]])),/поддерживаемой/);
 assert.throws(()=>core.parseDxf(dxf([[0,'ARC'],[10,0],[20,0],[40,1],[50,'Infinity'],[51,20]])),/углы/);
 const huge=core.parseDxf(dxf([[0,'ARC'],[10,0],[20,0],[40,1],[50,-1e300],[51,1e300]]));assert(huge.total<300);
});
test('HTML report is self-contained and escapes markup',()=>{
 const html=core.passportHtml({'Проект':'<img src=x onerror=alert(1)>',quality:[]});assert(!html.includes('<img'));assert(html.includes('&lt;img'));assert(html.includes("default-src 'none'"));assert(html.includes('size:A4'));
});
test('stored workspace rejects malformed overlays and profile containers',()=>{
 assert.throws(()=>core.validateExtras({profiles:{}}),/профили/);assert.throws(()=>core.validateExtras({drawing:{}}),/DXF/);
 assert.deepEqual(core.validateExtras({}),{});
});
test('survey comparison joins exact point numbers, not row positions',()=>{
 const a=[{no:'001',x:10,y:20,z:-5},{no:'2',x:30,y:40,z:''},{no:'old',x:1,y:2,z:3}],b=[{no:'2',x:30,y:40,z:8},{no:'001',x:13,y:24,z:-7},{no:'new',x:0,y:0,z:0}],before=JSON.stringify([a,b]);
 const r=core.compareSurveys(a,b,.1,.1);assert.equal(r.rows[0].plan,5);assert.equal(r.rows[0].dz,-2);assert.equal(r.rows[1].dz,null);assert.equal(r.exceeded,1);assert.deepEqual(r.added,['new']);assert.deepEqual(r.missing,['old']);assert.equal(JSON.stringify([a,b]),before);assert(core.comparisonCsv(r).includes('Нет во второй съёмке'));
});
test('comparison rejects ambiguous IDs and preserves 001 versus 1',()=>{
 assert.throws(()=>core.compareSurveys([{no:'a',x:1,y:2},{no:'a',x:3,y:4}],[]),/Повтор/);assert.throws(()=>core.compareSurveys([{no:'',x:1,y:2}],[]),/номера/);
 const r=core.compareSurveys([{no:'001',x:1,y:2}],[{no:'1',x:1,y:2}]);assert.equal(r.rows.length,0);assert.equal(r.added[0],'1');assert.throws(()=>core.compareSurveys([],[],-1,1),/Допуски/);
});
test('strict survey CSV parser handles quotes, multiline text, axis order and empty Z',()=>{
 const r=core.readPoints('no;E;N;Z;D\r\n001;20;10;;"берег; \"\"А\"\"\nлиния"','header',';');assert.equal(r.points[0].no,'001');assert.equal(r.points[0].x,'10');assert.equal(r.points[0].z,'');assert.equal(r.points[0].d,'берег; "А"\nлиния');
 assert.equal(core.readPoints('1,20,10,-5,Дно','penzd',',').points[0].x,'10');assert.throws(()=>core.readPoints('1;2;3','header',';'),/заголовки/);assert.throws(()=>core.readPoints('no;N;E\n1;"2;3','header',';'),/кавычка/);
 const json=core.readPoints('{"source":"msk164","points":[{"no":"a","x":1,"y":2}]}');assert.equal(json.source,'msk164');assert.equal(json.points[0].z,'');
});
test('point CSV round-trips text and negative heights',()=>{
 const p=[{no:'001',x:'12.125',y:'55',z:'-1,25',d:'берег; "А"'}];assert.deepEqual(core.readPoints(core.pointCsv(p)).points,p);
});
test('stored ZIP headers, filenames, UTF-8 bytes and CRC match standard',()=>{
 const zip=core.zipStored([{name:'точки.csv',text:'123456789'}]),v=new DataView(zip.buffer);assert.equal(v.getUint32(0,true),0x04034b50);assert.equal(v.getUint32(14,true),0xcbf43926);assert.equal(v.getUint16(6,true),0x800);const n=v.getUint16(26,true);assert.equal(new TextDecoder().decode(zip.slice(30,30+n)),'точки.csv');assert.equal(new TextDecoder().decode(zip.slice(30+n,30+n+9)),'123456789');assert.equal(v.getUint32(zip.length-22,true),0x06054b50);
});
const near=(a,b)=>assert(Math.abs(a-b)<1e-8,`${a} != ${b}`);
const drawingFile=(blocks,entities,layers=[])=>['0','SECTION','2','TABLES',...layers.flat(),'0','ENDSEC','0','SECTION','2','BLOCKS',...blocks.flat(),'0','ENDSEC',dxf(entities)].join('\n');
test('nested blocks apply base points, rotation, reflection and inherited layer 0',()=>{
 const blocks=[[0,'BLOCK'],[2,'inner'],[10,1],[20,2],[0,'LINE'],[8,'0'],[10,1],[20,2],[11,2],[21,2],[0,'LINE'],[8,'Own'],[10,1],[20,2],[11,1],[21,3],[0,'ENDBLK'],[0,'BLOCK'],[2,'outer'],[10,0],[20,0],[0,'INSERT'],[2,'inner'],[10,10],[20,0],[41,-2],[42,3],[50,90],[0,'ENDBLK']];
 const r=core.parseDxf(drawingFile(blocks,[[0,'INSERT'],[2,'outer'],[8,'\\U+041Aонтур'],[10,100],[20,200]]));
 assert.equal(r.paths.length,2);assert.equal(r.paths[0].layer,'Контур');assert.deepEqual(r.paths[0].parents,['Контур']);assert.equal(r.paths[1].layer,'Own');
 for(const [a,b] of r.paths[0].vertices[0].map((n,i)=>[n,[110,200][i]]))near(a,b);
 near(r.paths[0].vertices[1][0],110);near(r.paths[0].vertices[1][1],198);near(r.paths[1].vertices[1][0],107);
});
test('array insertion spacing rotates but is not scaled, recursive blocks terminate',()=>{
 const blocks=[[0,'BLOCK'],[2,'A'],[0,'POINT'],[10,0],[20,0],[0,'INSERT'],[2,'A'],[0,'ENDBLK']];
 const r=core.parseDxf(drawingFile(blocks,[[0,'INSERT'],[2,'A'],[41,2],[50,90],[70,2],[71,2],[44,10],[45,20]]));
 assert.equal(r.paths.length,4);near(r.paths[1].vertices[0][1],10);near(r.paths[2].vertices[0][0],-20);assert.equal(r.skipped['INSERT (циклический/глубокий блок)'],4);
});
test('bulge arcs retain direction, endpoints, closing edge and exact analytic bounds',()=>{
 for(const bulge of [1,-1,.5,-2]){
  const r=core.parseDxf(dxf([[0,'LWPOLYLINE'],[10,0],[20,0],[42,bulge],[10,2],[20,0]])),p=r.paths[0],v=core.drawingVertices(p,100);
  near(v[0][0],0);near(v[0][1],0);near(v.at(-1)[0],2);near(v.at(-1)[1],0);assert.equal(Math.sign(p.curve.sweep),Math.sign(bulge));
  const b=core.drawingBounds(p);for(const q of v)assert(q[0]>=b[0]-1e-9&&q[0]<=b[2]+1e-9&&q[1]>=b[1]-1e-9&&q[1]<=b[3]+1e-9);
 }
 const r=core.parseDxf(dxf([[0,'LWPOLYLINE'],[70,1],[10,0],[20,0],[10,2],[20,0],[42,1]]));assert.equal(r.paths.length,2);assert(r.paths[1].curve);
});
test('nonuniform scaled block circle remains an ellipse with bounded adaptive display',()=>{
 const r=core.parseDxf(drawingFile([[0,'BLOCK'],[2,'C'],[0,'CIRCLE'],[10,0],[20,0],[40,1],[0,'ENDBLK']],[[0,'INSERT'],[2,'C'],[10,10],[20,20],[41,2],[42,3],[50,90]]));
 const p=r.paths[0],b=core.drawingBounds(p);[7,18,13,22].forEach((n,i)=>near(b[i],n));assert.equal(p.vertices.length,0);assert.equal(r.total,4);assert(core.drawingVertices(p,1e12).length<=8193);
});
test('off and frozen layer names survive Unicode decoding and workspace round trip',()=>{
 const r=core.parseDxf(drawingFile([],[[0,'LINE'],[8,'\\U+041Aонтур'],[10,0],[20,0],[11,2],[21,3]],[[0,'LAYER'],[2,'\\U+041Aонтур'],[62,-7],[0,'LAYER'],[2,'Frozen'],[70,1]]));
 assert.deepEqual(r.hidden,['Контур','Frozen']);const w={drawing:{paths:r.paths,hidden:r.hidden,frame:'x',scale:1,opacity:.5}};assert.deepEqual(core.validateExtras(w),w);
 const invalid={drawing:{...w.drawing,paths:[{layer:'0',vertices:[],curve:{c:[0,0],u:[1,0],v:[0,1],start:0,sweep:Infinity}}]}};assert.throws(()=>core.validateExtras(invalid),/дуга/);
});
if(failures)process.exitCode=1;
