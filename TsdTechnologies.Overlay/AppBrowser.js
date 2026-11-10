(() => {
 'use strict';
 const root=document.getElementById('browser-view');
 let snapshot={apps:[],busy:false,message:'Browse compatible Field OS apps.',updatedUtc:''},selected=null;
 const list=root.querySelector('[data-browser-list]'),detail=root.querySelector('[data-browser-detail]');
 const status=root.querySelector('[data-browser-status]'),search=root.querySelector('[data-browser-search]');
 const getInstalled=id=>registeredApps.find(a=>a.id===id);
 const node=(tag,cls,text)=>{const el=document.createElement(tag);if(cls)el.className=cls;if(text!=null)el.textContent=text;return el;};
 function icon(entry){
  const holder=node('span','browser-icon');const app=getInstalled(entry.appId);
  if(app?.iconSvg)holder.innerHTML=app.iconSvg;
  else if(entry.thumbnail){const img=node('img');img.alt='';img.loading='lazy';img.referrerPolicy='no-referrer';img.src=entry.thumbnail;img.addEventListener('error',()=>{img.remove();holder.textContent=entry.name.slice(0,2).toUpperCase()},{once:true});holder.append(img);}
  else holder.textContent=entry.forgeId===3301?'TSD':entry.name.slice(0,2).toUpperCase();
  return holder;
 }
 function button(text,action,cls='browser-action'){const b=node('button',cls,text);b.type='button';b.addEventListener('click',action);return b;}
 function showDetails(entry){
  selected=entry.forgeId;list.hidden=true;detail.hidden=false;detail.replaceChildren();
  detail.append(button('← All apps',()=>{selected=null;render();},'browser-back'));
  const heading=node('div','browser-detail-heading');heading.append(icon(entry));
  const title=node('div');title.append(node('span','eyebrow','FIELD OS APP'),node('h2','',entry.name),node('p','browser-byline','By '+entry.author));heading.append(title);detail.append(heading);
  const badges=node('div','browser-badges');if(getInstalled(entry.appId))badges.append(node('span','browser-installed','Installed'));
  if(entry.version)badges.append(node('span','browser-badge','Version '+entry.version));if(entry.spt)badges.append(node('span','browser-badge','SPT '+entry.spt));detail.append(badges);
  detail.append(node('p','browser-summary',entry.summary));
  const actions=node('div','browser-actions');if(getInstalled(entry.appId))actions.append(button('Open app',()=>openExternalApp(entry.appId)));
  actions.append(button('View on Forge ↗',()=>window.overlay.send('tsd-browser-open',String(entry.forgeId))));detail.append(actions);
  detail.append(node('h3','','Requirements'),node('p','browser-requirements',entry.requirements||'Check the Forge listing for current requirements.'));
  detail.append(node('p','browser-install-note','Download and install through the Forge listing, then restart the game. App Browser does not install files.'));
  detail.append(node('h3','','About this app'),node('div','browser-description',entry.description||entry.summary));
 }
 function render(){
  status.textContent=snapshot.message+(snapshot.updatedUtc?' · Checked '+new Date(snapshot.updatedUtc).toLocaleString():'');
  root.querySelector('[data-browser-refresh]').disabled=!!snapshot.busy;
  setTabletDataActivity('app-browser',!!snapshot.busy);
  const selectedEntry=snapshot.apps.find(x=>x.forgeId===selected);if(selectedEntry){showDetails(selectedEntry);return;}
  selected=null;list.hidden=false;detail.hidden=true;list.replaceChildren();
  const term=search.value.trim().toLowerCase();const entries=snapshot.apps.filter(a=>(a.name+' '+a.author+' '+a.summary).toLowerCase().includes(term));
  for(const entry of entries){
   const card=button('',()=>showDetails(entry),'browser-card');card.append(icon(entry));
   const copy=node('span','browser-card-copy');copy.append(node('strong','',entry.name),node('span','browser-byline',entry.author),node('span','browser-card-summary',entry.summary));card.append(copy);
   card.append(node('span',getInstalled(entry.appId)?'browser-installed':'browser-badge',getInstalled(entry.appId)?'Installed':'View app'));list.append(card);
  }
  if(!entries.length)list.append(node('p','browser-empty',term?'No apps match your search.':'No apps are listed yet. Check Forge for new additions.'));
 }
 search.addEventListener('input',()=>{selected=null;render();});
 root.querySelector('[data-browser-refresh]').addEventListener('click',()=>window.overlay.send('tsd-browser-refresh','force'));
 root.querySelector('[data-browser-dependents]').addEventListener('click',()=>window.overlay.send('tsd-browser-open','dependents'));
 window.overlay.on('tsd-browser-catalogue',json=>{let value;try{value=JSON.parse(json);}catch{return;}if(!Array.isArray(value.apps))return;snapshot=value;render();});
 window.tsd.refreshAppBrowser=()=>window.overlay.send('tsd-browser-refresh','cached');
 window.tsd.appBrowserInstalledChanged=render;
 render();
})();
