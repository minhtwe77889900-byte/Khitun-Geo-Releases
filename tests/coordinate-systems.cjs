  assert(error<0.003,`${c.id}: ${error} m`);maxError=Math.max(maxError,error);count++;
    const back=t.run(`toWgs(${JSON.stringify(out)},${JSON.stringify(c.id)},true)`);
    assert(Math.hypot((back[0]-p.input[0])*111320,(back[1]-p.input[1])*111320)<0.00001,`${c.id}: reverse`);
  }
  console.log(JSON.stringify({geoprojGlobalControls:count,maxError}));
});
// Generated with pyproj 3.8.0; inputs use explicit per-record seven-parameter operations.
const controls=JSON.parse(fs.readFileSync(process.argv[3]||path.join(__dirname,'crs-proj-controls.json'),'utf8'));
test('catalogue transformations against independent PROJ controls',()=>{
  let maxPlan=0,maxHeight=0,maxBack=0;
  for(const c of controls){
    const r=q.run(`(()=>{const c=regionalCatalogues[${JSON.stringify(c.family)}].find(r=>r.name===${JSON.stringify(c.record)});regionalSelection.src[c.family]=regionalSelection.dst[c.family]=c.id;const p=${JSON.stringify(c.input)},out=fromWgs(p,c.family,true),back=toWgs(out,c.family,true);return {out,back}})()`);
    const plan=Math.hypot(r.out[0]-c.expected[0],r.out[1]-c.expected[1]);maxPlan=Math.max(maxPlan,plan);maxHeight=Math.max(maxHeight,Math.abs(r.out[2]-c.expected[2]));
    maxBack=Math.max(maxBack,Math.hypot((r.back[0]-c.input[0])*111000,(r.back[1]-c.input[1])*111000,r.back[2]-c.input[2]));
    // PROJ uses a first-order inverse rotation; Khitun solves the 3x3 inverse.
    // The Bessel/Moscow seven-parameter operation differs by about 2 mm in height.
    assert(plan<0.002,`${c.record}: ${plan} m`);assert(Math.abs(r.out[2]-c.expected[2])<0.003,`${c.record}: height ${r.out[2]-c.expected[2]} m`);
  }
  console.log(JSON.stringify({controls:controls.length,maxPlan,maxHeight,maxBack}));assert(maxBack<0.00001);
});
test('columns support Shift ranges and Ctrl toggles without selecting rows',()=>{
  const t=fresh();t.run('beginColumnSelection(1,{});beginColumnSelection(3,{shiftKey:true})');assert.equal(t.run('[...selectedColumns].join()'),'1,2,3');assert.equal(t.run('selected.size'),0);
  t.run('beginColumnSelection(2,{ctrlKey:true})');assert.equal(t.run('[...selectedColumns].join()'),'1,3');t.run('beginRowSelection(0,{})');assert.equal(t.run('selectedColumns.size'),0);
});
test('redo restores a change and new edits invalidate redo',()=>{
  const t=fresh();t.run("pts=[{no:'1',x:'55',y:'85',z:'-7'}];history=[];redoHistory=[];$('addBtn').onclick();restore();redo()");assert.equal(t.run('pts.length'),2);
  t.run("restore();$('addBtn').onclick()");assert.equal(t.run('redoHistory.length'),0);
});
test('height assignment accepts a negative Z even when the old cell is empty',()=>{
  const t=heightHarness();t.run("pts[0].z='';selected=new Set([0]);openHeightSettings();$('heightOperation').value='set';$('heightAmount').value='−12,4';applyHeightChange()");assert.equal(t.run('pts[0].z'),'-12.4');t.run('restore()');assert.equal(t.run('pts[0].z'),'');
});
test('small negative values are not rounded into a zero depth label',()=>{const t=fresh();assert.match(t.run('pointLevelText(-0.00002)'),/-0,000020/)});
test('JSON parsing is pure and does not change source on cancel',()=>{const t=fresh();t.run("$('src').value='msk164';parseJsonPoints({type:'FeatureCollection',features:[{geometry:{type:'Point',coordinates:[84,65,-12]}}]})");assert.equal(t.run("$('src').value"),'msk164')});
test('explicit XYZ headers do not duplicate X into point number',()=>{const t=fresh();assert.equal(t.run("presetMap('autocad_xy',3,['X','Y','Z'],[['12','13','-2']]).no"),-1)});
test('Z and depth survive repeated UI transforms in every regional definition',()=>{
  const t=fresh();let cases=0;for(const family of ['msk','sk63'])for(const control of controls.filter(c=>c.family===family)){
    const id=t.run(`regionalCatalogues[${JSON.stringify(family)}].find(r=>r.name===${JSON.stringify(control.record)}).id`);
    t.run(`$('src').value='wgs';$('dst').value=${JSON.stringify(family)};regionalSelection.dst[${JSON.stringify(family)}]=${JSON.stringify(id)};pts=[{no:'1',x:'${control.input[0]}',y:'${control.input[1]}',z:'-12,400'},{no:'2',x:'${control.input[0]}',y:'${control.input[1]}',z:'0'},{no:'3',x:'${control.input[0]}',y:'${control.input[1]}',z:'8.25'}];$('depthReference').value='0';$('labelMode').value='depth';applyTransform(false);`);
    assert.equal(t.run("$('src').value"),family,`${control.record}: forward transformation must execute`);
    t.run('applyTransform(false)');assert.equal(t.run("$('src').value"),'wgs',`${control.record}: reverse transformation must execute`);
    assert.equal(t.run('pts.map(p=>p.z).join(";")'),'-12,400;0;8.25');assert.equal(t.run("pointLabel({p:pts[0],i:0})"),'Гл. 12,400 м');cases++;
  }console.log(JSON.stringify({heightDepthRoundTrips:cases}));
});
test('DOM has unique identifiers and every direct JS identifier exists',()=>{
  const markup=html.split('<script>')[0],ids=[...markup.matchAll(/\bid="([^"]+)"/g)].map(m=>m[1]);assert.equal(new Set(ids).size,ids.length,'duplicate DOM IDs');
  for(const m of html.matchAll(/\$\(['"]([^'"]+)['"]\)/g))assert(ids.includes(m[1]),'missing DOM element '+m[1]);
});
test('labeled civil imports honor actual column order and absent Z stays unassigned',()=>{
  const t=fresh();assert.equal(t.run("presetMap('civil_nez',3,['E','N','Z'],[['10','20','-2']]).x"),1);assert.equal(t.run("presetMap('civil_nez',3,['E','N','Z'],[['10','20','-2']]).y"),0);
  assert.equal(t.run("presetMap('autocad_xy',3,['X','Y','Описание'],[['10','20','Точка']]).z"),-1);
});
test('invalid imports do not change existing points or create undo entries',()=>{
  const t=fresh();t.run("pts=[{no:'1',x:'65',y:'84',z:'-2'}];history=[];const savedImport=JSON.stringify(pts)");
  assert.throws(()=>t.run("commitImportedPoints([{no:'2',x:'1',y:'2',z:'-'}],'replace',null)"));
  assert.equal(t.run('JSON.stringify(pts)'),t.run('savedImport'));assert.equal(t.run('history.length'),0);
});
test('passport captures only successful transformations and survives undo/redo',()=>{
 const t=fresh(true);t.run("pts=[{no:'1',x:'65',y:'84',z:'−2,5',d:'дно'}];$('src').value='wgs';$('dst').value='msk164';history=[];applyTransform(false)");
 assert.equal(t.run("workspaceExtras.passport['Число преобразованных точек']"),1);
 assert.equal(t.run("pts[0].z"),'−2,5');assert.equal(t.run('workspaceExtras.passportResult===passportSignature(pts)'),true);
 assert.match(t.run('passportHtmlForNative()'),/SHA-256/);
 t.run('restore()');assert.equal(t.run('workspaceExtras.passport'),undefined);
 t.run('redo()');assert.equal(t.run("workspaceExtras.passport['Число преобразованных точек']"),1);
 t.run("const previous=JSON.stringify(workspaceExtras.passport);pts[0].z='-';applyTransform(false)");assert.equal(t.run('JSON.stringify(workspaceExtras.passport)'),t.run('previous'));
});
test('workspace state and exchange settings round-trip, corrupted drawing rejected atomically',()=>{
 const t=fresh(true);t.run("pts=[{no:'1',x:'65',y:'84',z:'-2'}];$('src').value='wgs';$('dst').value='msk164';workspaceExtras={profiles:[{name:'Площадка',settings:profileSettings()}]};$('expDecimals').value='6';const saved=JSON.parse(JSON.stringify(projectPayload()));$('expDecimals').value='1';applyProjectPayload(saved)");
 assert.equal(t.run("$('expDecimals').value"),'6');assert.equal(t.run('workspaceExtras.profiles[0].name'),'Площадка');
 assert.throws(()=>t.run("applyProjectPayload({...saved,points:[{no:'bad',x:'1',y:'2',z:'0'}],workspace:{drawing:{paths:[],scale:0}}})"));assert.equal(t.run('pts[0].no'),'1');
 t.run('createProject()');assert.equal(t.run('Object.keys(workspaceExtras).length'),0);
});
test('drawing is separate from table, frame mismatch hides it, undo restores it',()=>{
 const t=fresh(true);t.run("$('src').value='msk164';pts=[];snapshot();workspaceExtras.drawing={name:'test',frame:drawingFrame(),scale:1,opacity:.5,hidden:[],paths:[{layer:'0',vertices:[[10,20],[30,40]]}]};const payload=JSON.parse(JSON.stringify(projectPayload()))");
 assert.equal(t.run('drawingPoints().length'),2);assert.equal(t.run('pts.length'),0);
 t.run("$('src').value='wgs'");assert.equal(t.run('drawingPoints().length'),0);
 t.run('applyProjectPayload(payload)');assert.equal(t.run('drawingPoints()[0].e'),10);
});
test('passport HTML escapes user text and detects changes after calculation',()=>{
 const t=fresh(true);t.run("pts=[{no:'1',x:'65',y:'84',z:'-2'}];$('src').value='wgs';$('dst').value='msk164';$('projectName').value='<script>alert(1)</script>';applyTransform(false);pts[0].z='-3'");
 const text=t.run('passportHtmlForNative()');assert(!text.includes('<script>'));assert(text.includes('&lt;script&gt;'));assert(text.includes('Таблица изменена'));
});
test('new quality and passport hooks preserve negative Z across four agreed systems',()=>{
 for(const target of ['wgs','gsk3','msk164','msk165']){const t=fresh(true);const source=target==='wgs'?'msk164':'wgs';
  t.run(`$('src').value='wgs';$('dst').value='${target==='msk165'?'msk':source==='wgs'?target:source}';${target==='msk165'?"regionalSelection.dst.msk=regionalCatalogues.msk.find(r=>r.group==='165|').id;":''}pts=[{no:'1',x:'65',y:'84',z:'-12.4567',d:''},{no:'2',x:'65.01',y:'84.02',z:'-3.2',d:''}];applyTransform(false);applyTransform(false)`);
  assert.equal(t.run('pts[0].z'),'-12.4567');assert.equal(t.run('pts[1].z'),'-3.2');assert.equal(t.run('workspaceExtras.passportResult===passportSignature(pts)'),true,target+': '+t.run("$('opStatus').textContent"));
 }
});
test('Ctrl and Shift column ranges clear values only, with atomic undo and redo',()=>{
 const t=fresh();t.run("pts=[{no:'1',x:'10',y:'20',z:'-5',d:'a'},{no:'2',x:'30',y:'40',z:'-7',d:'b'}];history=[];beginColumnSelection(1,{});beginColumnSelection(3,{ctrlKey:true});deleteTableSelection()");
 assert.equal(t.run('pts.length'),2);assert.equal(t.run('pts[0].x'), '');assert.equal(t.run('pts[0].z'),'');assert.equal(t.run('pts[0].y'),'20');assert.equal(t.run('pts[0].no'),'1');t.run('restore()');assert.equal(t.run('pts[0].z'),'-5');t.run('redo()');assert.equal(t.run('pts[0].z'),'');
 t.run('restore();beginColumnSelection(1,{});beginColumnSelection(3,{shiftKey:true});deleteTableSelection()');assert.equal(t.run('pts[1].y'),'');assert.equal(t.run('pts[1].d'),'b');
});
test('batch conversion preserves live table, settings and distinct negative heights',()=>{
 const t=fresh(true);t.run("$('src').value='wgs';$('dst').value='msk164';pts=[{no:'existing',x:'65',y:'84',z:'0'}];const original=JSON.stringify(pts);const result=transformBatchPoints([{no:'1',x:'65',y:'84',z:'-12.5'},{no:'2',x:'65.01',y:'84.02',z:'-3.7'}])");
 assert.equal(t.run('JSON.stringify(pts)'),t.run('original'));assert.equal(t.run("$('src').value"),'wgs');assert.equal(t.run('result.points[0].z'),'-12.5');assert.equal(t.run('result.points[1].z'),'-3.7');assert.throws(()=>t.run("transformBatchPoints([{no:'1',x:'65',y:'',z:'0'}])"));
});
test('survey metadata mismatch is rejected without changing active project',()=>{
 const t=fresh(true);t.run("$('src').value='msk164'");assert.throws(()=>t.run("assertSurveySource({source:'wgs'})"),/СК/);assert.equal(t.run("$('src').value"),'msk164');
});
test('native project store migrates legacy data without deleting it and propagates write errors',()=>{
 const t=fresh();t.run(`const legacy=localStorage.getItem(STORE_KEY);let nativeRaw='';window.chrome={webview:{hostObjects:{sync:{projectStore:{Read:()=>nativeRaw,Write:s=>{nativeRaw=s},Validate:s=>JSON.parse(s)}}}}};const migrated=getStore();`);
 assert.equal(t.run('nativeRaw'),t.run('legacy'));assert.equal(t.run('localStorage.getItem(STORE_KEY)'),t.run('legacy'));
 t.run("probeStore({a:{points:[],payload:'x'.repeat(9*1024*1024)}})");assert.equal(t.run('nativeRaw'),t.run('legacy'));
 t.run("window.chrome.webview.hostObjects.sync.projectStore.Write=()=>{throw Error('disk full')}");assert.throws(()=>t.run('saveLocalNow()'),/disk full/);assert.equal(t.run('nativeRaw'),t.run('legacy'));
});
test('layer visibility honors both nested parent layers and entity layers; curves fit and undo',()=>{
 const t=fresh();t.run(`workspaceExtras={drawing:{frame:drawingFrame(),scale:1,opacity:.5,hidden:[],paths:[{layer:'Детали',parents:['Блок'],vertices:[],closed:true,curve:{c:[10,20],u:[3,0],v:[0,2],start:0,sweep:Math.PI*2}}]}};const before=JSON.stringify(pts);`);
 assert.equal(t.run('drawingPoints().length'),2);assert.equal(t.run('drawingPoints()[0].e'),7);assert.equal(t.run('drawingPoints()[1].n'),22);
 t.run("snapshot();workspaceExtras.drawing.hidden=['Блок']");assert.equal(t.run('drawingPoints().length'),0);t.run('restore()');assert.equal(t.run('drawingPoints().length'),2);
 t.run("workspaceExtras.drawing.hidden=['Детали']");assert.equal(t.run('drawingPoints().length'),0);assert.equal(t.run('JSON.stringify(pts)'),t.run('before'));
});
if(failures.length)process.exitCode=1;
cancellation does nothing',()=>{
  const t=heightHarness();t.run("openHeightSettings();$('heightAmount').value='1';pts[1].z='-';const original=JSON.stringify(pts);applyHeightChange()");
  assert.equal(t.run('JSON.stringify(pts)'),t.run('original'));assert.equal(t.run('history.length'),0);assert.equal(t.run("$('heightDialog').open"),true);assert.match(t.run("$('heightError').textContent"),/Строка 2/);
  t.run("$('heightCancel').onclick()");assert.equal(t.run('JSON.stringify(pts)'),t.run('original'));assert.equal(t.run("$('heightDialog').open"),false);
  t.run("pts[1].z='2';openHeightSettings();$('heightAmount').value='-1';applyHeightChange()");assert.match(t.run("$('heightError').textContent"),/положительную/);assert.equal(t.run('history.length'),0);
  t.run("$('heightAmount').value='1';$('heightScope').value='selected';selected.clear();applyHeightChange()");assert.match(t.run("$('heightError').textContent"),/Нет строк/);
});
test('negative Z entry accepts a temporary minus, comma and unicode minus without losing edits',()=>{
  const t=heightHarness(),input={dataset:{i:'0',k:'z'},value:'-'};
  t.event('tbody','input',{target:input});assert.equal(t.run('pts[0].z'),'-');assert.equal(t.run("displayCoordinate(pts[0].z,'z')"),'-');
  input.value='−12,5';t.event('tbody','input',{target:input});assert.equal(t.run('num(pts[0].z)'),-12.5);assert.equal(t.run('history.length'),1);
  t.run("$('src').value='msk164';$('expScope').value='all';const exported=makeExportData()");assert(t.run('JSON.stringify(exported)').includes('-12.500'));
  t.run('restore()');assert.equal(t.run('pts[0].z'),'10');
});
function plotHarness(){
  const t=fresh();t.run(`renderPlot=()=>{};let captured=null,plotSelections=0;selectFromPlot=()=>plotSelections++;
    $('plotCanvas').setPointerCapture=id=>captured=id;$('plotCanvas').hasPointerCapture=id=>captured===id;
    $('plotCanvas').releasePointerCapture=()=>captured=null;
    $('plotCanvas').getBoundingClientRect=()=>({left:0,top:0,width:600,height:500});
    Object.assign(plotView,{ready:true,scale:1,cx:0,cy:0,panX:0,panY:0,hits:[{i:0,sx:100,sy:100,r:8}]});`);
  t.pointer=(name,overrides={})=>{const event={pointerId:1,button:1,buttons:4,clientX:100,clientY:100,prevented:false,stopped:false,preventDefault(){this.prevented=true},stopPropagation(){this.stopped=true},...overrides};t.event('plotCanvas',name,event);return event};return t;
}
test('middle mouse pans only the plot and never selects a table row',()=>{
  const t=plotHarness();for(const name of ['mousedown','auxclick']){const e=t.pointer(name);assert(e.prevented&&e.stopped);assert.equal(t.pointer(name,{button:0}).prevented,false)}
  t.pointer('pointerdown');t.pointer('pointermove',{clientX:125,clientY:90});
  assert.equal(t.run('plotView.panX'),25);assert.equal(t.run('plotView.panY'),-10);
  t.pointer('pointerup',{clientX:125,clientY:90,buttons:0});assert.equal(t.run('plotView.drag'),false);assert.equal(t.run('captured'),null);
  t.pointer('pointerdown');t.pointer('pointerup',{buttons:0});assert.equal(t.run('plotSelections'),0);
  assert.match(t.run("$('plotCursor').textContent"),/^X: [^\n]+\nY: /);
  t.pointer('pointerleave');assert.equal(t.run("$('plotCursor').textContent"),'X: —\nY: —');
});
test('plot drag cancellation releases capture and left click still selects',()=>{
  const t=plotHarness();t.pointer('pointerdown',{button:2,buttons:2});assert.equal(t.run('plotView.drag'),false);
  for(const name of ['pointercancel','lostpointercapture']){t.pointer('pointerdown');t.pointer(name);assert.equal(t.run('plotView.drag'),false);assert.equal(t.run('captured'),null)}
  t.pointer('pointerdown');t.pointer('pointermove',{pointerId:2,clientX:150});assert.equal(t.run('plotView.panX'),0);
  t.pointer('pointermove',{buttons:0,clientX:150});assert.equal(t.run('plotView.drag'),false);assert.equal(t.run('plotView.panX'),0);
  t.pointer('pointerdown',{button:0,buttons:1});t.pointer('pointerup',{button:0,buttons:0});assert.equal(t.run('plotSelections'),1);
  t.pointer('pointerdown',{button:0,buttons:1});for(const x of [101,102,103])t.pointer('pointermove',{button:0,buttons:1,clientX:x});
  t.pointer('pointerup',{button:0,buttons:0,clientX:103});assert.equal(t.run('plotSelections'),1);
});
test('depth labels require an explicit reference and preserve point coordinates',()=>{
  const t=fresh();t.run("pts=[{no:'7',x:'55',y:'85',z:'97,25',d:'test'}];const originalPoints=JSON.stringify(pts);$('labelMode').value='depth';$('depthReference').value='100';updatePointInfoSettings()");
  assert.equal(t.run('pointLabel({p:pts[0],i:0})'),'Гл. 2,750 м');assert.equal(t.run("$('depthSettings').hidden"),false);
  t.run("$('labelMode').value='no_depth'");assert.equal(t.run('pointLabel({p:pts[0],i:0})'),'7 · Гл. 2,750 м');
  t.run("$('depthReference').value='90,5'");assert.equal(t.run('pointLabel({p:pts[0],i:0})'),'7 · Гл. -6,750 м');
  for(const level of ['', 'bad']){t.run(`$('depthReference').value=${JSON.stringify(level)}`);assert.equal(t.run('pointLabel({p:pts[0],i:0})'),'7 · Гл. —')}
  t.run("$('depthReference').value='100'");for(const z of ['',null,'bad'])assert.equal(t.run(`pointLabel({p:{no:'7',z:${JSON.stringify(z)}},i:0})`),'7 · Гл. —');
  t.run("$('labelMode').value='height';updatePointInfoSettings()");assert.equal(t.run('pointLabel({p:pts[0],i:0})'),'Z: 97,250 м');assert.equal(t.run("$('depthSettings').hidden"),true);
  assert.equal(t.run('JSON.stringify(pts)'),t.run('originalPoints'));
});
test('depth reference persists per project and after horizontal conversion with undo',()=>{
  const t=fresh();t.run("$('src').value='wgs';$('dst').value='sk426';pts=[{no:'1',x:'55',y:'85',z:'100'}];$('labelMode').value='depth';$('depthReference').value='125.4';const saved=JSON.parse(JSON.stringify(projectPayload()));$('depthReference').value='0';applyProjectPayload(saved)");
  assert.equal(t.run("$('depthReference').value"),'125.4');assert.equal(t.run("$('labelMode').value"),'depth');
  t.run('applyTransform(false)');assert.equal(t.run("$('depthReference').value"),'125.4');t.run('restore()');assert.equal(t.run("$('depthReference').value"),'125.4');
  t.run('delete saved.view;applyProjectPayload(saved)');assert.equal(t.run("$('depthReference').value"),'');
  t.run("$('depthReference').value='150';createProject()");assert.equal(t.run("$('depthReference').value"),'');
});
test('catalogue counts and unique definitions',()=>{assert.equal(q.run('regionalCatalogues.msk.length'),262);assert.equal(q.run('regionalCatalogues.sk63.length'),103)});
test('catalogue update rejects invalid records atomically',()=>{
  const t=fresh(),before=t.run('JSON.stringify(regionalCatalogues)');
  assert.throws(()=>t.run(`installRegionalCatalogues(${JSON.stringify(msk)},${JSON.stringify(sk63)},{msk:[{name:'Broken',code:999,zone:1,proj:'+proj=merc'}],sk63:[]})`));
  assert.equal(t.run('JSON.stringify(regionalCatalogues)'),before);
});
test('regional update merges preserve existing project identifiers',()=>{
  const t=fresh();for(const id of ['163||1','164||1','165||1','166||1','167||1','168||1','169||1','170||1','krasn||1'])assert(t.run(`regionalCatalogues.msk.some(r=>r.id===${JSON.stringify(id)})`));
  assert(t.run("regionalCatalogues.msk.some(r=>r.id==='50||1')"));
  assert.equal(t.run("regionalCatalogues.msk.filter(r=>r.id==='164||1').length"),1);
});
test('original embedded controls',()=>assert.match(q.run("$('selftest').innerHTML"),/пройдена/));
test('independent source and destination settings',()=>{
  q.run("$('src').value='msk';$('dst').value='msk';regionalSelection.src.msk='01';regionalSelection.src.msk=regionalCatalogues.msk[0].id;regionalSelection.dst.msk=regionalCatalogues.msk[1].id;refreshUI()");
  assert.notEqual(q.run("crsParams('msk','src').id"),q.run("crsParams('msk','dst').id"));
  assert.equal(q.run("sameSystem('msk','msk')"),false);
  const error=q.run("(()=>{const w=[44,40,50],p=fromWgs(w,'msk',true,'src'),r=transformPoint(p,'msk','msk',true,true),ref=fromWgs(w,'msk',true,'dst');return Math.hypot(...r.map((x,i)=>x-ref[i]))})()");assert(error<0.001);
});
test('project, swap and undo preserve regional definitions',()=>{
  const t=fresh();t.run("$('src').value='wgs';$('dst').value='sk63';regionalSelection.dst.sk63='SK63W6_2';pts=[{no:'1',x:'65',y:'81',z:'100',d:''}];const before=projectPayload();applyTransform(false)");
  assert.equal(t.run("$('src').value"),'sk63');assert.equal(t.run('regionalSelection.src.sk63'),'SK63W6_2');
  t.run('const convertedPayload=projectPayload();applyProjectPayload(convertedPayload)');assert.equal(t.run('regionalSelection.src.sk63'),'SK63W6_2');
  t.run("applyProjectPayload(before);applyTransform(false);restore()");assert.equal(t.run("$('src').value"),'wgs');assert.equal(t.run('regionalSelection.dst.sk63'),'SK63W6_2');assert.equal(t.run('pts[0].x'),'65');
});
test('missing definition cannot silently switch zones',()=>{
  const t=fresh();t.run("$('src').value='msk';regionalSelection.src.msk='missing-definition';refreshUI()");
  assert.match(t.run("$('systemInfo').textContent"),/отсутствует/);assert.throws(()=>t.run("toWgs([100,100,0],'msk',true)"));
});
test('imported regional project requires a saved definition',()=>{
  const t=fresh();t.run("applyProjectPayload({source:'msk',target:'wgs',points:[{no:'1',x:'100',y:'100',z:'0'}]})");
  assert.match(t.run("$('systemInfo').textContent"),/отсутствует/);assert.throws(()=>t.run("toWgs([100,100,0],'msk',true)"));
});
test('region and zone events update the selected definition',()=>{
  const t=fresh();t.run("$('src').value='sk63';refreshUI();$('srcRegion').value='SK63W3'");t.event('srcRegion');assert.equal(t.run('regionalSelection.src.sk63'),'SK63W3_1');
  t.run("$('srcRegionalZone').value='SK63W3_4'");t.event('srcRegionalZone');assert.equal(t.run("crsParams('sk63','src').id"),'SK63W3_4');
});
test('search finds a region without changing the active definition',()=>{
  const t=fresh();t.run("$('src').value='msk';refreshUI();const initial=regionalSelection.src.msk;$('srcRegionSearch').value='МСК‑50'");t.event('srcRegionSearch','input');
  assert.equal(t.run('regionalSelection.src.msk'),t.run('initial'));
  assert(t.run("$('srcRegion').options.some(o=>o.value==='50|')"));
  assert.equal(t.run("$('srcRegion').options.length"),2);
  t.run("$('srcRegionSearch').value='такого региона нет'");t.event('srcRegionSearch','input');
  assert.equal(t.run('regionalSelection.src.msk'),t.run('initial'));assert(t.run("$('srcRegion').options.some(o=>o.text==='Совпадений нет')"));
  t.run('resetRegionSearch();refreshUI()');assert(t.run("$('srcRegion').options.length")>80);
});
test('regional false offsets ignore generic prefix controls',()=>{
  const t=fresh();assert(t.run("(()=>{const a=fromWgs([55,38,100],'msk',true),b=fromWgs([55,38,100],'msk',false);return a.every((x,i)=>x===b[i])})()"));
});
test('PZ-90 family inverse and forward',()=>{const t=fresh();for(const id of ['pz90geo','pz02geo']){
  const r=t.run(`(()=>{const p=[65,84,100],out=transformPoint(p,'wgs','${id}',true,true);return transformPoint(out,'${id}','wgs',true,true)})()`);
  assert(Math.abs(r[0]-65)<1e-9);assert(Math.abs(r[1]-84)<1e-9);assert(Math.abs(r[2]-100)<1e-6);
}});
test('PZ-90 to PZ-90.11 against EPSG operations in PROJ',()=>{const t=fresh();for(const [id,expected]of [
  ['pz90geo',[65.00000157496666,84.00006998698818,98.75300016254187]],
  ['pz02geo',[65.00000031512566,84.00001138568858,100.19409893453121]]
]){const out=t.run(`transformPoint([65,84,100],'${id}','pzgeo',true,true)`);assert(Math.abs(out[0]-expected[0])<1e-10);assert(Math.abs(out[1]-expected[1])<1e-10);assert(Math.abs(out[2]-expected[2])<1e-6)}});
test('automatic zone uses the target datum at a six-degree boundary',()=>{
  for(const id of ['sk426','sk956']){
    const t=fresh();t.run(`$('src').value='wgs';$('dst').value='${id}';pts=[{no:'1',x:'45',y:'132',z:'0'}];refreshUI()`);
    assert.match(t.run("$('zoneInfo').textContent"),/зона 22/);
    assert.equal(Math.floor(t.run(`fromWgs([45,132,0],'${id}',true)[1]`)/1e6),22);
    t.run("$('dstZonePrefix').checked=false;applyTransform(false)");
    assert.equal(t.run("$('zoneManual').value"),'22');assert.equal(t.run("$('autoZone').checked"),false);
  }
});
test('all available regional GeoProj choices against public controls',()=>{
  const t=fresh();let count=0,maxError=0;
  const audit=JSON.parse(fs.readFileSync(path.join(__dirname,'geoproj-audit.json'),'utf8'));
  assert.deepEqual(audit.summary,{match:360,difference:0,missing:0,unavailable:2});
  assert.deepEqual(audit.cases.filter(c=>c.status==='unavailable').map(c=>c.key),['msk-09-2','msk-09-3']);
  const holdouts=JSON.parse(fs.readFileSync(path.join(__dirname,'geoproj-holdout.json'),'utf8'));
  assert.equal(holdouts.summary.match,3);
  for(const c of [...audit.cases,...holdouts.cases]){
    if(c.status==='unavailable')continue;
    const id=c.family==='msk'?`${c.record.code}|${c.record.variant}|${c.record.zone||1}`:c.record.id;
    t.run(`regionalSelection.dst[${JSON.stringify(c.family)}]=${JSON.stringify(id)}`);
    for(const p of c.points){
      const out=t.run(`fromWgs(${JSON.stringify([...p.input,0])},${JSON.stringify(c.family)},true)`);
      const error=Math.hypot(out[0]-p.geoproj[0],out[1]-p.geoproj[1]);maxError=Math.max(maxError,error);count++;
      assert(error<0.003,`${c.key}: ${error} m`);
    }
  }
  assert.equal(count,1809);console.log(JSON.stringify({geoprojRegionalControls:count,maxError}));
});
test('geographic and Gauss-Kruger GeoProj choices against public controls',()=>{
  const t=fresh();let count=0,maxError=0;
  const controls=JSON.parse(fs.readFileSync(path.join(__dirname,'geoproj-global-controls.json'),'utf8'));
  assert.equal(controls.cases.length,10);
  for(const c of controls.cases)for(const p of c.points){
    const out=t.run(`fromWgs(${JSON.stringify([...p.input,0])},${JSON.stringify(c.id)},true)`);
    const geo=t.run(`isGeo(${JSON.stringify(c.id)})`);
    const error=Math.hypot((out[0]-p.geoproj[0])*(geo?111320:1),(out[1]-p.geoproj[1])*(geo?111320*Math.cos(p.input[0]*Math.PI/180):1));
  const fs=require('node:fs'), vm=require('node:vm'), assert=require('node:assert/strict');
const path=require('node:path');
const root=process.argv[2]||path.resolve(__dirname,'../src/KhitunGeo/wwwroot');
const html=fs.readFileSync(root+'/index.html','utf8');
const msk=JSON.parse(fs.readFileSync(root+'/crs/msk.json','utf8'));
const sk63=JSON.parse(fs.readFileSync(root+'/crs/sk63.json','utf8'));
const updates=JSON.parse(fs.readFileSync(root+'/crs/regional-updates.json','utf8'));
function fresh(includeWorkspace=false){
  const elements=new Map(),storage=new Map(),listeners=new Map();
  function element(id=''){return {id,value:'',checked:false,dataset:{},className:'',_html:'',get innerHTML(){return this._html},set innerHTML(v){this._html=v;this.options=[]},options:[],textContent:'',classList:{add(){},remove(){},toggle(){return false},contains(){return false}},add(option){this.options.push(option)},addEventListener(name,fn){listeners.set(id+':'+name,fn)},appendChild(){},setAttribute(){},querySelectorAll(){return []},querySelector(){return null},focus(){},getBoundingClientRect(){return {width:600,height:500}}}}
  const document={documentElement:element(),querySelectorAll(){return []},querySelector(){return [...elements.values()].find(e=>e.open)||null},addEventListener(){},getElementById(id){if(!elements.has(id))elements.set(id,element(id));return elements.get(id)},createElement(){return element()},body:element()};
  for(const [id,value] of Object.entries({customDatum:'sk42',customLon0:'84',customFE:'500000',customFN:'0',customK:'1',zoneManual:'28',impDecimal:'auto',expDelimiter:',',expFormat:'pnezd',expDecimals:'3',expScope:'all',projectName:'Test'}))document.getElementById(id).value=value;
  for(const id of ['autoZone','srcZonePrefix','dstZonePrefix'])document.getElementById(id).checked=true;
  const window={addEventListener(name,fn){const key='window:'+name;listeners.set(key,[...(listeners.get(key)||[]),fn])},getSelection:()=>({removeAllRanges(){}})};
  document.getElementById('focusSearchBtn').parentElement={appendChild(){}};
  const ctx=vm.createContext({document,window,fetch:()=>new Promise(()=>{}),Option:class{constructor(text,value){this.text=text;this.value=value}},localStorage:{getItem:k=>storage.get(k)??null,setItem:(k,v)=>storage.set(k,String(v)),removeItem:k=>storage.delete(k)},sessionStorage:{getItem(){return '1'},setItem(){}},setTimeout:()=>1,clearTimeout(){},requestAnimationFrame(){},confirm:()=>true,alert(){},TextEncoder,TextDecoder,Blob,URL,console});
  const run=s=>vm.runInContext(s,ctx);
  run(fs.readFileSync(root+'/coordinate-core.js','utf8'));
  run(fs.readFileSync(root+'/workspace-core.js','utf8'));
  run(html.match(/<script>([\s\S]*?)<\/script>/)[1]);
  run(`installRegionalCatalogues(${JSON.stringify(msk)},${JSON.stringify(sk63)},${JSON.stringify(updates)})`);
  if(includeWorkspace)run(fs.readFileSync(root+'/workspace-ui.js','utf8'));
  return {run, event:(id,name='change',event)=>{const handlers=listeners.get(id+':'+name);for(const fn of Array.isArray(handlers)?handlers:[handlers])fn(event)}};
}
const q=fresh(), failures=[];
function test(name,fn){try{fn();console.log('PASS '+name)}catch(e){failures.push(name);console.error('FAIL '+name+': '+e.message)}}
test('select all toggles to clear and follows partial and empty selection',()=>{
  const t=fresh();t.run("pts=[{no:'1',x:'1',y:'2',z:'0'},{no:'2',x:'3',y:'4',z:'0'}];selected.clear();render();$('selectAllBtn').onclick()");
  assert.equal(t.run('selected.size'),2);assert.equal(t.run("$('selectAllBtn').textContent"),'Снять выделение');
  t.run("$('selectAllBtn').onclick()");assert.equal(t.run('selected.size'),0);assert.equal(t.run("$('selectAllBtn').textContent"),'Выделить всё');
  t.run('selected.add(0);render()');assert.equal(t.run("$('selectAllBtn').textContent"),'Выделить всё');
  t.run('pts=[];selected.clear();render()');assert.equal(t.run("$('selectAllBtn').disabled"),true);
});
test('Ctrl+Z restores added and deleted points including Russian keyboard layout',()=>{
  const t=fresh();t.run("pts=[{no:'1',x:'55',y:'85',z:'100'}];$('addBtn').onclick();let prevented=0;const key={ctrlKey:true,code:'KeyZ',key:'я',target:{closest:()=>null},preventDefault(){prevented++}};handleUndoShortcut(key)");
  assert.equal(t.run('pts.length'),1);assert.equal(t.run('prevented'),1);
  t.run("selected=new Set([0]);$('deleteBtn').onclick();handleUndoShortcut(key)");assert.equal(t.run('pts[0].x'),'55');assert.equal(t.run('selected.has(0)'),true);
  t.run("$('addBtn').onclick();$('zoneDialog').open=true;handleUndoShortcut(key)");assert.equal(t.run('pts.length'),2);
  t.run("$('zoneDialog').open=false;handleUndoShortcut({...key,shiftKey:true})");assert.equal(t.run('pts.length'),2);
});
test('point editing is undoable as one edit and native text undo is preserved',()=>{
  const t=fresh(),input={dataset:{i:'0',k:'x'},value:'56'};
  t.run("pts=[{no:'1',x:'55.123456789',y:'85',z:'100'}];history=[]");
  t.event('tbody','input',{target:input});input.value='56.7';t.event('tbody','input',{target:input});
  assert.equal(t.run('history.length'),1);assert.equal(t.run('pts[0].x'),'56.7');
  t.run("handleUndoShortcut({ctrlKey:true,code:'KeyZ',target:{closest:()=>({})},preventDefault(){}})");assert.equal(t.run('pts[0].x'),'55.123456789');
  t.run("$('addBtn').onclick();let intercepted=false;handleUndoShortcut({ctrlKey:true,code:'KeyZ',target:{closest:s=>s==='#tbody'?null:{}},preventDefault(){intercepted=true}})");
  assert.equal(t.run('intercepted'),false);assert.equal(t.run('pts.length'),2);
});
test('zone dialog validates, cancels, commits and supports undo',()=>{
  const t=fresh();t.run("$('zoneDialog').showModal=function(){this.open=true};$('zoneDialog').close=function(){this.open=false};$('src').value='wgs';$('dst').value='sk426';openZoneSettings();$('zoneMode').value='manual';$('zoneNumber').value='22';$('zoneCancel').onclick()");
  assert.equal(t.run("$('autoZone').checked"),true);assert.equal(t.run("$('zoneManual').value"),'28');
  t.run("openZoneSettings();$('zoneMode').value='manual';$('zoneNumber').value='61';applyZoneSettings()");
  assert.equal(t.run("$('zoneDialog').open"),true);assert.match(t.run("$('zoneSettingsError').textContent"),/60/);
  t.run("$('zoneNumber').value='22';applyZoneSettings()");assert.equal(t.run("$('zoneDialog').open"),false);assert.equal(t.run("$('autoZone').checked"),false);assert.equal(t.run("$('zoneManual').value"),'22');
  t.run('restore()');assert.equal(t.run("$('autoZone').checked"),true);assert.equal(t.run("$('zoneManual').value"),'28');
  t.run("$('src').value='utm';openZoneSettings()");assert.equal(t.run("$('zoneNumber').disabled"),false);
});
test('help opens and one-click theme choice persists',()=>{
  const t=fresh();t.run("renderPlot=()=>{};$('helpDialog').showModal=function(){this.open=true};$('helpDialog').close=function(){this.open=false};$('helpBtn').onclick();$('themeToggleBtn').onclick()");
  assert.equal(t.run("$('helpDialog').open"),true);assert.match(t.run("$('systemInfo').textContent"),/Источник:/);
  assert.equal(t.run('localStorage.getItem(THEME_KEY)'),'2');assert.equal(t.run("$('themeToggleText').textContent"),'Светлая');
  t.run("$('themeToggleBtn').onclick();$('helpDone').onclick()");assert.equal(t.run('document.documentElement.dataset.theme'),'1');assert.equal(t.run("$('helpDialog').open"),false);
});
function rowHarness(){
  const t=fresh();t.run(`pts=Array.from({length:6},(_,i)=>({no:String(i+1),x:'55',y:'85',z:'10',d:''}));selected.clear();lastSelected=-1;
    let hoveredRow=0,focusedRow=-1,rowCapture=null;$('tbody').setPointerCapture=id=>rowCapture=id;$('tbody').hasPointerCapture=id=>rowCapture===id;$('tbody').releasePointerCapture=()=>rowCapture=null;
    const fakeRows=pts.map((_,i)=>{const row={dataset:{i:String(i)},children:[],classList:{toggle(){}},setAttribute(){},closest:s=>s==='tbody'?$('tbody'):null};
      const handle={dataset:{rowHandle:String(i)},closest:s=>s==='tr[data-i]'?row:s==='[data-row-handle]'?handle:null,focus(){focusedRow=i},scrollIntoView(){}};
      row.handle=handle;row.querySelector=()=>handle;return row});
    $('tbody').querySelectorAll=()=>fakeRows;$('tbody').querySelector=s=>fakeRows[Number(s.match(/"(\\d+)"/)?.[1])]?.handle;
    $('pointsTableWrap').scrollTop=0;$('pointsTableWrap').getBoundingClientRect=()=>({left:0,right:600,top:0,bottom:200});
    document.elementFromPoint=()=>fakeRows[hoveredRow].handle;`);
  t.pointer=(id,name,overrides={})=>t.event(id,name,{pointerId:1,pointerType:'mouse',button:0,buttons:1,clientX:30,clientY:100,target:t.run('fakeRows[hoveredRow].handle'),preventDefault(){},...overrides});return t;
}
test('row dragging expands and shrinks a range without replacing editable cells',()=>{
  const t=rowHarness();t.run("hoveredRow=1;$('tbody').innerHTML='unchanged editor'");t.pointer('tbody','pointerdown');
  t.run('hoveredRow=4');t.pointer('window','pointermove');assert.equal(t.run('[...selected].join()'),'1,2,3,4');
  t.run('hoveredRow=2');t.pointer('window','pointermove');assert.equal(t.run('[...selected].join()'),'1,2');
  assert.equal(t.run("$('tbody').innerHTML"),'unchanged editor');
  t.pointer('window','pointerup',{buttons:0});assert.equal(t.run('rowDrag'),null);assert.equal(t.run('rowCapture'),null);
  t.run('hoveredRow=5');t.pointer('window','pointermove');assert.equal(t.run('[...selected].join()'),'1,2');
});
test('Ctrl row selection adds and removes ranges while Shift retains its anchor',()=>{
  const t=rowHarness();t.run('beginRowSelection(1,{});beginRowSelection(4,{shiftKey:true})');assert.equal(t.run('[...selected].join()'),'1,2,3,4');
  t.run('beginRowSelection(2,{shiftKey:true})');assert.equal(t.run('[...selected].join()'),'1,2');
  t.run('hoveredRow=4');t.pointer('tbody','pointerdown',{ctrlKey:true});t.run('hoveredRow=5');t.pointer('window','pointermove');t.pointer('window','pointerup');
  assert.equal(t.run('[...selected].sort().join()'),'1,2,4,5');
  t.run('hoveredRow=1');t.pointer('tbody','pointerdown',{ctrlKey:true});t.run('hoveredRow=2');t.pointer('window','pointermove');t.pointer('window','pointerup');
  assert.equal(t.run('[...selected].sort().join()'),'4,5');
});
test('row selection scrolls only its table and cancels on release, cancellation or blur',()=>{
  const t=rowHarness();t.pointer('tbody','pointerdown');t.run('rowDrag.y=199;hoveredRow=3;scrollRowDrag(rowDrag)');
  assert.equal(t.run("$('pointsTableWrap').scrollTop"),12);assert.equal(t.run('[...selected].join()'),'0,1,2,3');
  t.pointer('window','pointermove',{buttons:0});assert.equal(t.run('rowDrag'),null);
  for(const name of ['pointercancel','blur']){t.pointer('tbody','pointerdown');t.pointer('window',name);assert.equal(t.run('rowDrag'),null)}
});
test('row handles support Shift arrows, Ctrl+A and Escape',()=>{
  const t=rowHarness();t.run('beginRowSelection(2,{})');
  const key=(key,extra={})=>t.event('tbody','keydown',{key,target:t.run('fakeRows[2].handle'),preventDefault(){},...extra});
  key('ArrowDown',{shiftKey:true});assert.equal(t.run('[...selected].join()'),'2,3');assert.equal(t.run('focusedRow'),3);
  key('End',{shiftKey:true});assert.equal(t.run('[...selected].join()'),'2,3,4,5');
  key('ф',{ctrlKey:true,code:'KeyA'});assert.equal(t.run('selected.size'),6);
  key('Escape');assert.equal(t.run('selected.size'),0);
});
function heightHarness(){const t=fresh();t.run("$('heightDialog').showModal=function(){this.open=true};$('heightDialog').close=function(){this.open=false};pts=[{no:'1',x:'55',y:'85',z:'10',d:'A'},{no:'2',x:'56',y:'86',z:'-2.5',d:'B'},{no:'3',x:'57',y:'87',z:'0',d:'C'}];history=[]");return t}
test('height addition, subtraction and sign change preserve XY and undo in one step',()=>{
  const t=heightHarness();t.run("selected=new Set([0,2]);const before=JSON.stringify(pts);openHeightSettings();$('heightAmount').value='12,5';$('heightOperation').value='subtract';applyHeightChange()");
  assert.equal(t.run('pts.map(p=>p.z).join()'),'-2.5,-2.5,-12.5');assert.equal(t.run('history.length'),1);
  assert.equal(t.run('pts.map(p=>[p.x,p.y,p.d].join()).join(";")'),'55,85,A;56,86,B;57,87,C');
  t.run('restore()');assert.equal(t.run('JSON.stringify(pts)'),t.run('before'));assert.equal(t.run('[...selected].join()'),'0,2');
  t.run("openHeightSettings();$('heightScope').value='all';$('heightOperation').value='add';$('heightAmount').value='0.25';applyHeightChange()");
  assert.equal(t.run('pts.map(p=>p.z).join()'),'10.25,-2.25,0.25');
  t.run("openHeightSettings();$('heightScope').value='all';$('heightOperation').value='negate';updateHeightDraft();applyHeightChange()");
  assert.equal(t.run("$('heightAmount').disabled"),true);assert.equal(t.run('pts.map(p=>p.z).join()'),'-10.25,2.25,-0.25');
  t.run('const saved=JSON.parse(JSON.stringify(projectPayload()));applyProjectPayload(saved)');assert.equal(t.run('pts[0].z'),'-10.25');
});
test('coordinate conversion preserves entered Z across four representative systems',()=>{
  for(const [name,src,dst] of [
    ['WGS-84 / GSK-2011','wgs','gsk6'],
    ['WGS-84 / SK-42','wgs','sk426'],
    ['WGS-84 / MSK-164','wgs','msk164'],
    ['GSK-2011 / SK-42','gskgeo','sk42geo']
  ]){
    const t=fresh(),points=[
      {no:'101',x:'65',y:'84',z:'',d:'empty'},
      {no:'205',x:'65.01',y:'84.02',z:'125,500',d:'comma'},
      {no:'310',x:'65.02',y:'84.04',z:'-7.25',d:'negative'}
    ];
    t.run(`$('src').value=${JSON.stringify(src)};$('dst').value=${JSON.stringify(dst)};pts=${JSON.stringify(points)};refreshUI();applyTransform(false);const firstZ=pts.map(p=>p.z);const firstSource=$('src').value;applyTransform(false);const secondZ=pts.map(p=>p.z)`);
    assert.equal(t.run('firstSource'),dst,`${name}: conversion must execute`);
    assert.equal(t.run("$('src').value"),src,`${name}: reverse conversion must execute`);
    assert.deepEqual(Array.from(t.run('firstZ')),points.map(p=>p.z),`${name}: first conversion`);
    assert.deepEqual(Array.from(t.run('secondZ')),points.map(p=>p.z),`${name}: round trip`);
  }
});
test('height dialog rejects invalid rows atomically and 