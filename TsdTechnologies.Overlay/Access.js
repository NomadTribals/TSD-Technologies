(() => {
 'use strict';
 const root=document.getElementById('access-view');
 const list=root.querySelector('[data-access-list]'),detail=root.querySelector('[data-access-detail]');
 const name=root.querySelector('[data-access-name]'),status=root.querySelector('[data-access-status]');
 const bar=root.querySelector('[data-access-bar]'),percent=root.querySelector('[data-access-percent]');
 const start=root.querySelector('[data-access-start]'),cancel=root.querySelector('[data-access-cancel]');
 const steps=[...root.querySelectorAll('[data-access-step]')];
 let snapshot={doors:[],busy:false,phase:'idle',progress:0,range:5,duration:10},selected=null,visible=false,lastStage=-1,lastPhase='idle',listSignature='';
 function send(channel,payload=''){window.overlay.send(channel,payload);}
 function render(){
  const s=snapshot;root.dataset.phase=s.phase;root.classList.toggle('access-busy',!!s.busy);
  setTabletDataActivity('native/access',visible&&!!s.busy);
  root.querySelector('[data-access-range]').textContent=Number(s.range).toFixed(1)+' m';
  root.querySelector('[data-access-duration]').textContent=Number(s.duration)+' s';
  root.querySelector('[data-access-scan]').disabled=!!s.busy;
  if(status.textContent!==(s.message||'Ready to scan.'))status.textContent=s.message||'Ready to scan.';
  const target=s.doors.find(d=>d.id===selected);
  if(s.busy)selected=s.targetId;
  const active=s.busy||selected===s.targetId;
  detail.hidden=!selected;
  const signature=JSON.stringify([s.doors,selected,s.busy]);
  if(signature!==listSignature){listSignature=signature;list.replaceChildren();
  for(const door of s.doors){
   const b=document.createElement('button');b.type='button';b.className='access-reader'+(door.id===selected?' selected':'');b.disabled=!!s.busy;
   const title=document.createElement('strong');title.textContent=door.name;
   const meta=document.createElement('span');meta.textContent=Number(door.distance).toFixed(1)+' m · '+(door.available?'LOCKED / READY':door.state.toUpperCase());
   b.append(title,meta);b.addEventListener('click',()=>{selected=door.id;render();playTabletSound('tab');});list.append(b);
  }
  if(!s.doors.length){const empty=document.createElement('p');empty.className='access-empty';empty.textContent='No local controllers detected. Stand near a keycard reader and scan.';list.append(empty);}
  }
  name.textContent=(s.busy?s.targetName:target?.name)||s.targetName||'Select a reader';
  root.querySelector('[data-access-distance]').textContent=target?Number(target.distance).toFixed(1)+' m · LOCAL LINK':'LOCAL LINK';
  const progress=active?Math.max(0,Math.min(100,Number(s.progress)||0)):0;
  bar.style.width=progress+'%';bar.parentElement.setAttribute('aria-valuenow',String(Math.round(progress)));percent.textContent=String(Math.floor(progress)).padStart(2,'0')+'%';
  const granted=active&&s.phase==='success'&&progress===100;
  bar.parentElement.hidden=granted;percent.hidden=granted;root.querySelector('[data-access-granted]').hidden=!granted;
  start.disabled=!!s.busy||!target?.available;start.textContent=s.busy?'BYPASS IN PROGRESS':'START HACK';
  cancel.hidden=!s.busy;cancel.disabled=!!s.committing;
  const stage=s.phase==='success'&&active?5:s.busy?Math.min(4,Math.floor(progress/20)):-1;
  steps.forEach((el,i)=>{el.hidden=stage<0||i>stage;el.classList.toggle('done',i<stage);el.classList.toggle('current',i===stage);el.querySelector('span').textContent=i<stage?'OK':i===stage?'RUN':'—';});
  if(visible&&s.busy&&stage!==lastStage)playTabletSound('data-write');
  if(visible&&s.phase==='success'&&lastPhase!=='success')playTabletSound('submit');
  lastStage=stage;lastPhase=s.phase;
 }
 root.querySelector('[data-access-scan]').addEventListener('click',()=>{selected=null;send('tsd-access-scan');});
 start.addEventListener('click',()=>{if(selected&&!snapshot.busy)send('tsd-access-start',selected);});
 cancel.addEventListener('click',()=>send('tsd-access-cancel'));
 window.overlay.on('tsd-access-state',json=>{let s;try{s=JSON.parse(json);}catch{return;}if(!Array.isArray(s.doors))return;snapshot=s;render();});
 window.tsd.setAccessVisible=value=>{visible=!!value;send('tsd-access-visible',String(visible));if(!visible)setTabletDataActivity('native/access',false);};
 const setVisible=window.tsd.setTabletVisible;
 window.tsd.setTabletVisible=function(value){setVisible?.(value);window.tsd.setAccessVisible(!!value&&currentTabletView==='access');};
 render();
})();
