/* JSON boundary for the unchanged coordinate mathematics. No browser services. */
(function(root){
'use strict';
const math=typeof module==='object'&&module.exports?require('./coordinate-core.js'):root.KhitunCoordinates;
const {E,datumToWgs,wgsToDatum,webInv,webFwd,projectionFor,invTM,fwdTM,ecefToGeod,geodToEcef,helm,invHelm,zoneFromPrefixedY,zoneGK6,zoneGK3,zoneUTM}=math;
function definition(value){
 if(!value||typeof value!=='object'||Array.isArray(value)||!['geo','webmerc','gk6','gk3','utm','fixedtm','custom'].includes(value.kind))throw Error('Не задана разрешённая СК');
 if(!Object.hasOwn(E,value.datum)||value.datum==='bessel')throw Error('Неизвестный датум');
 const c={kind:value.kind,datum:value.datum};
 if(['utm','webmerc'].includes(c.kind)&&c.datum!=='wgs')throw Error('Для UTM/Web Mercator необходим WGS-84');
 if(['fixedtm','custom'].includes(c.kind)){
  for(const name of ['lon0','fe','fn','k']){if(!Number.isFinite(value[name]))throw Error('Некорректный параметр СК: '+name);c[name]=value[name];}
  if(c.k<=0)throw Error('Масштаб должен быть больше нуля');
  if(Math.abs(c.lon0)>360)throw Error('Долгота осевого меридиана вне диапазона');
  c.lat0=value.lat0??0;if(!Number.isFinite(c.lat0)||Math.abs(c.lat0)>=90)throw Error('Некорректная широта начала проекции');
  if(c.kind==='custom'&&c.lat0!==0)throw Error('Пользовательская проекция не поддерживает ненулевой lat0');
  if(value.ellipsoid!==undefined){if(!['bessel','krass'].includes(value.ellipsoid))throw Error('Неизвестный эллипсоид');if(value.ellipsoid==='krass'&&!['sk42','sk95'].includes(c.datum))throw Error('Эллипсоид krass несовместим с датумом');c.ellipsoid=value.ellipsoid;}
  if(value.towgs!==undefined){if(!Array.isArray(value.towgs)||value.towgs.length!==7||!value.towgs.every(Number.isFinite))throw Error('Некорректные параметры перехода');c.towgs=[...value.towgs];}
  if(c.ellipsoid==='bessel'&&!c.towgs)throw Error('Для Bessel необходимы параметры перехода');
 }
 return c;
}
function convertBatch(request){
 if(!request||request.version!==1)throw Error('Неподдерживаемая версия запроса');
 const source=definition(request.source),target=definition(request.target),settings=request.zone;
 if(!settings||typeof settings.auto!=='boolean'||typeof settings.sourcePrefix!=='boolean'||typeof settings.targetPrefix!=='boolean')throw Error('Не заданы параметры зон');
 if(!Array.isArray(request.points))throw Error('Не задана таблица точек');
 const zoned=c=>['gk6','gk3','utm'].includes(c.kind);
 for(const c of [source,target])if(zoned(c)&&(!settings.auto||(c===source&&c.kind==='utm'))){
  const max=c.kind==='gk3'?120:60;
  if(!Number.isInteger(settings.manual)||settings.manual<1||settings.manual>max)throw Error(`Зона должна быть целым числом от 1 до ${max}`);
 }
 if(settings.auto&&['gk6','gk3'].includes(source.kind)&&!settings.sourcePrefix)throw Error('Для исходной плоской СК без префикса Y отключите авто-зону и задайте зону вручную');
 const sourceId=source.kind==='utm'?'utm':'source',targetId=target.kind==='utm'?'utm':'target';
 const targetZones=new Set();
 const crsParams=(_,side='src')=>side==='src'?source:target;
 const isGeo=id=>(id===sourceId?source:target).kind==='geo';
 const sameSystem=()=>JSON.stringify(source)===JSON.stringify(target);
 function detectZoneForPoint(c,p,isSource,prefix){
  if(!['gk6','gk3','utm'].includes(c.kind))return null;
  let z;
  if(!settings.auto)z=settings.manual;
  else if(isSource&&prefix&&c.kind!=='utm'){z=zoneFromPrefixedY(p[1]);if(z===null)throw Error('В Y не найден номер зоны');}
  else if(isSource){if(c.kind==='utm')z=settings.manual;else throw Error('Для исходной плоской СК без префикса Y отключите авто-зону и задайте зону вручную');}
  else z=c.kind==='gk6'?zoneGK6(p[1]):c.kind==='gk3'?zoneGK3(p[1]):zoneUTM(p[1]);
  const max=c.kind==='gk3'?120:60;
  if(!Number.isInteger(z)||z<1||z>max)throw Error(`Зона должна быть целым числом от 1 до ${max}`);
  if(!isSource)targetZones.add(z);
  return z;
 }
function toWgs(p,id,prefix,side='src'){const c=crsParams(id,side);if(c.kind==='geo')return datumToWgs(p,c.datum);if(c.kind==='webmerc')return webInv(p[0],p[1],p[2]);if(['gk6','gk3','utm'].includes(c.kind)){const z=detectZoneForPoint(c,p,true,prefix),pr=projectionFor(c,z,prefix),g=invTM(p[0],p[1],p[2],E[c.datum],pr.lon0,0,pr.k,pr.fe,pr.fn);return datumToWgs(g,c.datum)}if(c.kind==='fixedtm'||c.kind==='custom'){const pr=projectionFor(c,null,prefix),ell=c.ellipsoid==='bessel'?E.bessel:E[c.datum],g=invTM(p[0],p[1],p[2],ell,pr.lon0,pr.lat0||0,pr.k,pr.fe,pr.fn);if(c.towgs)return ecefToGeod(...helm(geodToEcef(...g,ell),c.towgs),E.wgs);return datumToWgs(g,c.datum)}throw Error('СК не поддерживается')}
function fromWgs(w,id,prefix,side='dst'){const c=crsParams(id,side);if(c.kind==='geo')return wgsToDatum(w,c.datum);if(c.kind==='webmerc')return webFwd(w[0],w[1],w[2]);if(['gk6','gk3','utm'].includes(c.kind)){const g=wgsToDatum(w,c.datum),z=detectZoneForPoint(c,g,false,prefix),pr=projectionFor(c,z,prefix);return fwdTM(g[0],g[1],g[2],E[c.datum],pr.lon0,0,pr.k,pr.fe,pr.fn)}if(c.kind==='fixedtm'||c.kind==='custom'){const pr=projectionFor(c,null,prefix),ell=c.ellipsoid==='bessel'?E.bessel:E[c.datum],g=c.towgs?ecefToGeod(...invHelm(geodToEcef(...w,E.wgs),c.towgs),ell):wgsToDatum(w,c.datum);return fwdTM(...g,ell,pr.lon0,pr.lat0||0,pr.k,pr.fe,pr.fn)}throw Error('СК не поддерживается')}
function transformPoint(p,s,t,srcPrefix,dstPrefix){if(!Array.isArray(p)||p.length!==3||!p.every(Number.isFinite))throw new Error('Координаты должны быть конечными числами');if(isGeo(s)&&(Math.abs(p[0])>=90||Math.abs(p[1])>180))throw new Error('Допустимая широта: от −90 до 90 без полюсов; долгота: от −180 до 180');if(s==='utm'&&p[0]<0)throw new Error('UTM North не поддерживает южное полушарие');if(sameSystem(s,t)&&srcPrefix===dstPrefix)return [...p];const w=toWgs(p,s,srcPrefix);if(t==='utm'&&(w[0]<0||w[0]>84))throw new Error('UTM North поддерживает широты от 0 до 84°');return fromWgs(w,t,dstPrefix)}
 const points=request.points.map((point,index)=>{
  try{
   if(!point||typeof point.name!=='string'||typeof point.description!=='string'||!Number.isFinite(point.x)||!Number.isFinite(point.y)||(point.height!==null&&!Number.isFinite(point.height)))throw Error('Некорректные координаты или данные точки');
   if(zoned(source))detectZoneForPoint(source,[point.x,point.y,point.height??0],true,settings.sourcePrefix);
   const result=transformPoint([point.x,point.y,point.height??0],sourceId,targetId,settings.sourcePrefix,settings.targetPrefix);
   if(!result.every(Number.isFinite))throw Error('Некорректный результат преобразования');
   return {...point,x:result[0],y:result[1]};
  }catch(error){throw Error(`Строка ${index+1}: ${error.message}`);}
 });
 return {version:1,points,targetZones:[...targetZones].sort((a,b)=>a-b)};
}
function convertJson(json){return JSON.stringify(convertBatch(JSON.parse(json)));}
const api=Object.freeze({convertBatch,convertJson});
if(typeof module==='object'&&module.exports)module.exports=api;else root.KhitunCoordinateAdapter=api;
})(typeof globalThis!=='undefined'?globalThis:this);
