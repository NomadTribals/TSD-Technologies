// Forward only tablet HUD navigation to Satellite's existing keyboard controls.
(()=>{
 let panning=false,reduced=true;
 const nativeRatio=window.devicePixelRatio||1;
 // Only this embedded minimap uses a lighter canvas backing store. CSS dimensions stay native.
 try{Object.defineProperty(window,'devicePixelRatio',{configurable:true,get:()=>reduced?Math.min(nativeRatio,.7):nativeRatio});}catch{}
 let viewportWidth=innerWidth,viewportHeight=innerHeight,viewportRatio=window.devicePixelRatio;
 window.addEventListener('resize',event=>{
  const width=innerWidth,height=innerHeight,ratio=window.devicePixelRatio;
  if(width===viewportWidth&&height===viewportHeight&&ratio===viewportRatio){event.stopImmediatePropagation();return;}
  viewportWidth=width;viewportHeight=height;viewportRatio=ratio;
 },true);
 const key=value=>window.dispatchEvent(new KeyboardEvent('keydown',{key:value,bubbles:true}));
 window.addEventListener('message',event=>{
  if(event.source!==parent||!event.data?.tsdMiniControl)return;
  if(event.data.tsdMiniControl==='quality'){const next=event.data.reduced===true;if(next!==reduced){reduced=next;window.dispatchEvent(new Event('resize'));}return;}
  if(event.data.tsdMiniControl==='follow'){panning=false;key(';');return;}
  if(event.data.tsdMiniControl==='zoom'){
   const delta=Number(event.data.delta),stage=document.getElementById('stage');
   if(!stage||!Number.isFinite(delta)||!delta)return;
   const rect=stage.getBoundingClientRect();
   stage.dispatchEvent(new WheelEvent('wheel',{deltaY:-Math.max(-4,Math.min(4,delta))*100,deltaMode:0,shiftKey:true,clientX:rect.left+rect.width/2,clientY:rect.top+rect.height/2,bubbles:true,cancelable:true}));return;
  }
  if(event.data.tsdMiniControl!=='pan'||!Array.isArray(event.data.keys))return;
  const keys=event.data.keys.filter(k=>['ArrowLeft','ArrowRight','ArrowUp','ArrowDown'].includes(k));
  if(!keys.length)return;
  if(!panning){key(';');document.getElementById('follow')?.click();panning=true;}
  keys.forEach(key);
 });
})();
