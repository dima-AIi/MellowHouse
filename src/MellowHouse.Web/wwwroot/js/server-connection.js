// Retry startup reads, or the dedicated browser-cart sync protected by a server receipt.
// Ordinary cart mutations, checkout and payments must never use this retry loop.
export async function readWhenReady(read,{timeout=180000,delay=2500,now=Date.now,sleep=ms=>new Promise(r=>setTimeout(r,ms)),onWaiting=()=>{}}={}){
 const start=now();let last;
 while(now()-start<timeout){
  try{return await read();}catch(error){
   last=error;
   if(error.status&&![408,425,429,502,503,504].includes(error.status)&&error.status<500)throw error;
   onWaiting(error);if(now()-start>=timeout)break;await sleep(Math.min(delay,timeout-(now()-start)));
  }
 }
 throw last??new Error('Не удалось подключиться к кафе.');
}
export function isJsonResponse(response){return(response.headers.get('content-type')??'').toLowerCase().includes('application/json');}
