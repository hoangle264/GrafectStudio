namespace GrafcetStudioFlowStruct {
  type Project = GrafcetStudioProject.Project;
  type SharedFlowStructSchema = GrafcetStudioProject.SharedFlowStructSchema;
  type SharedFlowStructMember = GrafcetStudioProject.SharedFlowStructMember;

  export const SUPPORTED_PLC_TYPES = [
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

  export function getSharedFlowStruct(project?: Project | null): SharedFlowStructSchema {
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

  export function addMemberToSchema(
    schema: SharedFlowStructSchema,
    name: string,
    type: string = 'BOOL',
    comment: string = '',
    defaultValue: string = ''
  ): SharedFlowStructMember {
    const newMember: SharedFlowStructMember = {
      id: 'fsm-' + Date.now() + '-' + Math.random().toString(36).substr(2, 5),
      name: name.trim(),
      type: type.trim() || 'BOOL',
      comment: comment.trim(),
      defaultValue: defaultValue.trim()
    };
    schema.members.push(newMember);
    return newMember;
  }

  export function removeMemberFromSchema(schema: SharedFlowStructSchema, memberId: string): boolean {
    const idx = schema.members.findIndex(m => m.id === memberId);
    if (idx !== -1) {
      schema.members.splice(idx, 1);
      return true;
    }
    return false;
  }

  export function renderSharedFlowStructModal(project: Project, onSaveCallback?: () => void): void {
    const schema = getSharedFlowStruct(project);
    const existingModal = document.getElementById('shared-flow-struct-modal');
    if (existingModal) {
      existingModal.remove();
    }

    const modalOverlay = document.createElement('div');
    modalOverlay.id = 'shared-flow-struct-modal';
    modalOverlay.className = 'modal-bg show sfs-overlay';

    const container = document.createElement('div');
    container.className = 'sfs-container';

    // Header
    const header = document.createElement('div');
    header.className = 'sfs-header';
    header.innerHTML = `
      <h3 class="sfs-title">⚙️ Shared Flow Struct Editor (ST_&lt;FlowName&gt;)</h3>
      <button id="sfsm-close-btn" class="sfs-close">&times;</button>
    `;

    // Body
    const body = document.createElement('div');
    body.className = 'sfs-body';

    // Top controls (Enable & Struct Type Name)
    const topControls = document.createElement('div');
    topControls.className = 'sfs-top';
    topControls.innerHTML = `
      <label class="sfs-check">
        <input type="checkbox" id="sfsm-enabled-chk" ${schema.enabled !== false ? 'checked' : ''} /> Enable Shared Flow Struct
      </label>
      <div class="sfs-name-row">
        <span class="sfs-name-lbl">Struct UDT Name:</span>
        <input type="text" id="sfsm-typename-input" value="${schema.structTypeName || 'UDT_FlowData'}"  class="sfs-input" />
      </div>
    `;

    // Table header & add button
    const tableHeader = document.createElement('div');
    tableHeader.className = 'sfs-table-bar';
    tableHeader.innerHTML = `
      <span class="sfs-table-title">Struct Members (empty by default - add your own)</span>
      <button id="sfsm-add-btn" class="btn g">+ Add Member</button>
    `;

    // Members Table Container
    const tableWrapper = document.createElement('div');
    tableWrapper.className = 'sfs-table-wrap';

    const renderTable = () => {
      if (schema.members.length === 0) {
        tableWrapper.innerHTML = `
          <div class="sfs-empty">
            No members in the Struct yet. Click <strong>+ Add Member</strong> to add a variable.
          </div>
        `;
        return;
      }

      let html = `
        <table class="sfs-table">
          <thead>
            <tr>
              <th>Name</th>
              <th>Type</th>
              <th>Default Value</th>
              <th>Comment</th>
              <th class="center">Action</th>
            </tr>
          </thead>
          <tbody>
      `;

      schema.members.forEach(m => {
        html += `
          <tr>
            <td>
              <input type="text" data-field="name" data-id="${m.id}" value="${m.name}" class="sfs-input" />
            </td>
            <td>
              <select data-field="type" data-id="${m.id}" class="sfs-input">
                ${SUPPORTED_PLC_TYPES.map(t => `<option value="${t}" ${t === m.type ? 'selected' : ''}>${t}</option>`).join('')}
              </select>
            </td>
            <td>
              <input type="text" data-field="defaultValue" data-id="${m.id}" value="${m.defaultValue || ''}" class="sfs-input" />
            </td>
            <td>
              <input type="text" data-field="comment" data-id="${m.id}" value="${m.comment || ''}" class="sfs-input" />
            </td>
            <td class="center">
              <button data-action="delete" data-id="${m.id}" class="btn r btn-sm">✕</button>
            </td>
          </tr>
        `;
      });

      html += `</tbody></table>`;
      tableWrapper.innerHTML = html;

      // Bind input events
      tableWrapper.querySelectorAll('input, select').forEach(el => {
        el.addEventListener('change', (e: Event) => {
          const target = e.target as HTMLInputElement | HTMLSelectElement;
          const id = target.getAttribute('data-id');
          const field = target.getAttribute('data-field');
          const member = schema.members.find(m => m.id === id);
          if (member && field) {
            (member as any)[field] = target.value;
          }
        });
      });

      tableWrapper.querySelectorAll('button[data-action="delete"]').forEach(btn => {
        btn.addEventListener('click', (e: Event) => {
          const target = e.currentTarget as HTMLButtonElement;
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
    footer.className = 'sfs-footer';
    footer.innerHTML = `
      <button id="sfsm-cancel-btn" class="btn">Cancel</button>
      <button id="sfsm-save-btn" class="btn a">Save Changes</button>
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
    if (closeBtn) closeBtn.onclick = closeModal;
    if (cancelBtn) cancelBtn.onclick = closeModal;

    if (addBtn) {
      addBtn.onclick = () => {
        addMemberToSchema(schema, `Var_${schema.members.length + 1}`, 'BOOL', '');
        renderTable();
      };
    }

    if (saveBtn) {
      saveBtn.onclick = () => {
        const enabledChk = document.getElementById('sfsm-enabled-chk') as HTMLInputElement;
        const typeNameInput = document.getElementById('sfsm-typename-input') as HTMLInputElement;

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
}
