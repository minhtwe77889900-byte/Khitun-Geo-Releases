/* Original Khitun Geo mathematics, extracted without changing formulas or parameters. */
(function(root){
'use strict';
class Ell{constructor(a,invF){this.a=a;this.invF=invF;this.f=1/invF;this.e2=this.f*(2-this.f)}}
const E={wgs:new Ell(6378137,298.257223563),gsk2011:new Ell(6378136.5,298.2564151),pz9011:new Ell(6378136,298.257839303),pz90:new Ell(6378136,298.257839303),pz9002:new Ell(6378136,298.257839303),sk42:new Ell(6378245,298.3),sk95:new Ell(6378245,298.3),bessel:new Ell(6377397.155,299.1528128)};
// Внутренняя формула Helmert ниже использует знаки Position Vector. EPSG 5044/5043 заданы как Coordinate Frame, поэтому знаки вращений обращены.
const TO_WGS={
  // Static GSK-2011 -> WGS 84 (G1150), CF rotations converted to PV.
  // Published table: https://mapinfo.ru/node/508; checked against GeoProj controls.
  gsk2011:[0.013,-0.092,-0.03,-0.001738,0.003559,-0.004263,0.0074],
  sk42:[23.57,-140.95,-79.8,0,0.35,0.79,-0.22],
  sk95:[24.47,-130.89,-81.56,0,0,0.13,-0.22]
};
// GSK-2011 -> PZ-90.11, EPSG:7705, перевод CF -> PV; вращения из mas в arcsec.
const GSK_TO_PZ=[0,0.014,-0.008,0.000562,0.000019,-0.000053,-0.0006];
// EPSG:7704 and EPSG:7703 (reference epoch 2010 for 7703). CF -> PV arcseconds.
const PZ_TO_9011={pz90:[-1.443,0.156,0.222,0.0023,-0.00354,0.13421,-0.228],pz9002:[-0.373,0.186,0.202,0.0023,-0.00354,0.00421,-0.008]};
const d2r=d=>d*Math.PI/180,r2d=r=>r*180/Math.PI,as2r=s=>s*Math.PI/(180*3600);
function geodToEcef(lat,lon,h,e){lat=d2r(lat);lon=d2r(lon);const sl=Math.sin(lat),cl=Math.cos(lat),so=Math.sin(lon),co=Math.cos(lon),N=e.a/Math.sqrt(1-e.e2*sl*sl);return [(N+h)*cl*co,(N+h)*cl*so,(N*(1-e.e2)+h)*sl]}
function ecefToGeod(x,y,z,e){const lon=Math.atan2(y,x),p=Math.hypot(x,y);let lat=Math.atan2(z,p*(1-e.e2)),h=0;for(let i=0;i<30;i++){const s=Math.sin(lat),N=e.a/Math.sqrt(1-e.e2*s*s),c=Math.cos(lat);if(Math.abs(c)<1e-15)break;h=p/c-N;const next=Math.atan2(z,p*(1-e.e2*N/(N+h)));if(Math.abs(next-lat)<1e-14){lat=next;break}lat=next}const s=Math.sin(lat),N=e.a/Math.sqrt(1-e.e2*s*s);if(Math.abs(Math.cos(lat))>1e-15)h=p/Math.cos(lat)-N;return [r2d(lat),r2d(lon),h]}
function helm(xyz,p){const [tx,ty,tz]=p,rx=as2r(p[3]),ry=as2r(p[4]),rz=as2r(p[5]),m=1+p[6]*1e-6,[x,y,z]=xyz;return [tx+m*(x-rz*y+ry*z),ty+m*(rz*x+y-rx*z),tz+m*(-ry*x+rx*y+z)]}
function invHelm(t,p){const [tx,ty,tz]=p,rx=as2r(p[3]),ry=as2r(p[4]),rz=as2r(p[5]),m=1+p[6]*1e-6;const a11=m,a12=-m*rz,a13=m*ry,a21=m*rz,a22=m,a23=-m*rx,a31=-m*ry,a32=m*rx,a33=m,b1=t[0]-tx,b2=t[1]-ty,b3=t[2]-tz;const det=a11*(a22*a33-a23*a32)-a12*(a21*a33-a23*a31)+a13*(a21*a32-a22*a31);return [(b1*(a22*a33-a23*a32)-a12*(b2*a33-a23*b3)+a13*(b2*a32-a22*b3))/det,(a11*(b2*a33-a23*b3)-b1*(a21*a33-a23*a31)+a13*(a21*b3-b2*a31))/det,(a11*(a22*b3-b2*a32)-a12*(a21*b3-b2*a31)+b1*(a21*a32-a22*a31))/det]}
function arc(phi,e){const e2=e.e2,e4=e2*e2,e6=e4*e2;return e.a*((1-e2/4-3*e4/64-5*e6/256)*phi-(3*e2/8+3*e4/32+45*e6/1024)*Math.sin(2*phi)+(15*e4/256+45*e6/1024)*Math.sin(4*phi)-(35*e6/3072)*Math.sin(6*phi))}
function norm(v){while(v>Math.PI)v-=2*Math.PI;while(v< -Math.PI)v+=2*Math.PI;return v}
function normLon(d){while(d>180)d-=360;while(d<=-180)d+=360;return d}
function lon360(d){return d<0?d+360:d}
function fwdTM(latD,lonD,h,e,lon0D,lat0D,k0,fe,fn){const phi=d2r(latD),lam=d2r(lonD),lam0=d2r(lon0D),phi0=d2r(lat0D),e2=e.e2,ep2=e2/(1-e2),sp=Math.sin(phi),cp=Math.cos(phi),tp=Math.tan(phi),N=e.a/Math.sqrt(1-e2*sp*sp),t=tp*tp,c=ep2*cp*cp,a=cp*norm(lam-lam0),m=arc(phi,e)-arc(phi0,e),a2=a*a,a3=a2*a,a4=a2*a2,a5=a4*a,a6=a3*a3;const east=fe+k0*N*(a+(1-t+c)*a3/6+(5-18*t+t*t+72*c-58*ep2)*a5/120),north=fn+k0*(m+N*tp*(a2/2+(5-t+9*c+4*c*c)*a4/24+(61-58*t+t*t+600*c-330*ep2)*a6/720));return [north,east,h]}
function invTMApprox(north,east,h,e,lon0D,lat0D,k0,fe,fn){const e2=e.e2,ep2=e2/(1-e2),m0=arc(d2r(lat0D),e),m=m0+(north-fn)/k0,e4=e2*e2,e6=e4*e2,mu=m/(e.a*(1-e2/4-3*e4/64-5*e6/256)),e1=(1-Math.sqrt(1-e2))/(1+Math.sqrt(1-e2)),e12=e1*e1,e13=e12*e1,e14=e12*e12,phi1=mu+(3*e1/2-27*e13/32)*Math.sin(2*mu)+(21*e12/16-55*e14/32)*Math.sin(4*mu)+(151*e13/96)*Math.sin(6*mu)+(1097*e14/512)*Math.sin(8*mu),s=Math.sin(phi1),c=Math.cos(phi1),tt=Math.tan(phi1),c1=ep2*c*c,t1=tt*tt,N=e.a/Math.sqrt(1-e2*s*s),R=e.a*(1-e2)/Math.pow(1-e2*s*s,1.5),d=(east-fe)/(N*k0),d2=d*d,d3=d2*d,d4=d2*d2,d5=d4*d,d6=d3*d3,lat=phi1-(N*tt/R)*(d2/2-(5+3*t1+10*c1-4*c1*c1-9*ep2)*d4/24+(61+90*t1+298*c1+45*t1*t1-252*ep2-3*c1*c1)*d6/720),lon=d2r(lon0D)+(d-(1+2*t1+c1)*d3/6+(5-2*c1+28*t1-3*c1*c1+8*ep2+24*t1*t1)*d5/120)/c;return [r2d(lat),r2d(lon),h]}
function invTM(north,east,h,e,lon0D,lat0D,k0,fe,fn){
  let [lat,lon]=invTMApprox(north,east,h,e,lon0D,lat0D,k0,fe,fn);
  const delta=1e-6;
  for(let i=0;i<8;i++){
    const q=fwdTM(lat,lon,h,e,lon0D,lat0D,k0,fe,fn),dn=north-q[0],de=east-q[1];
    if(Math.hypot(dn,de)<1e-7)return [lat,lon,h];
    const a=fwdTM(lat+delta,lon,h,e,lon0D,lat0D,k0,fe,fn),b=fwdTM(lat,lon+delta,h,e,lon0D,lat0D,k0,fe,fn);
    const an=(a[0]-q[0])/delta,ae=(a[1]-q[1])/delta,bn=(b[0]-q[0])/delta,be=(b[1]-q[1])/delta,det=an*be-bn*ae;
    if(!Number.isFinite(det)||Math.abs(det)<1e-12)throw new Error('Обратная проекция не сходится: проверьте систему и зону');
    lat+=(dn*be-bn*de)/det;lon+=(an*de-dn*ae)/det;
  }
  const q=fwdTM(lat,lon,h,e,lon0D,lat0D,k0,fe,fn);
  if(!Number.isFinite(lat)||!Number.isFinite(lon)||Math.hypot(north-q[0],east-q[1])>1e-5)throw new Error('Обратная проекция не сходится: проверьте систему и зону');
  return [lat,lon,h];
}
function webFwd(lat,lon,h){const R=6378137,cl=Math.max(-85.05112878,Math.min(85.05112878,lat));return [R*Math.log(Math.tan(Math.PI/4+d2r(cl)/2)),R*d2r(lon),h]}
function webInv(north,east,h){const R=6378137;return [r2d(2*Math.atan(Math.exp(north/R))-Math.PI/2),r2d(east/R),h]}
function datumToWgs(p,datum){if(PZ_TO_9011[datum]){const xyz=helm(geodToEcef(...p,E[datum]),PZ_TO_9011[datum]);return datumToWgs(ecefToGeod(...xyz,E.pz9011),'pz9011')}if(datum==='wgs')return [...p];if(datum==='pz9011'){const pz=geodToEcef(...p,E.pz9011),g=invHelm(pz,GSK_TO_PZ);return ecefToGeod(...helm(g,TO_WGS.gsk2011),E.wgs)}if(TO_WGS[datum])return ecefToGeod(...helm(geodToEcef(...p,E[datum]),TO_WGS[datum]),E.wgs);throw Error('Неизвестный датум')}
function wgsToDatum(w,datum){if(PZ_TO_9011[datum]){const pz=wgsToDatum(w,'pz9011'),xyz=invHelm(geodToEcef(...pz,E.pz9011),PZ_TO_9011[datum]);return ecefToGeod(...xyz,E[datum])}if(datum==='wgs')return [...w];if(datum==='pz9011'){const g=invHelm(geodToEcef(...w,E.wgs),TO_WGS.gsk2011),pz=helm(g,GSK_TO_PZ);return ecefToGeod(...pz,E.pz9011)}if(TO_WGS[datum])return ecefToGeod(...invHelm(geodToEcef(...w,E.wgs),TO_WGS[datum]),E[datum]);throw Error('Неизвестный датум')}
function zoneGK6(lon){return Math.max(1,Math.min(60,Math.floor(lon360(lon)/6)+1))}
function lon0GK6(z){return normLon(z*6-3)}
function zoneGK3(lon){return Math.max(1,Math.min(120,Math.round(lon360(lon)/3)))}
function lon0GK3(z){return normLon(z*3)}
function zoneUTM(lon){return Math.max(1,Math.min(60,Math.floor((lon+180)/6)+1))}
function lon0UTM(z){return z*6-183}
function zoneFromPrefixedY(y){const a=Math.abs(Number(y));if(!Number.isFinite(a)||a<1000000)return null;const z=Math.floor(a/1000000);return z>=1&&z<=120?z:null}
function projectionFor(c,zone,prefix){if(c.kind==='gk6'){const lon0=lon0GK6(zone),fe=prefix?zone*1e6+500000:500000;return {lon0,fe,fn:0,k:1,zone}}if(c.kind==='gk3'){const lon0=lon0GK3(zone),base=c.datum==='gsk2011'?250000:500000,fe=prefix?zone*1e6+base:base;return {lon0,fe,fn:0,k:1,zone}}if(c.kind==='utm'){return {lon0:lon0UTM(zone),fe:500000,fn:0,k:.9996,zone}}if(c.kind==='fixedtm')return {lon0:c.lon0,lat0:c.lat0||0,fe:c.fe,fn:c.fn,k:c.k,zone:null};if(c.kind==='custom')return {lon0:c.lon0,fe:c.fe,fn:c.fn,k:c.k,zone:null};return null}

const api=Object.freeze({Ell,E,TO_WGS,GSK_TO_PZ,PZ_TO_9011,d2r,r2d,as2r,geodToEcef,ecefToGeod,helm,invHelm,arc,norm,normLon,lon360,fwdTM,invTMApprox,invTM,webFwd,webInv,datumToWgs,wgsToDatum,zoneGK6,lon0GK6,zoneGK3,lon0GK3,zoneUTM,lon0UTM,zoneFromPrefixedY,projectionFor});
if(typeof module==='object'&&module.exports)module.exports=api;
else root.KhitunCoordinates=api;
})(typeof globalThis!=='undefined'?globalThis:this);
