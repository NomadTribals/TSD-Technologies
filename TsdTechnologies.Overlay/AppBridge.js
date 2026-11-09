// Field OS injects this bridge into every registered app before its scripts run.
(()=>{
 const handlers=new Map();let deviceKey='F9';
 const post=(kind,extra={})=>window.parent.postMessage({tabletApp:true,kind,...extra},'*');
 const api=window.tabletApp={
  send:(channel,payload)=>post('send',{channel,payload:String(payload??'')}),
  on:(channel,handler)=>handlers.set(channel,handler),
  activity:(operation,active)=>post('activity',{operation,active}),
  sound:sound=>post('sound',{sound}),page:page=>post('page',{page}),
  setDeviceKey:key=>deviceKey=key,
  setTheme:color=>window.dispatchEvent(new CustomEvent('tablet-theme',{detail:color}))
 };
 window.addEventListener('message',event=>{
  if(event.source!==window.parent||!event.data?.tabletHost)return;const m=event.data;
  if(m.kind==='script'){try{(0,eval)(m.script)}catch(error){console.error('App update failed',error)}}
  else if(m.kind==='event')handlers.get(m.channel)?.(m.payload);
  else if(m.kind==='visibility')api.onVisibility?.(m.visible);
  else if(m.kind==='open-page')api.openPage?.(m.page);
 });
    function deviceKeyName(event) {
        const code = event.code;
        if (/^Key[A-Z]$/.test(code)) return code.slice(3);
        if (/^Digit[0-9]$/.test(code)) return 'Alpha' + code.slice(5);
        if (/^Numpad[0-9]$/.test(code)) return 'Keypad' + code.slice(6);
        if (/^F([1-9]|1[0-2])$/.test(code)) return code;
        const names = {Escape:'Escape',Space:'Space',Tab:'Tab',Enter:'Return',Backspace:'Backspace',Delete:'Delete',Insert:'Insert',Home:'Home',End:'End',PageUp:'PageUp',PageDown:'PageDown',ArrowUp:'UpArrow',ArrowDown:'DownArrow',ArrowLeft:'LeftArrow',ArrowRight:'RightArrow',ControlLeft:'LeftControl',ControlRight:'RightControl',ShiftLeft:'LeftShift',ShiftRight:'RightShift',AltLeft:'LeftAlt',AltRight:'RightAlt',Minus:'Minus',Equal:'Equals',BracketLeft:'LeftBracket',BracketRight:'RightBracket',Backslash:'Backslash',Semicolon:'Semicolon',Quote:'Quote',Backquote:'BackQuote',Comma:'Comma',Period:'Period',Slash:'Slash',NumpadAdd:'KeypadPlus',NumpadSubtract:'KeypadMinus',NumpadMultiply:'KeypadMultiply',NumpadDivide:'KeypadDivide',NumpadDecimal:'KeypadPeriod'};
        return names[code] || null;
    }
 document.addEventListener('keydown',event=>{
  if(event.repeat||event.ctrlKey||event.altKey||event.metaKey||!event.key||event.key.length!==1||event.target?.closest?.('input,textarea,select,[contenteditable]'))return;
  const key=deviceKeyName(event);
  if(key===deviceKey){event.preventDefault();event.stopPropagation();post('device-close')}
 });
 document.addEventListener('pointerdown',()=>api.sound('screen-tap'),true);
 document.addEventListener('click',event=>{if(event.target.closest('button,input,select'))api.sound('function')},true);
 document.addEventListener('DOMContentLoaded',()=>post('ready'),{once:true});
})();
