// Keep the server-rendered interface usable if a browser cannot load the React bundle.
import('/react/storefront.js?v=20261029-v25').catch(error=>{
 console.error('React storefront could not start; using the existing interface.',error);
 delete window.mhReact;
 document.documentElement.dataset.uiRuntime='compatibility';
 return import('/js/site.js?v=20261029-react25');
});
