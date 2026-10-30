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
        'DWORD'
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
    function escapeHtml(value) {
        return String(value ?? '').replace(/&/g, '&amp;').replace(/</g, '&lt;').replace(/>/g, '&gt;').replace(/"/g, '&quot;');
    }
    // Same dialog layout as the Struct Data editor (dlg-* / dev-sig-*). Edits a copy; nothing changes until Save.
    function renderSharedFlowStructModal(project, onSaveCallback) {
        const schema = JSON.parse(JSON.stringify(getSharedFlowStruct(project)));
        document.getElementById('shared-flow-struct-modal')?.remove();
        const overlay = document.createElement('div');
        overlay.id = 'shared-flow-struct-modal';
        overlay.className = 'modal-bg show';
        overlay.innerHTML = `
      <div class="modal dlg-modal dlg-w-800">
        <div class="dlg-head">
          <span class="dlg-head-title">Shared Flow Struct</span>
          <span class="dlg-spacer"></span>
          <span class="sfs-system-tag">System</span>
        </div>
        <div class="dlg-row">
          <div class="dlg-col-grow">
            <div class="dev-field-lbl">Struct type name</div>
            <input id="sfsm-typename-input" type="text" class="dlg-input" placeholder="UDT_FlowData" value="${escapeHtml(schema.structTypeName || 'UDT_FlowData')}">
          </div>
          <div class="dlg-col-fixed">
            <div class="dev-field-lbl">Status</div>
            <label class="sfs-enable">
              <input id="sfsm-enabled-chk" type="checkbox" ${schema.enabled !== false ? 'checked' : ''}> Enabled
            </label>
          </div>
        </div>
        <div class="sfs-hint">Every flow gets a variable <b>ST_&lt;FlowName&gt;</b> of this type in the Variable table.</div>
        <div class="dlg-section-bar">
          <span class="dlg-section-title">Members</span>
          <button id="sfsm-add-btn" class="btn btn-cyan btn-sm">+ Add Member</button>
        </div>
        <div class="dlg-scroll">
          <table class="dev-sig-table">
            <thead><tr>
              <th class="col-name">Member name</th>
              <th class="col-var">Data type</th>
              <th class="col-addr">Default value</th>
              <th>Comment</th>
              <th class="col-del"></th>
            </tr></thead>
            <tbody id="sfsm-tbody"></tbody>
          </table>
        </div>
        <div class="dlg-foot">
          <button id="sfsm-cancel-btn" class="btn">Cancel</button>
          <button id="sfsm-save-btn" class="btn a">Save</button>
        </div>
      </div>`;
        document.body.appendChild(overlay);
        const tbody = overlay.querySelector('#sfsm-tbody');
        const renderTable = () => {
            if (!schema.members.length) {
                tbody.innerHTML = '<tr><td colspan="5" class="sfs-empty">No members yet. Click <b>+ Add Member</b>.</td></tr>';
                return;
            }
            tbody.innerHTML = schema.members.map(m => `
        <tr data-id="${escapeHtml(m.id)}">
          <td><input class="dev-sig-input" data-field="name" placeholder="Busy" value="${escapeHtml(m.name)}"></td>
          <td>
            <select class="dev-sig-select" data-field="type">
              ${GrafcetStudioFlowStruct.SUPPORTED_PLC_TYPES.map(t => `<option value="${t}" ${t === m.type ? 'selected' : ''}>${t}</option>`).join('')}
            </select>
          </td>
          <td><input class="dev-sig-input is-addr" data-field="defaultValue" placeholder="FALSE" value="${escapeHtml(m.defaultValue || '')}"></td>
          <td><input class="dev-sig-input is-comment" data-field="comment" placeholder="e.g. Flow is running" value="${escapeHtml(m.comment || '')}"></td>
          <td><button class="dev-del-row" data-action="delete" title="Remove">✕</button></td>
        </tr>`).join('');
        };
        renderTable();
        tbody.addEventListener('input', (e) => {
            const target = e.target;
            const field = target.getAttribute('data-field');
            const member = schema.members.find(m => m.id === target.closest('tr')?.getAttribute('data-id'));
            if (member && field)
                member[field] = target.value;
        });
        tbody.addEventListener('click', (e) => {
            const button = e.target.closest('[data-action="delete"]');
            const id = button?.closest('tr')?.getAttribute('data-id');
            if (id && removeMemberFromSchema(schema, id))
                renderTable();
        });
        const close = () => overlay.remove();
        overlay.querySelector('#sfsm-cancel-btn').onclick = close;
        overlay.querySelector('#sfsm-add-btn').onclick = () => {
            addMemberToSchema(schema, `Var_${schema.members.length + 1}`, 'BOOL', '');
            renderTable();
            tbody.querySelector('tr:last-child input')?.select();
        };
        overlay.querySelector('#sfsm-save-btn').onclick = () => {
            const typeName = overlay.querySelector('#sfsm-typename-input').value.trim();
            const members = schema.members.filter(m => String(m.name || '').trim());
            members.forEach(m => { m.name = m.name.trim(); });
            const seen = new Set();
            const duplicates = members.map(m => m.name).filter(name => seen.has(name.toLowerCase()) || !seen.add(name.toLowerCase()));
            if (duplicates.length) {
                notifyError('Duplicate member name is not allowed: ' + Array.from(new Set(duplicates)).join(', '));
                return;
            }
            schema.enabled = overlay.querySelector('#sfsm-enabled-chk').checked;
            schema.structTypeName = typeName || 'UDT_FlowData';
            schema.members = members;
            project.sharedFlowStruct = schema;
            if (typeof onSaveCallback === 'function')
                onSaveCallback();
            close();
        };
        setTimeout(() => overlay.querySelector('#sfsm-typename-input')?.focus(), 80);
    }
    GrafcetStudioFlowStruct.renderSharedFlowStructModal = renderSharedFlowStructModal;
})(GrafcetStudioFlowStruct || (GrafcetStudioFlowStruct = {}));
