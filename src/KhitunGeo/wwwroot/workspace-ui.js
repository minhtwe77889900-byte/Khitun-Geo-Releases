'use strict';
// This script shares the existing classic-script workspace, including its undo journal.
const WX=KhitunWorkspace;
const PROFILE_FIELDS=['source','target','autoZone','srcZonePrefix','dstZonePrefix','zoneManual','regional','custom','view'];
const EXCHANGE_FIELDS=['impPreset','impEncoding','impDelimiter','impDecimal','expFormat','expDelimiter','expDecimals','expScope','expHeader','expSwapXY'];
function workspaceDialog(title,body){
 const dialog=document.createElement('dialog');dialog.className='settingsDialog workspaceDialog';dialog.setAttribute('aria-label',title);
 dialog.innerHTML=`<div class="modalHead"><h2>${WX.escape(title)}</h2><button data-close aria-label="Закрыть">×</button></div><div class="modalBody">${body}</div><div class="modalFoot"><button data-close>Закрыть</button></div>`;
 const primary=dialog.querySelector('.modalBody > button[data-run], .modalBody > button[data-load], .modalBody > button[data-export], .modalBody > button[data-apply]');if(primary){primary.classList.add('accent');dialog.querySelector('.modalFoot').appendChild(primary);dialog.querySelector('.modalFoot [data-close]').textContent='Отмена';}
 document.body.appendChild(dialog);dialog.querySelectorAll('[data-close]').forEach(b=>b.onclick=()=>dialog.close());dialog.addEventListener('close',()=>dialog.remove());dialog.showModal();return dialog;
}
function qualityIssues(){return WX.quality(pts,crsParams($('src').value,'src').kind==='geo');}
function openQuality(){
 const issues=qualityIssues(),errors=issues.filter(q=>q.severity==='error').length;
 const d=workspaceDialog('Контроль качества',`<p>Ошибок: ${errors}; предупреждений: ${issues.length-errors}. Проверка не изменяет данные и не оценивает геодезическую точность.</p><p>Проверяются X/Y/Z, диапазоны географических координат, повтор номеров и точные совпадения X/Y.</p><div>${issues.slice(0,500).map(q=>`<button data-row="${q.row}" style="display:block;margin:4px 0">${q.severity==='error'?'Ошибка':'Предупреждение'} · строка ${q.row+1}: ${WX.escape(q.message)}</button>`).join('')||'Замечаний нет.'}</div>${issues.length>500?'<p>Показаны первые 500 замечаний.</p>':''}`);
 d.querySelectorAll('[data-row]').forEach(b=>b.onclick=()=>{selected=new Set([Number(b.dataset.row)]);lastSelected=Number(b.dataset.row);render();d.close();$('tbody').querySelector(`tr[data-i="${lastSelected}"]`)?.scrollIntoView({block:'center'});});
}
function profileSettings(){const p=projectPayload(),result={};for(const f of PROFILE_FIELDS)result[f]=JSON.parse(JSON.stringify(p[f]));result.exchange={};for(const id of EXCHANGE_FIELDS){const el=$(id);if(el)result.exchange[id]=el.type==='checkbox'?el.checked:el.value;}return result;}
function validateProfile(p){
 if(!p||typeof p!=='object'||!CRS[p.source]||!CRS[p.target])throw Error('Неизвестная система координат профиля.');
 for(const side of ['src','dst']){const id=side==='src'?p.source:p.target;if(CRS[id].kind==='regional'){const family=CRS[id].family,code=p.regional?.[side]?.[family];if(!regionalCatalogues[family]?.some?.(r=>r.id===code)){
   // Use the engine's own catalogue validation without changing current state.
   const old=regionalSelection;try{regionalSelection=p.regional;crsParams(id,side);}finally{regionalSelection=old;}
  }}}
 for(const id of ['lon0','fe','fn','k'])if(!Number.isFinite(WX.number(p.custom?.[id])))throw Error('Некорректные параметры пользовательской СК.');
 if(WX.number(p.custom.k)<=0)throw Error('Масштаб пользовательской СК должен быть положительным.');
 if(p.view?.depthReference!==''&&!Number.isFinite(WX.number(p.view?.depthReference)))throw Error('Некорректная отметка отсчёта глубины.');
}
function openProfiles(){
 const profiles=workspaceExtras.profiles||[];
 const d=workspaceDialog('Профили объекта',`<p>Сохраняются СК, зоны, пользовательская проекция, отметка отсчёта глубины и настройки обмена. Точки, заказчик и описание объекта не сохраняются в профиль.</p><p>Профили входят в этот проект и его резервную копию.</p><label>Название <input data-name maxlength="80" placeholder="Например: площадка МСК-164"></label><button data-save>Сохранить настройки</button><div>${profiles.map((p,i)=>`<p><strong>${WX.escape(p.name)}</strong> <button data-apply="${i}">Применить</button> <button data-delete="${i}">Удалить</button></p>`).join('')||'<p>Профилей пока нет.</p>'}</div>`);
 d.querySelector('[data-save]').onclick=()=>{try{const name=d.querySelector('[data-name]').value.trim();if(!name)throw Error('Введите название профиля.');const settings=profileSettings();validateProfile(settings);if(profiles.length>=30)throw Error('Максимум 30 профилей в проекте.');snapshot();workspaceExtras.profiles=[...profiles,{name,settings}];scheduleSave();d.close();openProfiles();}catch(e){alert(e.message);}};
 d.querySelectorAll('[data-apply]').forEach(b=>b.onclick=()=>{try{
  const p=profiles[Number(b.dataset.apply)];validateProfile(p.settings);const current=projectPayload(),remaining=history.slice(),future=redoHistory.slice();snapshot();const afterSnapshot=history.slice();
  const merged={...current,exchange:p.settings.exchange,workspace:{...current.workspace,importDefaults:{...p.settings.exchange}}};for(const key of PROFILE_FIELDS)merged[key]=p.settings[key];merged.view={...current.view,depthReference:p.settings.view.depthReference};
  try{applyProjectPayload(merged);}catch(e){applyProjectPayload(current);history=remaining;redoHistory=future;throw e;}
  history=afterSnapshot;redoHistory=[];
  scheduleSave();d.close();setStatus('Профиль применён. Точки не изменены. Ctrl+Z — отменить настройки СК.');
 }catch(e){alert(e.message);}});
 d.querySelectorAll('[data-delete]').forEach(b=>b.onclick=()=>{if(!confirm('Удалить профиль?'))return;snapshot();workspaceExtras.profiles=profiles.filter((_,i)=>i!==Number(b.dataset.delete));scheduleSave();d.close();openProfiles();});
}
let dwgRequest=null;
window.chrome?.webview?.addEventListener('message',event=>{
 const result=event.data;if(result?.type!=='dwg-result'||!dwgRequest||result.id!==dwgRequest.id)return;
 const request=dwgRequest;dwgRequest=null;clearTimeout(request.timer);
 if(result.error)request.reject(Error(String(result.error)));else if(typeof result.dxf!=='string'||result.dxf.length>32*1024*1024)request.reject(Error('Некорректный результат чтения DWG.'));else request.resolve(result);
});
async function openDwgImport(file){
 try{
  if(!window.chrome?.webview)throw Error('DWG открывается в установленном приложении Khitun Geo для Windows. В браузере используйте ASCII DXF.');
  if(dwgRequest)throw Error('Дождитесь чтения предыдущего DWG.');
  if(!file.size||file.size>16*1024*1024)throw Error('DWG должен быть не больше 16 МБ. Разделите чертёж.');
  const frame=drawingFrame(),project=currentProjectId;
  setStatus('Читаем DWG… Файл обрабатывается на компьютере.');
  const base64=await new Promise((resolve,reject)=>{const reader=new FileReader();reader.onload=()=>resolve(String(reader.result).split(',')[1]);reader.onerror=()=>reject(Error('Не удалось открыть DWG.'));reader.readAsDataURL(file);});
  if(dwgRequest)throw Error('Дождитесь чтения предыдущего DWG.');
  const result=await new Promise((resolve,reject)=>{const id='dwg-'+Date.now()+'-'+Math.random().toString(36).slice(2);const timer=setTimeout(()=>{if(dwgRequest?.id===id)dwgRequest=null;reject(Error('Чтение DWG занимает слишком долго. Попробуйте меньший чертёж или ASCII DXF.'));},120000);dwgRequest={id,resolve,reject,timer};try{window.chrome.webview.postMessage({type:'dwg-read',id,base64});}catch(e){clearTimeout(timer);dwgRequest=null;reject(e);}});
  if(project!==currentProjectId||frame!==drawingFrame())throw Error('Проект или СК изменились во время чтения. Откройте чертёж заново.');
  await openDxfImport(file,result);
 }catch(e){setStatus('DWG: '+e.message,'warn');alert(e.message);}
}
async function openDxfImport(file,converted=null){
 try{
  const format=converted?'DWG':'DXF',frame=drawingFrame(),project=currentProjectId;
  if(file.size>32*1024*1024)throw Error('Чертёж больше 32 МБ. Разделите его.');
  let text=converted?.dxf;
  if(!converted){const bytes=await file.arrayBuffer();text=new TextDecoder('utf-8').decode(bytes);if(/ANSI_1251/i.test(text))text=new TextDecoder('windows-1251').decode(bytes);}
  if(project!==currentProjectId||frame!==drawingFrame())throw Error('Проект или СК изменились. Откройте чертёж заново.');
  const parsed=WX.parseDxf(text),geo=crsParams($('src').value,'src').kind==='geo';
  const d=workspaceDialog('Наложение '+format+' — 2D',`<p>${WX.escape(file.name)} · опорных точек: ${parsed.total}; слоёв: ${parsed.layers.length}.</p><p>Подтвердите: чертёж уже в текущей СК <strong>${WX.escape(CRS[$('src').value].name)}</strong>. Перепроецирование чертежа не выполняется.</p><p>CAD X → Y (восток), CAD Y → X (север). Для обратной записи выберите перестановку.</p><label>Единицы <select data-unit>${geo?'<option value="1">Градусы</option>':'<option value="1">Метры</option><option value="0.001">Миллиметры</option><option value="0.01">Сантиметры</option><option value="0.3048">Футы (международные)</option>'}</select></label><label><input type="checkbox" data-swap> Поменять CAD X и Y местами</label><p>Пропущенные объекты: ${WX.escape(JSON.stringify(parsed.skipped))}. Поддерживаются 2D-блоки и полилинии с дугами. Текст, размеры, штриховки, внешние ссылки, 3D-объекты и поверхности Civil 3D не отображаются.</p>${converted?`<p>Сообщений при чтении DWG: ${Number(converted.notices)||0}. Подложка может содержать не все объекты исходного чертежа.</p>`:''}<label><input type="checkbox" data-confirm> Система координат и единицы проверены</label><button data-load>Добавить чертёж</button>`);
  d.querySelector('[data-load]').onclick=()=>{try{
   if(!d.querySelector('[data-confirm]').checked)throw Error('Подтвердите систему координат и единицы.');if(frame!==drawingFrame()||project!==currentProjectId)throw Error('Проект или СК изменились. Откройте чертёж заново.');
   if(workspaceExtras.drawing&&!confirm('Заменить текущий чертёж?'))return;
   const drawing={name:file.name,frame,scale:Number(d.querySelector('[data-unit]').value),opacity:.65,hidden:parsed.hidden||[],paths:parsed.paths,skipped:parsed.skipped};
   if(d.querySelector('[data-swap]').checked)drawing.paths=drawing.paths.map(p=>({...p,vertices:p.vertices.map(v=>[v[1],v[0]]),...(p.curve?{curve:{...p.curve,c:[...p.curve.c].reverse(),u:[...p.curve.u].reverse(),v:[...p.curve.v].reverse()}}:{})}));
   const candidate=WX.validateExtras({...workspaceExtras,drawing});
   // Probe the complete project store before mutating the drawing or undo journal.
   const store=getStore();store[currentProjectId]={...projectPayload(),workspace:candidate};probeStore(store);
   snapshot(true);workspaceExtras=candidate;plotView.ready=false;fitPlot();scheduleSave();d.close();setStatus('2D '+format+' добавлен отдельно от точек. Ctrl+Z — отменить.');
  }catch(e){alert(e.message);}};
 }catch(e){setStatus('Чертёж: '+e.message,'warn');alert(e.message);}
}
function openDrawing(){
 const drawing=workspaceExtras.drawing;
 if(!drawing){const input=document.createElement('input');input.type='file';input.accept='.dxf,.dwg';input.onchange=()=>{if(input.files[0])importFile(input.files[0]);};input.click();return;}
 const layers=[...new Set(drawing.paths.flatMap(p=>[p.layer,...(p.parents||[])]))].sort((a,b)=>a.localeCompare(b,'ru'));
 const d=workspaceDialog('Слой чертежа',`<p>${WX.escape(drawing.name)}</p>${drawing.frame!==drawingFrame()?'<p class="warn">Слой скрыт: настройки СК отличаются от настроек при загрузке. Верните исходную СК или загрузите чертёж в нужной СК.</p>':''}<label>Непрозрачность <input data-opacity type="range" min="0.1" max="1" step="0.05" value="${drawing.opacity}"></label><div>${layers.map((l,i)=>`<label style="display:block"><input data-layer="${i}" type="checkbox" ${drawing.hidden.includes(l)?'':'checked'}>${WX.escape(l)}</label>`).join('')}</div><button data-apply>Применить</button><button data-remove>Убрать чертёж</button>`);
 d.querySelector('[data-apply]').onclick=()=>{snapshot(true);drawing.opacity=Number(d.querySelector('[data-opacity]').value);drawing.hidden=[...d.querySelectorAll('[data-layer]')].filter(e=>!e.checked).map(e=>layers[Number(e.dataset.layer)]);fitPlot();scheduleSave();d.close();};
 d.querySelector('[data-remove]').onclick=()=>{snapshot(true);delete workspaceExtras.drawing;fitPlot();scheduleSave();d.close();};
}
function passportSignature(points){return WX.sha256(JSON.stringify(points.map(p=>[p.no,p.x,p.y,p.z,p.d])));}
function capturePassport(){
 const issues=qualityIssues();if(issues.some(q=>q.severity==='error'))throw Error('Обнаружены ошибки данных. Откройте «Контроль качества».');
 return {createdAt:new Date().toISOString(),project:$('projectName').value,source:describeSystem($('src').value,'src'),target:describeSystem($('dst').value,'dst'),settings:profileSettings(),quality:issues,before:passportSignature(pts)};
}
function finishPassport(capture,count){workspaceExtras.passportResult=passportSignature(pts);workspaceExtras.passport={Дата:capture.createdAt,Проект:capture.project,'Исходная СК':capture.source,'Выходная СК':capture.target,'Число преобразованных точек':count,'SHA-256 исходных точек':capture.before,'SHA-256 результата':workspaceExtras.passportResult,'Высоты Z':'Сохранены без изменения. Пересчёт вертикальной системы и модели геоида не выполнялся.','Настройки расчёта':capture.settings,'Число замечаний проверки':capture.quality.length,'Замечания в отчёте':'Первые 500; полный список доступен в контроле качества исходных данных.',quality:capture.quality.slice(0,500)};}
function passportHtmlForNative(){if(!workspaceExtras.passport)throw Error('Сначала выполните преобразование.');return WX.passportHtml({...workspaceExtras.passport,'Состояние данных':workspaceExtras.passportResult===passportSignature(pts)?'Таблица соответствует результату расчёта':'Таблица изменена после расчёта; паспорт относится к ранее выполненному преобразованию'});}
function openPassport(){
 if(!workspaceExtras.passport){alert('Паспорт появится после успешного преобразования координат.');return;}
 const d=workspaceDialog('Паспорт преобразования',`<p>Экспорт записи о последнем успешном преобразовании. ${workspaceExtras.passportResult===passportSignature(pts)?'Таблица соответствует расчёту.':'Таблица изменена после расчёта.'}</p><label>Формат <select data-format><option value="html">HTML</option><option value="pdf">PDF</option></select></label><button data-export>Сохранить паспорт</button>`);
 d.querySelector('[data-export]').onclick=()=>{const html=passportHtmlForNative();if(d.querySelector('[data-format]').value==='html')download('Khitun-Geo-passport.html',html,'text/html;charset=utf-8');else if(window.chrome?.webview)window.chrome.webview.postMessage('passport-pdf');else{alert('В браузере выберите «Сохранить как PDF» в диалоге печати. В Windows-приложении PDF сохраняется напрямую.');const w=window.open('','_blank');if(!w){alert('Разрешите открытие окна для печати.');return;}w.document.write(html);w.document.close();w.focus();w.print();}d.close();};
}
function registerWorkspaceTool(id,label,action,menu){const b=document.createElement('button');b.id=id;b.textContent=label;b.setAttribute('aria-haspopup','dialog');b.onclick=()=>{document.querySelectorAll('.railGroup').forEach(g=>g.open=false);try{action();}catch(e){alert(e.message);}};$(menu).appendChild(b);}
for(const [id,label,action,menu]of [['qualityBtn','Контроль качества',openQuality,'analysisActions'],['dxfBtn','Чертёж DXF / DWG (2D)',openDrawing,'viewActions'],['profilesBtn','Профили объекта',openProfiles,'projectActions'],['passportBtn','Паспорт преобразования',openPassport,'dataActions']])registerWorkspaceTool(id,label,action,menu);
function refreshDrawingStatus(){const d=workspaceExtras.drawing,b=$('dxfBtn');if(!b)return;const hidden=d&&d.frame!==drawingFrame();b.textContent=hidden?'Чертёж DXF / DWG (СК отличается)':'Чертёж DXF / DWG (2D)';b.title=hidden?'Слой скрыт: система координат изменилась. Откройте настройки слоя.':d?.name||'Загрузить 2D чертёж';}
refreshDrawingStatus();

