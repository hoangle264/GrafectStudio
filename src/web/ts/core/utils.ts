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
type ToastKind = 'info' | 'success' | 'warn' | 'error';
interface ToastOptions {
  kind?: ToastKind;
  /** ms before auto-hide; errors and toasts with an action stay longer by default */
  duration?: number;
  action?: { label: string; run: () => void };
}
let toastTimer: ReturnType<typeof setTimeout>;
function toast(msg: string, opts: ToastOptions = {}): void {
  const old=document.querySelector('.toast'); if(old) old.remove();
  const kind: ToastKind = opts.kind || 'success';
  const el=document.createElement('div'); el.className='toast toast-'+kind;
  el.setAttribute('role', kind==='error' ? 'alert' : 'status');
  const text=document.createElement('span'); text.className='toast-msg'; text.textContent=msg;
  el.appendChild(text);
  const hideToast=(): void => { clearTimeout(toastTimer); el.style.opacity='0'; setTimeout(()=>el.remove(),300); };
  if (opts.action) {
    const action=opts.action;
    const btn=document.createElement('button'); btn.className='toast-action'; btn.textContent=action.label;
    btn.onclick=()=>{ hideToast(); action.run(); };
    el.appendChild(btn);
  }
  if (kind==='error' || opts.action) {
    const close=document.createElement('button'); close.className='toast-close'; close.textContent='×'; close.title='Close';
    close.onclick=hideToast;
    el.appendChild(close);
  }
  document.body.appendChild(el);
  clearTimeout(toastTimer);
  const duration = opts.duration ?? (kind==='error' ? 8000 : opts.action ? 7000 : 2500);
  toastTimer=setTimeout(hideToast, duration);
}

/** Non-blocking replacement for alert() on failures. */
function notifyError(msg: string): void { toast(msg, { kind: 'error' }); }

// ── Address helpers ─────────────────────────────────────────
interface ParsedAddressBase {
  prefix: string;
  number: number;
  width: number;
}

/** Parse a base address such as "MR100" (empty value => defaultBase). Throws on malformed input. */
function parseAddressBase(value: unknown, defaultBase: string): ParsedAddressBase {
  const source = String(value == null || value === '' ? defaultBase : value).trim();
  const match = source.match(/^([A-Za-z]+)(\d+)$/);
  if (!match) throw new Error('Invalid base address: ' + value);
  return { prefix: match[1].toUpperCase(), number: Number(match[2]), width: match[2].length };
}
