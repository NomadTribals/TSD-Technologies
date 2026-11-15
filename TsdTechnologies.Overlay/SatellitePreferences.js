// TSD viewer preferences survive the bridge's changing loopback address.
// Satellite's installed files and ordinary external-browser settings stay untouched.
(()=>{
 const mini=__MINI__,initial=JSON.parse(decodeURIComponent(escape(atob('__PREFERENCES__'))));
 let current=initial;
 let writes=Promise.resolve();
 const persist=value=>{if(mini)return;writes=writes.catch(()=>{}).then(async()=>{
  const response=await fetch('/tsd/preferences',{method:'POST',headers:{'Content-Type':'application/json','X-TSD-Preferences':'__PREFERENCES_TOKEN__'},body:value});
  if(!response.ok)throw Error('Map settings could not be saved');
 }).catch(error=>console.warn('TSD map preferences:',error));};
 // Inline tablet pages have an opaque top origin. Chromium can refuse even the
 // localStorage getter in their child frames, before a Storage method override runs.
 let original;try{original=window.localStorage;}catch{}
 const fallback=new Map();
 const storage={getItem(key){
  if(key!=='livemap'){try{return original?original.getItem(key):fallback.get(key)??null;}catch{return fallback.get(key)??null;}}
  return JSON.stringify(mini?Object.assign({},current,{follow:true,panel:false,side:false,zoom:0.08}):current);
 },setItem(key,value){
  if(key!=='livemap'){fallback.set(key,String(value));try{original?.setItem(key,value);}catch{}return;}
  try{const parsed=JSON.parse(value);if(!parsed||typeof parsed!=='object'||Array.isArray(parsed))return;current=parsed;
   persist(String(value));
  }catch{}
 },removeItem(key){
  if(key!=='livemap'){fallback.delete(key);try{original?.removeItem(key);}catch{}return;}
  current={};persist('{}');
 },clear(){current={};fallback.clear();persist('{}');},key(index){return index===0?'livemap':Array.from(fallback.keys())[index-1]??null;},get length(){return fallback.size+1;}};
 Object.defineProperty(window,'localStorage',{configurable:true,get:()=>storage});
})();