function surveyInputControls(multiple=false){return `<label>${multiple?'Файлы':'Вторая съёмка'} <input data-files type="file" accept=".csv,.txt,.json" ${multiple?'multiple':''}></label><label>Формат <select data-format><option value="header">CSV с заголовками no,N,E,Z,D / JSON points</option><option value="pnezd">PNEZD без заголовка</option><option value="penzd">PENZD без заголовка</option></select></label><label>Разделитель <select data-delimiter><option value=";">Точка с запятой</option><option value=",">Запятая</option><option value="tab">Табуляция</option></select></label><p>Текстовые файлы — UTF-8. X/N = север, Y/E = восток. Номера должны быть заполнены и уникальны; 001 и 1 — разные номера.</p>`;}
async function readSurveyFile(file,dialog){if(file.size>32*1024*1024)throw Error('Файл больше 32 МБ.');const sep=dialog.querySelector('[data-delimiter]').value;return WX.readPoints(await file.text(),dialog.querySelector('[data-format]').value,sep==='tab'?'\t':sep);}
function assertSurveySource(data){
 if(data.source&&data.source!==$('src').value)throw Error('СК файла отличается от текущей исходной СК. Сначала приведите данные к одной системе.');
 const m=data.metadata;if(data.source==='msk'||data.source==='sk63'){const family=CRS[data.source].family;if(m.regional?.src?.[family]!==regionalSelection.src[family])throw Error('Региональная СК файла отличается или не определена.');}
 if(data.source==='custom'){const custom=projectPayload().custom;if(!m.custom||['datum','lon0','fe','fn','k'].some(k=>String(m.custom[k])!==String(custom[k])))throw Error('Параметры пользовательской СК файла отличаются.');}
 if(m?.zoneManual!==undefined&&(String(m.zoneManual)!==String($('zoneManual').value)||!!m.autoZone!==$('autoZone').checked||!!m.srcZonePrefix!==$('srcZonePrefix').checked))throw Error('Настройки зон файла отличаются. Проверьте исходную СК.');
}
function openComparison(){
 try{if(crsParams($('src').value,'src').kind==='geo')throw Error('Для сравнения в метрах сначала преобразуйте обе съёмки в одну плоскую СК.');
 const base=JSON.parse(JSON.stringify(pts.filter(p=>String(p.x).trim()||String(p.y).trim()))),frame=drawingFrame();
 const d=workspaceDialog('Сравнение съёмок по номерам',`${surveyInputControls()}<p>Δ = вторая съёмка − текущая. СК: ${WX.escape(describeSystem($('src').value,'src'))}</p><label>Допуск в плане, м <input data-plan type="number" min="0" step="0.001" value="0.01"></label><label>Допуск Z, м <input data-height type="number" min="0" step="0.001" value="0.01"></label><label><input data-confirm type="checkbox"> Обе съёмки в одной СК, зоне и системе высот, единицы — метры</label><button data-run>Сравнить</button><div data-result role="status"></div>`);
 d.querySelector('[data-run]').onclick=async()=>{const output=d.querySelector('[data-result]');try{if(!d.querySelector('[data-confirm]').checked)throw Error('Подтвердите одинаковые СК и единицы.');const file=d.querySelector('[data-files]').files[0];if(!file)throw Error('Выберите вторую съёмку.');const data=await readSurveyFile(file,d);if(!d.isConnected)return;if(frame!==drawingFrame())throw Error('Настройки СК изменились. Откройте сравнение заново.');assertSurveySource(data);
  const r=WX.compareSurveys(base,data.points,WX.number(d.querySelector('[data-plan]').value),WX.number(d.querySelector('[data-height]').value));
  output.innerHTML=`<p>Совпало: ${r.rows.length}; превышений: ${r.exceeded}; новых: ${r.added.length}; отсутствуют: ${r.missing.length}. Без пары Z: ${r.rows.filter(q=>q.dz===null).length}.</p><p>Первый набор: ${base.length} точек текущей таблицы на момент открытия окна. Второй: ${WX.escape(file.name)}.</p><div style="overflow:auto"><table><thead><tr><th>Номер</th><th>ΔX, м</th><th>ΔY, м</th><th>ΔZ, м</th><th>В плане, м</th></tr></thead><tbody>${r.rows.slice(0,100).map(q=>`<tr ${q.exceeds?'class="warn"':''}><td>${WX.escape(q.no)}</td>${[q.dx,q.dy,q.dz,q.plan].map(n=>`<td>${n===null?'нет Z':n.toFixed(4)}</td>`).join('')}</tr>`).join('')}</tbody></table></div><p>Показаны первые 100 совпадений. Полный результат и несовпавшие номера — в CSV.</p><button data-download>Скачать CSV</button>`;
  output.querySelector('[data-download]').onclick=()=>download('Khitun-Geo-comparison.csv',WX.comparisonCsv(r),'text/csv;charset=utf-8');
 }catch(e){output.textContent=e.message;}};
 }catch(e){alert(e.message);}
}
function prepareCivil3D(){
 try{
  if(crsParams($('src').value,'src').kind==='geo')throw Error('Civil 3D: сначала преобразуйте широту/долготу в плоскую СК.');
  const points=pts.filter(p=>String(p.x).trim()||String(p.y).trim());if(!points.length)throw Error('Нет точек для подготовки.');const numbers=new Set();
  for(const [i,p]of points.entries()){if(!/^\d+$/.test(String(p.no).trim())||!Number.isSafeInteger(Number(p.no))||Number(p.no)<=0)throw Error(`Строка ${i+1}: для PNEZD нужен положительный целый номер точки.`);if(numbers.has(Number(p.no)))throw Error('Повтор номера Civil 3D: '+p.no);numbers.add(Number(p.no));num(p.x);num(p.y);if(!String(p.z??'').trim())throw Error(`Точка ${p.no}: Z не задана. Подставлять ноль автоматически нельзя.`);num(p.z);}
  const d=workspaceDialog('Подготовить для Civil 3D',`<p>Проверено точек: ${points.length}. Номера уникальны, X/Y/Z заполнены.</p><p>Экспорт PNEZD: номер, север (X), восток (Y), высота, описание. Разделитель — запятая, без заголовка. Точность — 3 знака; её можно изменить в мастере.</p><p>В Civil 3D выберите тот же формат PNEZD и ту же СК проекта. Геодезическая X соответствует Northing, а не CAD X. Перенос и привязка СК не выполняются автоматически.</p><p>Пустые строки не экспортируются. Запятые и переносы строк в описаниях заменяются пробелами.</p><button data-export>Открыть проверенный экспорт</button>`);
  d.querySelector('[data-export]').onclick=()=>{d.close();$('expFormat').value='pnezd';$('expDelimiter').value=',';$('expDecimals').value='3';$('expScope').value='all';$('expHeader').checked=false;$('expSwapXY').checked=false;openExportWizard();};
 }catch(e){alert(e.message);}
}
function transformBatchPoints(points){
 const source=$('src').value,target=$('dst').value,sp=$('srcZonePrefix').checked,dp=$('dstZonePrefix').checked,targetC=crsParams(target,'dst'),zones=new Set();
 const issues=WX.quality(points,crsParams(source,'src').kind==='geo');if(issues.some(q=>q.severity==='error'))throw Error('Файл содержит ошибки X/Y/Z.');
 const out=points.map((p,i)=>{if(!String(p.x).trim()||!String(p.y).trim())throw Error(`Строка ${i+1}: нужны X/Y.`);const height=String(p.z??'').trim()?num(p.z):0,q=transformPoint([num(p.x),num(p.y),height],source,target,sp,dp);if(!q.every(Number.isFinite))throw Error('Некорректный результат преобразования.');
  if(['utm','gk6','gk3'].includes(targetC.kind)){const w=toWgs([num(p.x),num(p.y),height],source,sp);zones.add(detectZoneForPoint(targetC,wgsToDatum(w,targetC.datum),false,dp));}
  return {...p,x:String(q[0]),y:String(q[1]),z:p.z};
 });if(zones.size>1&&(targetC.kind==='utm'||!dp))throw Error('Несколько выходных зон без префикса. Разделите файл или задайте одну зону.');return {points:out,zones:[...zones],warnings:issues.filter(q=>q.severity==='warning')};
}
function openBatch(){
 const settings=profileSettings(),fingerprint=JSON.stringify(settings),d=workspaceDialog('Пакетное преобразование',`${surveyInputControls(true)}<p>Все файлы: ${WX.escape(describeSystem($('src').value,'src'))} → ${WX.escape(describeSystem($('dst').value,'dst'))}</p><p>До 20 файлов, всего до 32 МБ. Каждый файл обрабатывается отдельно; исходники и открытая таблица не изменяются. Z сохраняется. Результат — ZIP с CSV и отчётом.</p><label><input data-confirm type="checkbox"> Все файлы используют указанную исходную СК, зону и порядок координат</label><button data-run>Обработать файлы</button><div data-result role="status"></div>`);
 d.querySelector('[data-run]').onclick=async()=>{const result=d.querySelector('[data-result]'),button=d.querySelector('[data-run]');try{
  if(!d.querySelector('[data-confirm]').checked)throw Error('Подтвердите исходную систему координат.');const files=[...d.querySelector('[data-files]').files];if(!files.length||files.length>20)throw Error('Выберите от 1 до 20 файлов.');if(files.reduce((n,f)=>n+f.size,0)>32*1024*1024)throw Error('Общий размер больше 32 МБ.');
  button.disabled=true;d.querySelectorAll('input,select').forEach(el=>el.disabled=true);const report={date:new Date().toISOString(),settings,height:'Z сохранена; вертикальное преобразование не выполнялось',files:[]},outputs=[];
  for(const [i,file]of files.entries()){if(!d.isConnected)return;if(JSON.stringify(profileSettings())!==fingerprint)throw Error('Настройки изменились: пакет отменён. Откройте окно заново.');result.textContent=`Обработка ${i+1}/${files.length}: ${file.name}`;
   try{const parsed=await readSurveyFile(file,d);if(!d.isConnected)return;if(JSON.stringify(profileSettings())!==fingerprint)throw Error('Настройки изменились во время чтения файла.');assertSurveySource(parsed);const converted=transformBatchPoints(parsed.points),name=String(i+1).padStart(2,'0')+'_'+file.name.replace(/\.[^.]+$/,'').replace(/[^a-zа-яё0-9_-]/gi,'_')+'.csv';outputs.push({name,text:WX.pointCsv(converted.points)});report.files.push({source:file.name,output:name,count:converted.points.length,zones:converted.zones,warnings:converted.warnings.slice(0,500),warningCount:converted.warnings.length});
   }catch(e){report.files.push({source:file.name,error:e.message});}
  }
  const successes=outputs.length;outputs.push({name:'report.json',text:JSON.stringify(report,null,2)});const zip=WX.zipStored(outputs);result.innerHTML=`<p>Готово: ${successes}/${files.length}. Ошибок: ${files.length-successes}.</p>${report.files.filter(f=>f.error).map(f=>`<p class="warn">${WX.escape(f.source)}: ${WX.escape(f.error)}</p>`).join('')}<p>Отчёт содержит СК, параметры зон и предупреждения. Проверьте его перед использованием результатов.</p><button data-download>Скачать ZIP с результатами и отчётом</button>`;result.querySelector('[data-download]').onclick=()=>download('Khitun-Geo-batch.zip',zip,'application/zip');
 }catch(e){result.textContent=e.message;}finally{button.disabled=false;d.querySelectorAll('input,select').forEach(el=>el.disabled=false);}};
}
for(const [id,label,action,menu]of [['compareBtn','Сравнить съёмки',openComparison,'analysisActions'],['civilPrepareBtn','Подготовить для Civil 3D',prepareCivil3D,'dataActions'],['batchBtn','Пакетное преобразование',openBatch,'dataActions']])registerWorkspaceTool(id,label,action,menu);
