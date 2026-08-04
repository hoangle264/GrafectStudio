"use strict";
var GrafcetStudioFlowStruct;
(function (GrafcetStudioFlowStruct) {
    GrafcetStudioFlowStruct.SUPPORTED_PLC_TYPES = [
        'BOOL',
        'INT',
        'DINT',
        'UINT',
        'UDINT',
        'REAL',
        'LREAL',
        'TIME',
        'STRING',
        'BYTE',
        'WORD',
        'DWORD',
        'POS',
        'TOOL'
    ];
    function getSharedFlowStruct(project) {
        if (!project) {
            return { enabled: true, structTypeName: 'UDT_FlowData', members: [] };
        }
        if (!project.sharedFlowStruct) {
            project.sharedFlowStruct = { enabled: true, structTypeName: 'UDT_FlowData', members: [] };
        }
        if (!Array.isArray(project.sharedFlowStruct.members)) {
            project.sharedFlowStruct.members = [];
        }
        return project.sharedFlowStruct;
    }
    GrafcetStudioFlowStruct.getSharedFlowStruct = getSharedFlowStruct;
    function addMemberToSchema(schema, name, type = 'BOOL', comment = '', defaultValue = '') {
        const newMember = {
            id: 'fsm-' + Date.now() + '-' + Math.random().toString(36).substr(2, 5),
            name: name.trim(),
            type: type.trim() || 'BOOL',
            comment: comment.trim(),
            defaultValue: defaultValue.trim()
        };
        schema.members.push(newMember);
        return newMember;
    }
    GrafcetStudioFlowStruct.addMemberToSchema = addMemberToSchema;
    function removeMemberFromSchema(schema, memberId) {
        const idx = schema.members.findIndex(m => m.id === memberId);
        if (idx !== -1) {
            schema.members.splice(idx, 1);
            return true;
        }
        return false;
    }
    GrafcetStudioFlowStruct.removeMemberFromSchema = removeMemberFromSchema;
    function renderSharedFlowStructModal(project, onSaveCallback) {
        const schema = getSharedFlowStruct(project);
        const existingModal = document.getElementById('shared-flow-struct-modal');
        if (existingModal) {
            existingModal.remove();
        }
        const modalOverlay = document.createElement('div');
        modalOverlay.id = 'shared-flow-struct-modal';
        modalOverlay.style.cssText = `
      position: fixed; top: 0; left: 0; width: 100vw; height: 100vh;
      background: rgba(0,0,0,0.6); display: flex; align-items: center; justify-content: center;
      z-index: 10000; font-family: sans-serif;
    `;
        const container = document.createElement('div');
        container.style.cssText = `
      background: #1e1e2e; color: #cdd6f4; width: 720px; max-width: 90vw; max-height: 85vh;
      border-radius: 8px; border: 1px solid #45475a; display: flex; flex-direction: column;
      box-shadow: 0 8px 32px rgba(0,0,0,0.5); overflow: hidden;
    `;
        // Header
        const header = document.createElement('div');
        header.style.cssText = `
      padding: 16px 20px; background: #181825; border-bottom: 1px solid #313244;
      display: flex; justify-content: space-between; align-items: center;
    `;
        header.innerHTML = `
      <h3 style="margin:0; font-size: 1.1rem; color: #cba6f7;">⚙️ Shared Flow Struct Editor (ST_&lt;FlowName&gt;)</h3>
      <button id="sfsm-close-btn" style="background:none; border:none; color:#a6adc8; font-size:1.4rem; cursor:pointer;">&times;</button>
    `;
        // Body
        const body = document.createElement('div');
        body.style.cssText = `padding: 20px; overflow-y: auto; flex: 1; display: flex; flex-direction: column; gap: 16px;`;
        // Top controls (Enable & Struct Type Name)
        const topControls = document.createElement('div');
        topControls.style.cssText = `display: flex; gap: 20px; align-items: center; background: #313244; padding: 12px 16px; border-radius: 6px;`;
        topControls.innerHTML = `
      <label style="display:flex; align-items:center; gap:8px; cursor:pointer; font-weight:600;">
        <input type="checkbox" id="sfsm-enabled-chk" ${schema.enabled !== false ? 'checked' : ''} /> Enable Shared Flow Struct
      </label>
      <div style="display:flex; align-items:center; gap:8px; flex:1;">
        <span style="font-size:0.9rem; color:#a6adc8;">Struct UDT Name:</span>
        <input type="text" id="sfsm-typename-input" value="${schema.structTypeName || 'UDT_FlowData'}" 
          style="background:#181825; color:#cdd6f4; border:1px solid #45475a; padding:6px 10px; border-radius:4px; flex:1;" />
      </div>
    `;
        // Table header & add button
        const tableHeader = document.createElement('div');
        tableHeader.style.cssText = `display:flex; justify-content:space-between; align-items:center;`;
        tableHeader.innerHTML = `
      <span style="font-weight:600; color:#89b4fa;">Struct Members (Mặc định trống - Người dùng tự thêm)</span>
      <button id="sfsm-add-btn" style="background:#a6e3a1; color:#11111b; border:none; padding:6px 14px; border-radius:4px; font-weight:600; cursor:pointer;">+ Add Member</button>
    `;
        // Members Table Container
        const tableWrapper = document.createElement('div');
        tableWrapper.style.cssText = `border: 1px solid #313244; border-radius: 6px; overflow: hidden; background: #181825;`;
        const renderTable = () => {
            if (schema.members.length === 0) {
                tableWrapper.innerHTML = `
          <div style="padding: 30px; text-align: center; color: #6c7086; font-style: italic;">
            Chưa có member nào trong Struct. Bấm <strong>+ Add Member</strong> để thêm biến mới.
          </div>
        `;
                return;
            }
            let html = `
        <table style="width:100%; border-collapse:collapse; text-align:left; font-size:0.9rem;">
          <thead>
            <tr style="background:#313244; color:#a6adc8;">
              <th style="padding:8px 12px;">Name</th>
              <th style="padding:8px 12px;">Type</th>
              <th style="padding:8px 12px;">Default Value</th>
              <th style="padding:8px 12px;">Comment</th>
              <th style="padding:8px 12px; text-align:center;">Action</th>
            </tr>
          </thead>
          <tbody>
      `;
            schema.members.forEach(m => {
                html += `
          <tr style="border-bottom: 1px solid #313244;">
            <td style="padding:6px 12px;">
              <input type="text" data-field="name" data-id="${m.id}" value="${m.name}" style="width:100%; background:#181825; color:#cdd6f4; border:1px solid #45475a; padding:4px 8px; border-radius:4px;" />
            </td>
            <td style="padding:6px 12px;">
              <select data-field="type" data-id="${m.id}" style="width:100%; background:#181825; color:#cdd6f4; border:1px solid #45475a; padding:4px 8px; border-radius:4px;">
                ${GrafcetStudioFlowStruct.SUPPORTED_PLC_TYPES.map(t => `<option value="${t}" ${t === m.type ? 'selected' : ''}>${t}</option>`).join('')}
              </select>
            </td>
            <td style="padding:6px 12px;">
              <input type="text" data-field="defaultValue" data-id="${m.id}" value="${m.defaultValue || ''}" style="width:100%; background:#181825; color:#cdd6f4; border:1px solid #45475a; padding:4px 8px; border-radius:4px;" />
            </td>
            <td style="padding:6px 12px;">
              <input type="text" data-field="comment" data-id="${m.id}" value="${m.comment || ''}" style="width:100%; background:#181825; color:#cdd6f4; border:1px solid #45475a; padding:4px 8px; border-radius:4px;" />
            </td>
            <td style="padding:6px 12px; text-align:center;">
              <button data-action="delete" data-id="${m.id}" style="background:#f38ba8; color:#11111b; border:none; padding:4px 10px; border-radius:4px; cursor:pointer; font-weight:bold;">✕</button>
            </td>
          </tr>
        `;
            });
            html += `</tbody></table>`;
            tableWrapper.innerHTML = html;
            // Bind input events
            tableWrapper.querySelectorAll('input, select').forEach(el => {
                el.addEventListener('change', (e) => {
                    const target = e.target;
                    const id = target.getAttribute('data-id');
                    const field = target.getAttribute('data-field');
                    const member = schema.members.find(m => m.id === id);
                    if (member && field) {
                        member[field] = target.value;
                    }
                });
            });
            tableWrapper.querySelectorAll('button[data-action="delete"]').forEach(btn => {
                btn.addEventListener('click', (e) => {
                    const target = e.currentTarget;
                    const id = target.getAttribute('data-id');
                    if (id) {
                        removeMemberFromSchema(schema, id);
                        renderTable();
                    }
                });
            });
        };
        body.appendChild(topControls);
        body.appendChild(tableHeader);
        body.appendChild(tableWrapper);
        renderTable();
        // Footer
        const footer = document.createElement('div');
        footer.style.cssText = `
      padding: 16px 20px; background: #181825; border-top: 1px solid #313244;
      display: flex; justify-content: flex-end; gap: 12px;
    `;
        footer.innerHTML = `
      <button id="sfsm-cancel-btn" style="background:#45475a; color:#cdd6f4; border:none; padding:8px 16px; border-radius:4px; cursor:pointer;">Cancel</button>
      <button id="sfsm-save-btn" style="background:#89b4fa; color:#11111b; border:none; padding:8px 20px; border-radius:4px; font-weight:600; cursor:pointer;">Save Changes</button>
    `;
        container.appendChild(header);
        container.appendChild(body);
        container.appendChild(footer);
        modalOverlay.appendChild(container);
        document.body.appendChild(modalOverlay);
        // Event handlers
        const closeBtn = document.getElementById('sfsm-close-btn');
        const cancelBtn = document.getElementById('sfsm-cancel-btn');
        const saveBtn = document.getElementById('sfsm-save-btn');
        const addBtn = document.getElementById('sfsm-add-btn');
        const closeModal = () => modalOverlay.remove();
        if (closeBtn)
            closeBtn.onclick = closeModal;
        if (cancelBtn)
            cancelBtn.onclick = closeModal;
        if (addBtn) {
            addBtn.onclick = () => {
                addMemberToSchema(schema, `Var_${schema.members.length + 1}`, 'BOOL', '');
                renderTable();
            };
        }
        if (saveBtn) {
            saveBtn.onclick = () => {
                const enabledChk = document.getElementById('sfsm-enabled-chk');
                const typeNameInput = document.getElementById('sfsm-typename-input');
                schema.enabled = enabledChk ? enabledChk.checked : true;
                schema.structTypeName = typeNameInput && typeNameInput.value.trim() ? typeNameInput.value.trim() : 'UDT_FlowData';
                project.sharedFlowStruct = schema;
                if (typeof onSaveCallback === 'function') {
                    onSaveCallback();
                }
                closeModal();
            };
        }
    }
    GrafcetStudioFlowStruct.renderSharedFlowStructModal = renderSharedFlowStructModal;
})(GrafcetStudioFlowStruct || (GrafcetStudioFlowStruct = {}));
