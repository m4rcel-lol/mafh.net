(() => {
  const root = document.documentElement;
  const saved = localStorage.getItem('m5-theme');
  root.dataset.theme = saved || (matchMedia('(prefers-color-scheme: dark)').matches ? 'dark' : 'light');
  document.addEventListener('click', async event => {
    const theme = event.target.closest('[data-theme-toggle]');
    if (theme) { root.dataset.theme = root.dataset.theme === 'dark' ? 'light' : 'dark'; localStorage.setItem('m5-theme', root.dataset.theme); }
    const copy = event.target.closest('[data-copy]');
    if (copy) { await navigator.clipboard.writeText(copy.dataset.copy); const old = copy.textContent; copy.textContent = 'Copied ✓'; setTimeout(() => copy.textContent = old, 1600); }
    const remove = event.target.closest('[data-api-delete]');
    if (remove && confirm('Permanently delete this item? This cannot be undone.')) { const response = await api(remove.dataset.apiDelete, { method: 'DELETE' }); if (response.ok) location.href = remove.dataset.redirect || location.href; else alert(await errorText(response)); }
    const toggle = event.target.closest('[data-admin-toggle]');
    if (toggle) { const response = await api(toggle.dataset.adminToggle, { method: 'PATCH', body: JSON.stringify({ [toggle.dataset.field]: toggle.dataset.value === 'True' }) }); response.ok ? location.reload() : alert(await errorText(response)); }
    const report = event.target.closest('[data-admin-report]');
    if (report) { const response = await api(report.dataset.adminReport, { method: 'PATCH', body: JSON.stringify({ status: report.dataset.status }) }); response.ok ? location.reload() : alert(await errorText(response)); }
    const userToggle = event.target.closest('[data-admin-user-toggle]');
    if (userToggle) { const response = await api(userToggle.dataset.adminUserToggle, { method: 'PATCH', body: JSON.stringify({ isBanned: userToggle.dataset.value === 'True' }) }); response.ok ? location.reload() : alert(await errorText(response)); }
    const role = event.target.closest('[data-admin-role]');
    if (role) { const value = role.closest('tr').querySelector('.user-role').value; const response = await api(role.dataset.adminRole, { method: 'PATCH', body: JSON.stringify({ role: value }) }); response.ok ? location.reload() : alert(await errorText(response)); }
  });

  document.querySelectorAll('[data-json-form]').forEach(form => form.addEventListener('submit', async event => {
    event.preventDefault(); const data = {};
    form.querySelectorAll('input:not([name="__RequestVerificationToken"]), textarea, select').forEach(field => { if (!field.name) return; data[field.name] = field.type === 'checkbox' ? field.checked : field.value; });
    const response = await api(form.action, { method: form.dataset.method || 'PATCH', body: JSON.stringify(data) });
    if (response.ok) { toast('Changes saved'); setTimeout(() => location.reload(), 600); } else toast(await errorText(response), true);
  }));

  const upload = document.querySelector('#upload-form');
  if (upload) {
    const picker = upload.querySelector('#file-picker'), queue = upload.querySelector('#upload-queue'), result = upload.querySelector('#upload-result'), drop = upload.querySelector('.drop-zone');
    const showFiles = () => { queue.hidden = !picker.files.length; queue.innerHTML = [...picker.files].map((file, i) => `<div class="queue-file"><span>${fileIcon(file.type)}</span><div><strong>${escapeHtml(file.name)}</strong><small>${formatBytes(file.size)}</small><div class="progress"><i id="upload-progress-${i}"></i></div></div><b>Waiting</b></div>`).join(''); };
    picker.addEventListener('change', showFiles);
    ['dragenter', 'dragover'].forEach(type => drop.addEventListener(type, () => drop.classList.add('dragging')));
    ['dragleave', 'drop'].forEach(type => drop.addEventListener(type, () => drop.classList.remove('dragging')));
    upload.addEventListener('submit', event => { event.preventDefault(); if (!picker.files.length) return; const xhr = new XMLHttpRequest(); const button = upload.querySelector('button[type="submit"]'); button.disabled = true; button.textContent = 'Uploading…'; xhr.open('POST', upload.action); xhr.upload.addEventListener('progress', e => { const percent = e.lengthComputable ? Math.round(e.loaded / e.total * 100) : 0; queue.querySelectorAll('.progress i').forEach(i => i.style.width = `${percent}%`); queue.querySelectorAll('.queue-file b').forEach(b => b.textContent = `${percent}%`); }); xhr.onload = () => { button.disabled = false; button.textContent = 'Upload and optimize ↑'; if (xhr.status >= 200 && xhr.status < 300) { const files = JSON.parse(xhr.responseText); result.innerHTML = `<div class="notice success"><strong>${files.length} file${files.length === 1 ? '' : 's'} safely uploaded.</strong>${files.map(f => `<a href="/f/${f.slug}">${escapeHtml(f.name)} →</a>`).join('')}</div>`; queue.querySelectorAll('.queue-file b').forEach(b => { b.textContent = 'Queued ✓'; b.className = 'done'; }); } else { result.innerHTML = `<div class="notice error">${escapeHtml(parseError(xhr.responseText))}</div>`; } }; xhr.onerror = () => { button.disabled = false; result.innerHTML = '<div class="notice error">Network interrupted. The server will clean incomplete temporary files.</div>'; }; xhr.send(new FormData(upload)); });
  }

  async function api(url, init) { const token = document.querySelector('input[name="__RequestVerificationToken"]')?.value; return fetch(url, { ...init, headers: { 'Content-Type': 'application/json', 'X-CSRF-TOKEN': token || '', ...(init.headers || {}) } }); }
  async function errorText(response) { try { const data = await response.json(); return data.error || data.title || 'The request could not be completed.'; } catch { return 'The request could not be completed.'; } }
  function parseError(text) { try { const data = JSON.parse(text); return data.error || data.title || 'Upload failed.'; } catch { return 'Upload failed.'; } }
  function toast(message, bad = false) { const el = document.createElement('div'); el.className = `toast ${bad ? 'bad' : ''}`; el.textContent = message; document.body.append(el); requestAnimationFrame(() => el.classList.add('show')); setTimeout(() => el.remove(), 2400); }
  function escapeHtml(value) { const el = document.createElement('span'); el.textContent = value; return el.innerHTML; }
  function formatBytes(value) { const units = ['B','KB','MB','GB']; let i=0; while(value >= 1024 && i<units.length-1){value/=1024;i++;} return `${value.toFixed(value < 10 && i ? 1 : 0)} ${units[i]}`; }
  function fileIcon(type) { return type.startsWith('image/') ? '◈' : type.startsWith('video/') ? '▶' : type.startsWith('audio/') ? '♫' : '◇'; }
})();
