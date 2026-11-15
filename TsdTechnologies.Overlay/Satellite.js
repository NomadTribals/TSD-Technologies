(()=>{
 let url='',shown=true,status='checking',mapKey=77;
 const frame=document.getElementById('satellite-map'),toggle=document.getElementById('satellite-mini');
 const panel=document.getElementById('satellite-required'),title=document.getElementById('satellite-required-title'),text=document.getElementById('satellite-required-text'),forge=document.getElementById('satellite-forge');
 const sync=()=>{
  const active=shown&&currentTabletView==='map'&&url&&status==='ready';
  frame.hidden=status!=='ready';panel.hidden=status==='ready';toggle.disabled=status!=='ready';
  if(active){if(frame.getAttribute('src')!==url)frame.src=url;}
  else if(frame.hasAttribute('src'))frame.removeAttribute('src');
 };
 window.tsd.syncSatellite=()=>{sync();window.overlay.send('tsd-satellite-state','');};
 window.tsd.satelliteWindowVisible=value=>{shown=!!value;sync();};
 window.overlay.on('tsd-satellite-state',raw=>{
  let state;try{state=typeof raw==='string'?JSON.parse(raw):raw;}catch{return;}
  url=state.url||'';status=state.status||'unavailable';toggle.textContent='Minimap: '+(state.enabled?'ON':'OFF');
  mapKey=Number(state.mapKey)||0;
  const missing=status==='missing-client'||status==='missing-server';
  title.textContent=missing?'Satellite required':status==='checking'?'Connecting to Satellite':'Satellite connection unavailable';
  text.textContent=missing?(status==='missing-client'?'Install Satellite by Fums to use live maps and the tablet minimap.':'Satellite’s server component could not be found. Install the full Satellite package to use this feature.')+' After installation, restart the game and SPT server.':status==='checking'?'Checking the Satellite connection…':'Make sure the SPT server is running with Satellite installed, then select Reload map.';
  forge.hidden=!missing;
  toggle.setAttribute('aria-pressed',String(!!state.enabled));sync();
  frame.contentWindow?.postMessage({tsdMapKey:mapKey},url?new URL(url).origin:'*');
 });
 toggle.onclick=()=>window.overlay.send('tsd-satellite-toggle','');
 forge.onclick=()=>window.overlay.send('tsd-satellite-forge','');
 const shortcut=()=>window.overlay.send('tsd-map-shortcut','');
 window.addEventListener('message',e=>{
  if(e.source!==frame.contentWindow||!url||e.origin!==new URL(url).origin)return;
  if(e.data?.tsdMapShortcut)shortcut();
  else if(typeof e.data?.tsdSatellitePreferences==='string'&&e.data.tsdSatellitePreferences.length<=131072)window.overlay.send('tsd-satellite-preferences',e.data.tsdSatellitePreferences);
 });
 document.addEventListener('keydown',e=>{if(e.repeat||e.ctrlKey||e.altKey||e.metaKey||e.key?.length!==1||e.target?.closest?.('input,textarea,select,[contenteditable]')||isCapturingTabletHotkey)return;if(mapKey&&e.keyCode===mapKey){e.preventDefault();e.stopPropagation();shortcut();}});
 frame.addEventListener('load',()=>{if(url)frame.contentWindow?.postMessage({tsdMapKey:mapKey},new URL(url).origin);});
 document.getElementById('satellite-reload').onclick=()=>{frame.removeAttribute('src');window.overlay.send('tsd-satellite-state','refresh');};
 window.overlay.send('tsd-satellite-state','');
})();
