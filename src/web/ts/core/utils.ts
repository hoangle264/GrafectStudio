// ═══════════════════════════════════════════════════════════
//  utils.ts — Grafcet Studio
//  Pure helper functions shared by all modules.
//  Must be loaded BEFORE grafcet-studio-v2.js and grafcet-codegen.js.
// ═══════════════════════════════════════════════════════════

// ── HTML escape ─────────────────────────────────────────────
/** Escape &, <, >, " for safe HTML attribute/text output. */
function esc2(s: unknown): string { return String(s).replace(/&/g,'&amp;').replace(/</g,'&lt;').replace(/>/g,'&gt;').replace(/"/g,'&quot;'); }

/** Escape &, <, > for safe HTML text content. */
function esc(s: unknown): string { return String(s).replace(/&/g,'&amp;').replace(/</g,'&lt;').replace(/>/g,'&gt;'); }

// ── DOM helpers ─────────────────────────────────────────────
function show(id: string): void { (document.getElementById(id) as HTMLElement).style.display='block'; }
function hide(id: string): void { (document.getElementById(id) as HTMLElement).style.display='none'; }
function closeModal(id: string): void { (document.getElementById(id) as HTMLElement).classList.remove('show'); }

// ── Toast notifications ─────────────────────────────────────
let toastTimer: ReturnType<typeof setTimeout>;
function toast(msg: string): void {
  const old=document.querySelector('.toast'); if(old) old.remove();
  const el=document.createElement('div'); el.className='toast'; el.textContent=msg;
  document.body.appendChild(el);
  clearTimeout(toastTimer);
  toastTimer=setTimeout(()=>{ el.style.opacity='0'; setTimeout(()=>el.remove(),300); },2500);
}
