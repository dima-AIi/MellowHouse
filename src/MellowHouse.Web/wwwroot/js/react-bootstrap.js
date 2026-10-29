// The server HTML remains readable if the compiled application cannot be downloaded.
import('/react/storefront.js?v=20261030-v26').catch(error=>{
 console.error('React storefront could not start.',error);
 const notice=document.createElement('div');notice.className='server-connection';notice.setAttribute('role','alert');
 const text=document.createElement('span');text.textContent='Не удалось загрузить интерфейс. Проверьте соединение и обновите страницу.';
 const button=document.createElement('button');button.type='button';button.textContent='Повторить';button.onclick=()=>location.reload();notice.append(text,button);document.body.append(notice);
});
