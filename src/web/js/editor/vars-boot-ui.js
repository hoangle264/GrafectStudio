// 
//  BOOT
// 
window.addEventListener('load', ()=>{
  document.body.classList.add('unified-vars');
  init();
  setTimeout(fitView, 200);
});
document.getElementById('modal-input').addEventListener('keydown', e=>{ if(e.key==='Enter') confirmRename(); });

// Unit modal enter
