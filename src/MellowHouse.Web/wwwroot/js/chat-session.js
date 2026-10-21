export const chatKey='mh.chat.v10';
export function readChat(storage){try{const v=JSON.parse(storage.getItem(chatKey)||'null');return v&&v.version===1&&typeof v.owner==='string'&&Array.isArray(v.entries)&&v.entries.length<=80?v:null;}catch{return null;}}
export function clearChat(storage){try{storage.removeItem(chatKey);}catch{}}
export function syncChatOwner(storage,owner){const saved=readChat(storage);if(!saved||saved.owner===owner)return false;if(saved.owner==='guest'&&owner!=='guest'){saved.owner=owner;writeChat(storage,saved);return false;}clearChat(storage);return true;}
export function writeChat(storage,data){try{storage.setItem(chatKey,JSON.stringify({...data,version:1,entries:data.entries.slice(-80)}));}catch{}}
