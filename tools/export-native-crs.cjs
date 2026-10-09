const fs=require('node:fs'),path=require('node:path'),vm=require('node:vm'),crypto=require('node:crypto');
const root=path.resolve(__dirname,'../src/KhitunGeo/wwwroot');
const html=fs.readFileSync(path.join(root,'index.html'),'utf8');
const source=html.slice(html.indexOf('const CRS={')+'const CRS='.length,html.indexOf('\nfor(const [v,c] of Object.entries(CRS))')).trim().replace(/;$/,'');
const builtin=vm.runInNewContext('('+source+')');
const context=vm.createContext({regionalCatalogues:{msk:[],sk63:[]},refreshUI(){},workspaceExtras:{},msk:JSON.parse(fs.readFileSync(path.join(root,'crs/msk.json'),'utf8')),sk63:JSON.parse(fs.readFileSync(path.join(root,'crs/sk63.json'),'utf8')),updates:JSON.parse(fs.readFileSync(path.join(root,'crs/regional-updates.json'),'utf8'))});
const resolvers=html.slice(html.indexOf('function parseRegionalRecord('),html.indexOf('async function loadRegionalCatalogues('));
vm.runInContext(resolvers+'\ninstallRegionalCatalogues(msk,sk63,updates)',context);
const fields=['kind','datum','lon0','lat0','fe','fn','k','ellipsoid','towgs'];
const definition=c=>Object.fromEntries(fields.filter(k=>c[k]!==undefined).map(k=>[k,c[k]]));
const systems=Object.entries(builtin).filter(([,c])=>!['regional','custom'].includes(c.kind)).map(([id,c])=>({id,name:c.name,definition:definition(c)}));
for(const family of ['msk','sk63'])for(const c of context.regionalCatalogues[family])systems.push({id:family+':'+c.id,name:c.name,definition:definition(c)});
if(new Set(systems.map(s=>s.id)).size!==systems.length)throw Error('Duplicate CRS IDs');
const hash=value=>crypto.createHash('sha256').update(value).digest('hex');
const sourceHashes={builtin:hash(source),resolvers:hash(resolvers)};
for(const name of ['msk','sk63','regional-updates'])sourceHashes[name]=hash(fs.readFileSync(path.join(root,'crs/'+name+'.json')));
const output=JSON.stringify({version:1,sourceHashes,systems},null,2)+'\n',file=path.join(root,'crs/native-systems.json');
if(process.argv.includes('--check')){if(fs.readFileSync(file,'utf8')!==output)throw Error('Native CRS catalogue is stale. Run node tools/export-native-crs.cjs');console.log('PASS native CRS catalogue matches '+systems.length+' resolved definitions');}
else {fs.writeFileSync(file,output);console.log('Exported '+systems.length+' resolved native CRS definitions');}
