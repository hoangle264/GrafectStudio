"use strict";

// ═══════════════════════════════════════════════════════════
//  CANVAS CONTEXT MENU
//  Right-click menu for steps, transitions, parallels on canvas.
//  Shows "Open Flowchart" shortcut for macro steps.
// ═══════════════════════════════════════════════════════════

(function () {
  let _menuEl = null;

  // ── Create or reuse the menu DOM element ──
  function _getOrCreateMenu() {
    if (_menuEl) return _menuEl;
    _menuEl = document.createElement('ul');
    _menuEl.id = 'canvas-ctx-menu';
    _menuEl.className = 'ctx-menu';
    document.body.appendChild(_menuEl);

    // Close on outside click or Escape
    document.addEventListener('mousedown', _handleOutsideClick, true);
    document.addEventListener('keydown', _handleKey, true);
    return _menuEl;
  }

  function _handleOutsideClick(e) {
    if (_menuEl && !_menuEl.contains(e.target)) hideContextMenu();
  }

  function _handleKey(e) {
    if (e.key === 'Escape') hideContextMenu();
  }

  // ── Build menu items for a step ──
  function _buildStepItems(step) {
    const items = [];

    // "Open Flowchart" — only for macro steps
    if (step.kind === 'macro') {
      const hasMacroFlow = !!step.macroFlowId;
      items.push({
        icon: '📂',
        label: 'Open Flowchart',
        disabled: !hasMacroFlow,
        title: hasMacroFlow ? '' : 'Macro step has no flowchart assigned',
        action: () => {
          if (hasMacroFlow) openTab(step.macroFlowId);
        }
      });
      items.push({ separator: true });
    }

    items.push({
      icon: '⤓',
      label: 'Add Transition + Step below (N)',
      action: () => quickAppend(step.id)
    });

    items.push({
      icon: '✏',
      label: 'Properties',
      action: () => {
        selectEl(step.id, 'step', null);
      }
    });

    items.push({
      icon: '🗑',
      label: 'Delete',
      danger: true,
      action: () => {
        selectEl(step.id, 'step', null);
        deleteEl(step.id);
        notifyDeleted(1);
        selIds.clear();
        updateProps();
      }
    });

    return items;
  }

  // ── Build menu items for a transition ──
  function _buildTransitionItems(transition) {
    return [
      {
        icon: '⤓',
        label: 'Add Step below (N)',
        action: () => quickAppend(transition.id)
      },
      {
        icon: '✏',
        label: 'Properties',
        action: () => selectEl(transition.id, 'transition', null)
      },
      {
        icon: '🗑',
        label: 'Delete',
        danger: true,
        action: () => {
          selectEl(transition.id, 'transition', null);
          deleteEl(transition.id);
          notifyDeleted(1);
          selIds.clear();
          updateProps();
        }
      }
    ];
  }

  // ── Render items into the menu ──
  function _renderMenu(items, x, y) {
    const menu = _getOrCreateMenu();
    menu.innerHTML = '';

    items.forEach(item => {
      if (item.separator) {
        const sep = document.createElement('li');
        sep.className = 'ctx-menu-sep';
        menu.appendChild(sep);
        return;
      }

      const li = document.createElement('li');
      li.className = 'ctx-menu-item' +
        (item.disabled ? ' disabled' : '') +
        (item.danger ? ' danger' : '');
      if (item.title) li.title = item.title;

      li.innerHTML =
        `<span class="ctx-menu-icon">${item.icon || ''}</span>` +
        `<span class="ctx-menu-label">${item.label}</span>`;

      if (!item.disabled) {
        li.addEventListener('mousedown', e => {
          e.stopPropagation();
          hideContextMenu();
          item.action();
        });
      }
      menu.appendChild(li);
    });

    // Position — show at cursor, flip if near viewport edge
    menu.style.display = 'block';
    menu.style.left = '0px';
    menu.style.top = '0px';

    const mw = menu.offsetWidth;
    const mh = menu.offsetHeight;
    const vw = window.innerWidth;
    const vh = window.innerHeight;

    const left = (x + mw > vw) ? Math.max(0, x - mw) : x;
    const top  = (y + mh > vh) ? Math.max(0, y - mh) : y;

    menu.style.left = left + 'px';
    menu.style.top  = top  + 'px';
  }

  // ── Public API ──

  /** Show context menu for a step element. */
  window.showStepContextMenu = function (mouseEvent, step) {
    mouseEvent.preventDefault();
    mouseEvent.stopPropagation();
    const items = _buildStepItems(step);
    _renderMenu(items, mouseEvent.clientX, mouseEvent.clientY);
  };

  /** Show context menu for a transition element. */
  window.showTransitionContextMenu = function (mouseEvent, transition) {
    mouseEvent.preventDefault();
    mouseEvent.stopPropagation();
    const items = _buildTransitionItems(transition);
    _renderMenu(items, mouseEvent.clientX, mouseEvent.clientY);
  };

  /** Hide and clear the context menu. */
  window.hideContextMenu = function () {
    if (_menuEl) _menuEl.style.display = 'none';
  };
})();
