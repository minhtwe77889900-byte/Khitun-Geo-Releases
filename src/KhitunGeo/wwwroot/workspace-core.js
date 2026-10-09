/* Independent, non-mutating tools for the next Khitun Geo update. */
(function(root){
 'use strict';
 const number=v=>{const s=String(v??'').trim().replace(/\s+/g,'').replace(/−/g,'-').replace(',','.');return s===''?NaN:Number(s);};
 const escape=v=>String(v??'').replace(/[&<>"']/g,c=>({'&':'&amp;','<':'&lt;','>':'&gt;','"':'&quot;',"'":'&#39;'}[c]));
 function quality(points,geographic=false){
  const issues=[],ids=new Map(),coordinates=new Map();
  points.forEach((p,i)=>{
   const x=number(p.x),y=number(p.y),z=number(p.z),id=String(p.no??'').trim();
   if(!String(p.x??'').trim()&&!String(p.y??'').trim())return;
   const add=(severity,message)=>issues.push({row:i,severity,message});
   if(!Number.isFinite(x)||!Number.isFinite(y))add('error','Некорректные или неполные X/Y');
   if(String(p.z??'').trim()==='')add('warning','Z не задана; в горизонтальном расчёте используется 0');
   else if(!Number.isFinite(z))add('error','Некорректная высота Z');
   if(geographic&&Number.isFinite(x)&&Number.isFinite(y)&&(Math.abs(x)>90||Math.abs(y)>180))add('error','Широта/долгота вне допустимого диапазона');
   if(!id)add('warning','Нет номера точки');
   else if(ids.has(id))add('warning',`Повтор номера: строка ${ids.get(id)+1}`);else ids.set(id,i);
   if(Number.isFinite(x)&&Number.isFinite(y)){const key=JSON.stringify([x,y]);if(coordinates.has(key))add('warning',`Совпадающие X/Y: строка ${coordinates.get(key)+1}`);else coordinates.set(key,i);}
  });
  return issues;
 }
 function parseDxf(text){
  if(text.startsWith('AutoCAD Binary DXF'))throw Error('Нужен текстовый ASCII DXF, не бинарный.');
  const lines=text.replace(/^\uFEFF/,'').split(/\r?\n/);while(lines.length&&lines.at(-1).trim()==='')lines.pop();
  if(lines.length%2)throw Error('Повреждён DXF: неполная пара код/значение.');
  const records=[],blockRecords=[],layerRecords=[];let record=null,section='';
  const flush=()=>{if(record){if(section==='ENTITIES')records.push(record);else if(section==='BLOCKS')blockRecords.push(record);else if(section==='TABLES'&&record.type==='LAYER')layerRecords.push(record);}};
  for(let i=0;i<lines.length;i+=2){const code=Number(lines[i].trim()),value=lines[i+1].trim();if(!Number.isInteger(code))throw Error('Некорректный код DXF.');
   if(code===0){flush();record={type:value,pairs:[]};if(value==='ENDSEC')section='';}
   else if(record){record.pairs.push([code,value]);if(record.type==='SECTION'&&code===2){section=value;record=null;}}
  }
  flush();
  if(!records.length)throw Error('В DXF не найдена секция ENTITIES с объектами.');
  const paths=[],skipped={},layers=new Set(),hidden=new Set(),blocks=new Map();let total=0,visits=0;
  const skip=t=>skipped[t]=(skipped[t]||0)+1;
  const field=(r,c,fallback)=>{const v=r.pairs.find(p=>p[0]===c);return v?number(v[1]):fallback;};
  const string=(r,c,fallback='')=>(r.pairs.find(p=>p[0]===c)?.[1]??fallback).replace(/\\U\+([0-9a-f]{4})/gi,(_,h)=>String.fromCharCode(parseInt(h,16)));
  for(const r of layerRecords){const name=string(r,2,'0');layers.add(name);if(field(r,62,7)<0||(field(r,70,0)&1))hidden.add(name);}
  let block=null;for(const r of blockRecords){if(r.type==='BLOCK'){block={record:r,records:[]};blocks.set(string(r,2),block);}else if(r.type==='ENDBLK')block=null;else if(block)block.records.push(r);}
  const identity=[1,0,0,1,0,0],point=(m,p)=>[m[0]*p[0]+m[2]*p[1]+m[4],m[1]*p[0]+m[3]*p[1]+m[5]],vector=(m,p)=>[m[0]*p[0]+m[2]*p[1],m[1]*p[0]+m[3]*p[1]];
  const compose=(a,b)=>[a[0]*b[0]+a[2]*b[1],a[1]*b[0]+a[3]*b[1],a[0]*b[2]+a[2]*b[3],a[1]*b[2]+a[3]*b[3],...point(a,[b[4],b[5]])];
  const add=(layer,parents,m,vertices,closed=false,curve=null)=>{
   vertices=vertices.map(p=>point(m,p));
   if(vertices.some(p=>!p.every(Number.isFinite)))throw Error('DXF содержит некорректные координаты.');
   if(curve){curve={...curve,c:point(m,curve.c),u:vector(m,curve.u),v:vector(m,curve.v)};if(![...curve.c,...curve.u,...curve.v,curve.start,curve.sweep].every(Number.isFinite))throw Error('DXF содержит некорректные координаты дуги.');}
   total+=vertices.length+(curve?4:0);if(total>1000000||paths.length>=200000)throw Error('Чертёж слишком большой: максимум 1 000 000 опорных точек / 200 000 объектов.');
   layers.add(layer);for(const p of parents)layers.add(p);paths.push({layer,vertices,closed,...(parents.length?{parents:[...new Set(parents)]}:{}),...(curve?{curve}: {})});
  };
  function polyline(r,v,bulges,layer,parents,m){
   const closed=!!(field(r,70,0)&1);
   if(!v.length)return;
   if(!v.flat().every(Number.isFinite)||!bulges.every(Number.isFinite))throw Error('DXF содержит некорректные координаты полилинии.');
   if(!bulges.some(Boolean)){add(layer,parents,m,v,closed);return;}
   if(v.length<2){skip(r.type+' (недостаточно вершин)');return;}
   let straight=[];const flushLine=()=>{if(straight.length>1)add(layer,parents,m,straight);straight=[];};
   for(let j=0;j<v.length-(closed?0:1);j++){
    const a=v[j],b=v[(j+1)%v.length],bulge=bulges[j]||0;
    if(!bulge){if(!straight.length)straight.push(a);straight.push(b);continue;}
    flushLine();const dx=b[0]-a[0],dy=b[1]-a[1],length=Math.hypot(dx,dy);
    if(!length){skip(r.type+' (нулевая дуга)');continue;}
    const offset=(1/bulge-bulge)/4,c=[(a[0]+b[0])/2-dy*offset,(a[1]+b[1])/2+dx*offset],radius=length*(Math.abs(bulge)+1/Math.abs(bulge))/4;
    add(layer,parents,m,[],false,{c,u:[radius,0],v:[0,radius],start:Math.atan2(a[1]-c[1],a[0]-c[0]),sweep:4*Math.atan(bulge)});
   }flushLine();
  }
  function walk(items,m=identity,inherited='0',parents=[],stack=[]){for(let k=0;k<items.length;k++){
   if(++visits>1000000)throw Error('Слишком много вложенных объектов DXF.');
   const r=items[k];if(['SECTION','ENDSEC','EOF','SEQEND','ATTRIB','ATTDEF'].includes(r.type))continue;
   if(field(r,67,0)!==0||field(r,210,0)!==0||field(r,220,0)!==0||field(r,230,1)!==1){skip(r.type+' (не XY)');continue;}
   if(field(r,60,0)===1)continue;
   const own=string(r,8,'0'),layer=own==='0'?inherited:own;
   if(r.type==='INSERT'){
    const name=string(r,2),b=blocks.get(name);if(!b){skip('INSERT');continue;}
    if(stack.includes(name)||stack.length>=16){skip('INSERT (циклический/глубокий блок)');continue;}
    if(field(b.record,70,0)&(4|8)){skip('INSERT (внешняя ссылка)');continue;}
    const x=field(r,10,0),y=field(r,20,0),sx=field(r,41,1),sy=field(r,42,1),angle=field(r,50,0)*Math.PI/180,bx=field(b.record,10,0),by=field(b.record,20,0),rows=field(r,71,1),cols=field(r,70,1),dx=field(r,44,0),dy=field(r,45,0);
    if(![x,y,sx,sy,angle,bx,by,dx,dy].every(Number.isFinite)||!sx||!sy||!Number.isInteger(rows)||!Number.isInteger(cols)||rows<1||cols<1||rows*cols>10000)throw Error('Некорректная вставка блока DXF.');
    const c=Math.cos(angle),s=Math.sin(angle),a=c*sx,bv=s*sx,cc=-s*sy,d=c*sy;
    for(let row=0;row<rows;row++)for(let col=0;col<cols;col++)walk(b.records,compose(m,[a,bv,cc,d,x-a*bx-cc*by+c*col*dx-s*row*dy,y-bv*bx-d*by+s*col*dx+c*row*dy]),layer,[...parents,layer],[...stack,name]);
   }
   else if(r.type==='LINE')add(layer,parents,m,[[field(r,10),field(r,20)],[field(r,11),field(r,21)]]);
   else if(r.type==='POINT')add(layer,parents,m,[[field(r,10),field(r,20)]]);
   else if(r.type==='CIRCLE'||r.type==='ARC'){
    const cx=field(r,10),cy=field(r,20),radius=field(r,40);if(!(radius>0))throw Error('Некорректный радиус DXF.');
    const startDegrees=r.type==='ARC'?field(r,50):0,endDegrees=r.type==='ARC'?field(r,51):360;
    if(!Number.isFinite(startDegrees)||!Number.isFinite(endDegrees))throw Error('Некорректные углы дуги DXF.');
    const start=((startDegrees%360)+360)%360*Math.PI/180;let end=((endDegrees%360)+360)%360*Math.PI/180;if(end<=start)end+=Math.PI*2;
    add(layer,parents,m,[],r.type==='CIRCLE',{c:[cx,cy],u:[radius,0],v:[0,radius],start,sweep:end-start});
   }else if(r.type==='LWPOLYLINE'){
    const v=[],bulges=[];for(const [code,value]of r.pairs){if(code===10){v.push([number(value),NaN]);bulges.push(0);}if(code===20&&v.length)v.at(-1)[1]=number(value);if(code===42&&v.length)bulges[bulges.length-1]=number(value);}polyline(r,v,bulges,layer,parents,m);
   }else if(r.type==='POLYLINE'){
    const v=[],bulges=[];const unsupported=!!(field(r,70,0)&(2|4|8|16|64));while(items[k+1]?.type==='VERTEX'){const q=items[++k];v.push([field(q,10),field(q,20)]);bulges.push(field(q,42,0));}if(unsupported)skip('POLYLINE (3D/кривые/сетка)');else polyline(r,v,bulges,layer,parents,m);
   }else skip(r.type);
  }}walk(records);
  if(!paths.length)throw Error('Нет поддерживаемой 2D геометрии. Поддерживаются LINE, POINT, ARC, CIRCLE, 2D POLYLINE и блоки.');
  return {paths,layers:[...layers],hidden:[...hidden],skipped,total};
 }
 // Curves retain analytic geometry. Tessellation is only for screen display.
 function drawingVertices(path,pixelsPerUnit=1){
  if(!path.curve)return path.vertices;const q=path.curve,r=Math.hypot(...q.u)+Math.hypot(...q.v),error=.35/Math.max(Math.abs(pixelsPerUnit),1e-12);
  const step=2*Math.acos(Math.max(-1,Math.min(1,1-error/Math.max(r,1e-20)))),count=Math.min(8192,Math.max(8,Math.ceil(Math.abs(q.sweep)/Math.max(step,1e-6))));
  return Array.from({length:count+1},(_,i)=>{const a=q.start+q.sweep*i/count;return [q.c[0]+q.u[0]*Math.cos(a)+q.v[0]*Math.sin(a),q.c[1]+q.u[1]*Math.cos(a)+q.v[1]*Math.sin(a)];});
 }
 function drawingBounds(path){
  let vertices=path.vertices;if(path.curve){const q=path.curve,angles=[q.start,q.start+q.sweep],tau=2*Math.PI;for(let axis=0;axis<2;axis++){const a=Math.atan2(q.v[axis],q.u[axis]);for(const t of [a,a+Math.PI]){const distance=((q.sweep>=0?t-q.start:q.start-t)%tau+tau)%tau;if(distance<=Math.abs(q.sweep)+1e-12)angles.push(t);}}vertices=angles.map(a=>[q.c[0]+q.u[0]*Math.cos(a)+q.v[0]*Math.sin(a),q.c[1]+q.u[1]*Math.cos(a)+q.v[1]*Math.sin(a)]);}
  const b=[Infinity,Infinity,-Infinity,-Infinity];for(const [x,y]of vertices){b[0]=Math.min(b[0],x);b[1]=Math.min(b[1],y);b[2]=Math.max(b[2],x);b[3]=Math.max(b[3],y);}return b;
 }
 function passportHtml(p){
  const rows=Object.entries(p).filter(([k])=>k!=='quality').map(([k,v])=>`<tr><th>${escape(k)}</th><td>${escape(typeof v==='object'?JSON.stringify(v):v)}</td></tr>`).join('');
  return `<!doctype html><html lang="ru"><meta charset="utf-8"><meta http-equiv="Content-Security-Policy" content="default-src 'none'; style-src 'unsafe-inline'"><title>Паспорт преобразования</title><style>body{font:14px Arial;margin:32px;color:#17242c}table{border-collapse:collapse;width:100%}th,td{border:1px solid #bbb;padding:8px;text-align:left;overflow-wrap:anywhere}th{width:30%}@page{size:A4;margin:15mm}@media print{body{margin:0}}</style><h1>Khitun Geo — паспорт преобразования</h1><p>Запись о выполненном расчёте. Не является подтверждением геодезической точности или сертификатом.</p><table>${rows}</table><h2>Контроль исходных данных</h2><p>${escape(p.quality?.length?JSON.stringify(p.quality):'Замечаний базовой проверки нет')}</p></html>`;
 }
 function validateExtras(value){
  // Do not clone a large drawing through JSON here: during DWG import that briefly
  // triples memory use (source object, JSON string and parsed copy). Validation is
  // structural; project files are parsed separately before this point.
  const v=value||{};if(typeof v!=='object'||Array.isArray(v))throw Error('Некорректные дополнительные данные проекта.');
  if(v.drawing){const d=v.drawing;if(!Array.isArray(d.paths)||d.paths.length>200000||typeof d.frame!=='string'||d.frame.length>4096||!Number.isFinite(d.scale)||d.scale<=0||!Number.isFinite(d.opacity)||d.opacity<0||d.opacity>1)throw Error('Некорректный слой DXF в проекте.');let count=0;const pair=q=>Array.isArray(q)&&q.length===2&&q.every(Number.isFinite);for(const p of d.paths){if(typeof p.layer!=='string'||p.layer.length>500||!Array.isArray(p.vertices)||p.parents!==undefined&&(!Array.isArray(p.parents)||p.parents.length>16||!p.parents.every(x=>typeof x==='string'&&x.length<=500)))throw Error('Некорректная геометрия DXF.');count+=p.vertices.length;if(!p.vertices.every(pair))throw Error('Некорректные координаты DXF.');if(p.curve){const q=p.curve;if(p.vertices.length||![q.c,q.u,q.v].every(pair)||!Number.isFinite(q.start)||!Number.isFinite(q.sweep)||Math.abs(q.sweep)>Math.PI*2+1e-10)throw Error('Некорректная дуга DXF.');count+=4;}}if(count>1000000)throw Error('Слишком много вершин DXF.');if(!Array.isArray(d.hidden)||!d.hidden.every(x=>typeof x==='string'&&x.length<=500))throw Error('Некорректные слои DXF.');}
  if(v.profiles&&(!Array.isArray(v.profiles)||v.profiles.length>30||v.profiles.some(p=>typeof p.name!=='string'||p.name.length>80||!p.settings||typeof p.settings!=='object')))throw Error('Некорректные профили объекта.');
  if(v.passport&&(typeof v.passport!=='object'||Array.isArray(v.passport)))throw Error('Некорректный паспорт.');return v;
 }
 function sha256(text){
  const bytes=new TextEncoder().encode(text),length=bytes.length,padded=new Uint8Array(Math.ceil((length+9)/64)*64);padded.set(bytes);padded[length]=128;
  const view=new DataView(padded.buffer);view.setUint32(padded.length-8,Math.floor(length/0x20000000));view.setUint32(padded.length-4,(length*8)>>>0);
  const primes=[];for(let n=2;primes.length<64;n++){if(primes.every(p=>n%p))primes.push(n);}
  const h=primes.slice(0,8).map(p=>(Math.sqrt(p)%1*0x100000000)>>>0),k=primes.map(p=>(Math.cbrt(p)%1*0x100000000)>>>0),w=new Uint32Array(64),rot=(n,b)=>(n>>>b)|(n<<(32-b));
  for(let off=0;off<padded.length;off+=64){for(let j=0;j<16;j++)w[j]=view.getUint32(off+j*4);for(let j=16;j<64;j++){const a=w[j-15],b=w[j-2];w[j]=(w[j-16]+(rot(a,7)^rot(a,18)^(a>>>3))+w[j-7]+(rot(b,17)^rot(b,19)^(b>>>10)))>>>0;}
   let [a,b,c,d,e,f,g,q]=h;for(let j=0;j<64;j++){const t1=(q+(rot(e,6)^rot(e,11)^rot(e,25))+((e&f)^(~e&g))+k[j]+w[j])>>>0,t2=((rot(a,2)^rot(a,13)^rot(a,22))+((a&b)^(a&c)^(b&c)))>>>0;q=g;g=f;f=e;e=(d+t1)>>>0;d=c;c=b;b=a;a=(t1+t2)>>>0;}[a,b,c,d,e,f,g,q].forEach((v,j)=>h[j]=(h[j]+v)>>>0);
  }return h.map(v=>v.toString(16).padStart(8,'0')).join('');
 }
 function parseDelimited(text,delimiter){
  const rows=[];let row=[],cell='',quoted=false,closed=false;
  for(let i=0;i<text.length;i++){const c=text[i];if(quoted){if(c==='"'){if(text[i+1]==='"'){cell+='"';i++;}else{quoted=false;closed=true;}}else cell+=c;continue;}
   if(c==='"'){if(cell.trim()||closed)throw Error('Неверные кавычки в таблице.');quoted=true;cell='';}
   else if(c===delimiter){row.push(cell.trim());cell='';closed=false;}
   else if(c==='\r'||c==='\n'){if(c==='\r'&&text[i+1]==='\n')i++;row.push(cell.trim());if(row.some(Boolean))rows.push(row);row=[];cell='';closed=false;}
   else{if(closed&&c.trim())throw Error('Текст после закрывающей кавычки.');if(!closed)cell+=c;}
  }if(quoted)throw Error('Незакрытая кавычка в таблице.');row.push(cell.trim());if(row.some(Boolean))rows.push(row);return rows;
 }
 function readPoints(text,format='header',delimiter=';'){
  text=text.replace(/^\uFEFF/,'');let points,source=null,metadata=null;
  if(/^\s*[\[{]/.test(text)){const o=JSON.parse(text),a=Array.isArray(o)?o:o.points;if(!Array.isArray(a))throw Error('JSON должен содержать массив points.');points=a.map(p=>{if(!p||Array.isArray(p)||typeof p!=='object')throw Error('Ожидаются точки с полями no, x, y, z.');return {no:String(p.no??''),x:String(p.x??''),y:String(p.y??''),z:String(p.z??''),d:String(p.d??'')};});source=Array.isArray(o)?null:o.source??null;metadata=Array.isArray(o)?null:o;
  }else{
   if(![';',',','\t'].includes(delimiter))throw Error('Выберите разделитель.');const rows=parseDelimited(text,delimiter);if(!rows.length)throw Error('Файл пуст.');let map;
   if(format==='header'){
    const names=rows.shift().map(x=>x.toLowerCase().trim()),find=rx=>names.findIndex(n=>rx.test(n));
    map={no:find(/^(no|p|номер|№|номер точки|id)$/),x:find(/^(n|northing|север|x|b|lat|широта)$/),y:find(/^(e|easting|восток|y|l|lon|долгота)$/),z:find(/^(z|h|elevation|высота|отметка)$/),d:find(/^(d|description|описание|код)$/)};
    if(map.no<0||map.x<0||map.y<0)throw Error('Нужны заголовки no,N,E (или no,X,Y). X=север, Y=восток.');
   }else if(format==='pnezd')map={no:0,x:1,y:2,z:3,d:4};else if(format==='penzd')map={no:0,x:2,y:1,z:3,d:4};else throw Error('Неизвестный формат.');
   points=rows.map(r=>Object.fromEntries(Object.entries(map).map(([k,i])=>[k,i<0?'':r[i]??''])));
  }
  if(!points.length)throw Error('Нет точек.');if(points.length>100000)throw Error('Максимум 100 000 точек на файл.');return {points,source,metadata};
 }
 function uniquePoints(points){const map=new Map();for(const [i,p]of points.entries()){const id=String(p.no??'').trim();if(!id)throw Error(`Строка ${i+1}: нет номера точки.`);if(map.has(id))throw Error(`Повтор номера ${id}: сопоставление неоднозначно.`);if(!Number.isFinite(number(p.x))||!Number.isFinite(number(p.y)))throw Error(`Точка ${id}: неверные X/Y.`);if(String(p.z??'').trim()&&!Number.isFinite(number(p.z)))throw Error(`Точка ${id}: неверная Z.`);map.set(id,p);}return map;}
 function compareSurveys(base,next,planTolerance=.01,heightTolerance=.01){
  if(!Number.isFinite(planTolerance)||planTolerance<0||!Number.isFinite(heightTolerance)||heightTolerance<0)throw Error('Допуски должны быть неотрицательными числами.');const a=uniquePoints(base),b=uniquePoints(next),rows=[],missing=[],added=[];
  for(const [id,p]of a){const q=b.get(id);if(!q){missing.push(id);continue;}const dx=number(q.x)-number(p.x),dy=number(q.y)-number(p.y),dz=Number.isFinite(number(p.z))&&Number.isFinite(number(q.z))?number(q.z)-number(p.z):null,plan=Math.hypot(dx,dy);if(![dx,dy,plan,...(dz===null?[]:[dz])].every(Number.isFinite))throw Error(`Точка ${id}: разница вне диапазона чисел.`);rows.push({no:id,dx,dy,dz,plan,exceeds:plan>planTolerance||(dz!==null&&Math.abs(dz)>heightTolerance)});}
  for(const id of b.keys())if(!a.has(id))added.push(id);return {rows,missing,added,planTolerance,heightTolerance,exceeded:rows.filter(r=>r.exceeds).length};
 }
 const csvCell=v=>'"'+String(v??'').replace(/"/g,'""')+'"';
 function comparisonCsv(result){const rows=[['Номер','ΔX (м)','ΔY (м)','ΔZ (м)','В плане (м)','Статус']];for(const r of result.rows)rows.push([r.no,r.dx,r.dy,r.dz,r.plan,r.exceeds?'Превышен допуск':r.dz===null?'Нет Z для сравнения':'В допуске']);for(const id of result.missing)rows.push([id,'','','','','Нет во второй съёмке']);for(const id of result.added)rows.push([id,'','','','','Новая точка']);return '\uFEFF'+rows.map(r=>r.map(csvCell).join(';')).join('\r\n');}
 function pointCsv(points){return 'no;N;E;Z;D\r\n'+points.map(p=>[p.no,p.x,p.y,p.z,p.d].map(csvCell).join(';')).join('\r\n');}
 function zipStored(files){
  const enc=new TextEncoder(),chunks=[],central=[];let offset=0;const crc=data=>{let n=0xffffffff;for(const b of data){n^=b;for(let j=0;j<8;j++)n=(n>>>1)^((n&1)?0xedb88320:0);}return (n^0xffffffff)>>>0;};
  for(const file of files){const name=enc.encode(file.name),data=enc.encode(file.text),sum=crc(data);if(name.length>65535)throw Error('Слишком длинное имя файла.');const header=new Uint8Array(30+name.length),v=new DataView(header.buffer);v.setUint32(0,0x04034b50,true);v.setUint16(4,20,true);v.setUint16(6,0x800,true);v.setUint16(12,33,true);v.setUint32(14,sum,true);v.setUint32(18,data.length,true);v.setUint32(22,data.length,true);v.setUint16(26,name.length,true);header.set(name,30);chunks.push(header,data);
   const c=new Uint8Array(46+name.length),cv=new DataView(c.buffer);cv.setUint32(0,0x02014b50,true);cv.setUint16(4,20,true);cv.setUint16(6,20,true);cv.setUint16(8,0x800,true);cv.setUint16(14,33,true);cv.setUint32(16,sum,true);cv.setUint32(20,data.length,true);cv.setUint32(24,data.length,true);cv.setUint16(28,name.length,true);cv.setUint32(42,offset,true);c.set(name,46);central.push(c);offset+=header.length+data.length;
  }const size=central.reduce((n,c)=>n+c.length,0),end=new Uint8Array(22),ev=new DataView(end.buffer);ev.setUint32(0,0x06054b50,true);ev.setUint16(8,files.length,true);ev.setUint16(10,files.length,true);ev.setUint32(12,size,true);ev.setUint32(16,offset,true);const out=new Uint8Array(offset+size+22);let i=0;for(const chunk of [...chunks,...central,end]){out.set(chunk,i);i+=chunk.length;}return out;
 }
 const api={quality,parseDxf,drawingVertices,drawingBounds,passportHtml,escape,number,validateExtras,sha256,readPoints,compareSurveys,comparisonCsv,pointCsv,zipStored};if(typeof module!=='undefined')module.exports=api;root.KhitunWorkspace=api;
})(typeof globalThis!=='undefined'?globalThis:this);
