// The guard is transient: no private form data is written to browser storage.
export function watchForm(form){
 const snapshot=()=>JSON.stringify([...form.elements].filter(e=>['INPUT','SELECT','TEXTAREA'].includes(e.tagName)).map(e=>[e.name,e.type,e.type==='file'?[...e.files].map(f=>[f.name,f.size,f.lastModified]):e.value,e.checked]));
 let baseline=snapshot();queueMicrotask(()=>{baseline=snapshot();});
 return()=>snapshot()!==baseline;
}
