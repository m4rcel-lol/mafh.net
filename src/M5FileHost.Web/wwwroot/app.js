(() => {
  const greeting = document.querySelector('[data-dashboard-greeting]');
  if (greeting) {
    const greetings = [
      ['Hello', 'en'], ['Cześć', 'pl'], ['Hola', 'es'], ['Bonjour', 'fr'],
      ['Hallo', 'de'], ['Ciao', 'it'], ['Olá', 'pt'], ['Ahoj', 'cs'],
      ['Hej', 'sv'], ['Привіт', 'uk'], ['こんにちは', 'ja'], ['안녕하세요', 'ko']
    ];
    const key = 'm5-dashboard-greeting';
    let previous = -1;
    try { previous = Number.parseInt(sessionStorage.getItem(key) ?? '-1', 10); } catch { /* Storage can be disabled. */ }
    const offset = 1 + Math.floor(Math.random() * (greetings.length - 1));
    const index = previous >= 0 && previous < greetings.length ? (previous + offset) % greetings.length : Math.floor(Math.random() * greetings.length);
    const [text, language] = greetings[index];
    greeting.textContent = `${text}, ${greeting.dataset.name}.`;
    greeting.lang = language;
    greeting.classList.add('greeting-swap');
    try { sessionStorage.setItem(key, String(index)); } catch { /* Keep the greeting even without storage. */ }
  }

  document.addEventListener('click', async event => {
    const interactive = event.target.closest('.button, .icon-button, .nav-rail a, .bottom-nav a, .quick a');
    if (interactive) {
      const bounds = interactive.getBoundingClientRect();
      const keyboardClick = event.detail === 0;
      interactive.style.setProperty('--ripple-x', `${keyboardClick ? bounds.width / 2 : event.clientX - bounds.left}px`);
      interactive.style.setProperty('--ripple-y', `${keyboardClick ? bounds.height / 2 : event.clientY - bounds.top}px`);
      interactive.classList.remove('material-ripple');
      void interactive.offsetWidth;
      interactive.classList.add('material-ripple');
      setTimeout(() => interactive.classList.remove('material-ripple'), 520);
    }
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
    upload.addEventListener('submit', event => { event.preventDefault(); if (!picker.files.length) return; const xhr = new XMLHttpRequest(); const button = upload.querySelector('button[type="submit"]'); button.disabled = true; button.textContent = 'Uploading…'; xhr.open('POST', upload.action); xhr.upload.addEventListener('progress', e => { const percent = e.lengthComputable ? Math.round(e.loaded / e.total * 100) : 0; queue.querySelectorAll('.progress i').forEach(i => i.style.width = `${percent}%`); queue.querySelectorAll('.queue-file b').forEach(b => b.textContent = `${percent}%`); }); xhr.onload = () => { button.disabled = false; button.innerHTML = 'Upload files <span class="emoji-icon" aria-hidden="true">📤</span>'; if (xhr.status >= 200 && xhr.status < 300) { const files = JSON.parse(xhr.responseText); result.innerHTML = `<div class="notice success"><strong>${files.length} file${files.length === 1 ? '' : 's'} safely uploaded.</strong>${files.map(f => `<a href="/f/${f.slug}">${escapeHtml(f.name)} →</a>`).join('')}</div>`; queue.querySelectorAll('.queue-file b').forEach(b => { b.textContent = 'Queued ✓'; b.className = 'done'; }); } else { result.innerHTML = `<div class="notice error">${escapeHtml(parseError(xhr.responseText))}</div>`; } }; xhr.onerror = () => { button.disabled = false; button.innerHTML = 'Upload files <span class="emoji-icon" aria-hidden="true">📤</span>'; result.innerHTML = '<div class="notice error">Network interrupted. The server will clean incomplete temporary files.</div>'; }; xhr.send(new FormData(upload)); });
  }

  async function api(url, init) { const token = document.querySelector('input[name="__RequestVerificationToken"]')?.value; return fetch(url, { ...init, headers: { 'Content-Type': 'application/json', 'X-CSRF-TOKEN': token || '', ...(init.headers || {}) } }); }
  async function errorText(response) { try { const data = await response.json(); return data.error || data.title || 'The request could not be completed.'; } catch { return 'The request could not be completed.'; } }
  function parseError(text) { try { const data = JSON.parse(text); return data.error || data.title || 'Upload failed.'; } catch { return 'Upload failed.'; } }
  function toast(message, bad = false) { const el = document.createElement('div'); el.className = `toast ${bad ? 'bad' : ''}`; el.textContent = message; document.body.append(el); requestAnimationFrame(() => el.classList.add('show')); setTimeout(() => el.remove(), 2400); }
  function escapeHtml(value) { const el = document.createElement('span'); el.textContent = value; return el.innerHTML; }
  function formatBytes(value) { const units = ['B','KB','MB','GB']; let i=0; while(value >= 1024 && i<units.length-1){value/=1024;i++;} return `${value.toFixed(value < 10 && i ? 1 : 0)} ${units[i]}`; }
  function fileIcon(type) { return type.startsWith('image/') ? '🖼️' : type.startsWith('video/') ? '🎬' : type.startsWith('audio/') ? '🎵' : '📎'; }
})();
