(() => {
  const uploadButtonLabel = 'Upload files <span class="emoji-icon" aria-hidden="true">📤</span>';

  document.addEventListener('click', async event => {
    const copy = event.target.closest('[data-copy]');
    if (copy) {
      await navigator.clipboard.writeText(copy.dataset.copy);
      const old = copy.textContent;
      copy.textContent = 'Copied ✓';
      setTimeout(() => copy.textContent = old, 1600);
    }

    const remove = event.target.closest('[data-api-delete]');
    if (remove && confirm('Permanently delete this item? This cannot be undone.')) {
      const response = await api(remove.dataset.apiDelete, { method: 'DELETE' });
      if (response.ok) location.href = remove.dataset.redirect || location.href;
      else alert(await errorText(response));
    }

    const toggle = event.target.closest('[data-admin-toggle]');
    if (toggle) {
      const response = await api(toggle.dataset.adminToggle, { method: 'PATCH', body: JSON.stringify({ [toggle.dataset.field]: toggle.dataset.value === 'True' }) });
      response.ok ? location.reload() : alert(await errorText(response));
    }

    const report = event.target.closest('[data-admin-report]');
    if (report) {
      const response = await api(report.dataset.adminReport, { method: 'PATCH', body: JSON.stringify({ status: report.dataset.status }) });
      response.ok ? location.reload() : alert(await errorText(response));
    }

    const userToggle = event.target.closest('[data-admin-user-toggle]');
    if (userToggle) {
      const response = await api(userToggle.dataset.adminUserToggle, { method: 'PATCH', body: JSON.stringify({ isBanned: userToggle.dataset.value === 'True' }) });
      response.ok ? location.reload() : alert(await errorText(response));
    }

    const role = event.target.closest('[data-admin-role]');
    if (role) {
      const value = role.closest('tr').querySelector('.user-role').value;
      const response = await api(role.dataset.adminRole, { method: 'PATCH', body: JSON.stringify({ role: value }) });
      response.ok ? location.reload() : alert(await errorText(response));
    }
  });

  document.addEventListener('pointerdown', event => {
    if (event.button !== undefined && event.button !== 0) return;
    addRipple(event);
  });

  document.addEventListener('click', event => {
    if (event.detail === 0) addRipple(event);
  });

  function enhance() {
    enhanceGreeting();

    document.querySelectorAll('[data-json-form]').forEach(form => {
      if (form.dataset.bound) return;
      form.dataset.bound = '1';
      form.addEventListener('submit', async event => {
        event.preventDefault();
        const data = {};
        form.querySelectorAll('input:not([name="__RequestVerificationToken"]), textarea, select').forEach(field => {
          if (!field.name) return;
          data[field.name] = field.type === 'checkbox' ? field.checked : field.value;
        });
        const response = await api(form.action, { method: form.dataset.method || 'PATCH', body: JSON.stringify(data) });
        if (response.ok) {
          toast('Changes saved');
          setTimeout(() => location.reload(), 600);
        } else {
          toast(await errorText(response), true);
        }
      });
    });

    const upload = document.querySelector('#upload-form');
    if (!upload || upload.dataset.bound) return;
    upload.dataset.bound = '1';

    const picker = upload.querySelector('#file-picker');
    const queue = upload.querySelector('#upload-queue');
    const result = upload.querySelector('#upload-result');
    const drop = upload.querySelector('.drop-zone');
    const button = upload.querySelector('button[type="submit"]');

    const showFiles = () => {
      queue.hidden = !picker.files.length;
      queue.innerHTML = [...picker.files].map((file, index) => `<div class="queue-file"><span class="emoji-icon">${fileIcon(file.type)}</span><div><strong>${escapeHtml(file.name)}</strong><small>${formatBytes(file.size)}</small><div class="progress"><i id="upload-progress-${index}"></i></div></div><b>Waiting</b></div>`).join('');
    };

    picker.addEventListener('change', showFiles);
    ['dragenter', 'dragover'].forEach(type => drop.addEventListener(type, event => {
      event.preventDefault();
      drop.classList.add('dragging');
    }));
    drop.addEventListener('dragleave', () => drop.classList.remove('dragging'));
    drop.addEventListener('drop', event => {
      event.preventDefault();
      drop.classList.remove('dragging');
      if (event.dataTransfer?.files.length) {
        picker.files = event.dataTransfer.files;
        showFiles();
      }
    });

    upload.addEventListener('submit', event => {
      event.preventDefault();
      if (!picker.files.length) {
        result.innerHTML = '<div class="notice error">Choose at least one file first.</div>';
        return;
      }

      const xhr = new XMLHttpRequest();
      button.disabled = true;
      button.textContent = 'Uploading…';
      result.innerHTML = '';
      xhr.open('POST', upload.action);
      xhr.setRequestHeader('X-Requested-With', 'XMLHttpRequest');
      xhr.setRequestHeader('Accept', 'application/json');
      xhr.upload.addEventListener('progress', progress => {
        const percent = progress.lengthComputable ? Math.round(progress.loaded / progress.total * 100) : 0;
        queue.querySelectorAll('.progress i').forEach(item => item.style.width = `${percent}%`);
        queue.querySelectorAll('.queue-file b').forEach(item => item.textContent = `${percent}%`);
      });
      xhr.onload = () => {
        button.disabled = false;
        button.innerHTML = uploadButtonLabel;
        if (xhr.status < 200 || xhr.status >= 300) {
          result.innerHTML = `<div class="notice error">${escapeHtml(parseError(xhr.responseText))}</div>`;
          return;
        }

        let files;
        try {
          files = JSON.parse(xhr.responseText);
        } catch {
          result.innerHTML = '<div class="notice error">The server returned an invalid upload response.</div>';
          return;
        }

        if (!Array.isArray(files) || files.length === 0) {
          result.innerHTML = '<div class="notice error">The server did not return an uploaded file.</div>';
          return;
        }

        location.href = files.length === 1
          ? `/f/${encodeURIComponent(files[0].slug)}?uploaded=1`
          : `/dashboard?uploaded=${files.length}`;
      };
      xhr.onerror = () => {
        button.disabled = false;
        button.innerHTML = uploadButtonLabel;
        result.innerHTML = '<div class="notice error">Network interrupted. The server will clean incomplete temporary files.</div>';
      };
      xhr.send(new FormData(upload));
    });
  }

  function enhanceGreeting() {
    const greeting = document.querySelector('[data-dashboard-greeting]');
    if (!greeting || greeting.dataset.greetingBound) return;
    greeting.dataset.greetingBound = '1';
    const greetings = [
      ['Hello', 'en'], ['Cześć', 'pl'], ['Hola', 'es'], ['Bonjour', 'fr'],
      ['Hallo', 'de'], ['Ciao', 'it'], ['Olá', 'pt'], ['Ahoj', 'cs'],
      ['Hej', 'sv'], ['Привіт', 'uk'], ['こんにちは', 'ja'], ['안녕하세요', 'ko']
    ];
    const key = 'm5-dashboard-greeting';
    let previous = -1;
    try { previous = Number.parseInt(sessionStorage.getItem(key) ?? '-1', 10); } catch { /* Storage can be disabled. */ }
    const offset = 1 + Math.floor(Math.random() * (greetings.length - 1));
    const index = previous >= 0 && previous < greetings.length
      ? (previous + offset) % greetings.length
      : Math.floor(Math.random() * greetings.length);
    const [text, language] = greetings[index];
    greeting.textContent = `${text}, ${greeting.dataset.name}.`;
    greeting.lang = language;
    greeting.classList.add('greeting-swap');
    try { sessionStorage.setItem(key, String(index)); } catch { /* Keep the greeting without storage. */ }
  }

  function addRipple(event) {
    const target = event.target.closest('.button, .icon-button, .nav-rail a, .bottom-nav a, .quick > a, .file-list > a, .chip-check, .rail-fab, [data-ripple]');
    if (!target || target.dataset.noRipple !== undefined) return;
    const rect = target.getBoundingClientRect();
    const size = Math.max(rect.width, rect.height);
    const keyboardClick = event.detail === 0;
    const ripple = document.createElement('span');
    ripple.className = 'ripple';
    ripple.style.width = ripple.style.height = `${size}px`;
    ripple.style.left = `${(keyboardClick ? rect.width / 2 : event.clientX - rect.left) - size / 2}px`;
    ripple.style.top = `${(keyboardClick ? rect.height / 2 : event.clientY - rect.top) - size / 2}px`;
    if (getComputedStyle(target).position === 'static') target.style.position = 'relative';
    target.appendChild(ripple);
    ripple.addEventListener('animationend', () => ripple.remove());
  }

  async function api(url, init) {
    const token = document.querySelector('input[name="__RequestVerificationToken"]')?.value;
    return fetch(url, { ...init, headers: { 'Content-Type': 'application/json', 'X-CSRF-TOKEN': token || '', ...(init.headers || {}) } });
  }

  async function errorText(response) {
    try {
      const data = await response.json();
      return data.error || data.title || 'The request could not be completed.';
    } catch {
      return 'The request could not be completed.';
    }
  }

  function parseError(value) {
    try {
      const data = JSON.parse(value);
      return data.error || data.title || 'Upload failed.';
    } catch {
      return 'Upload failed.';
    }
  }

  function toast(message, bad = false) {
    const element = document.createElement('div');
    element.className = `toast ${bad ? 'bad' : ''}`;
    element.textContent = message;
    document.body.append(element);
    requestAnimationFrame(() => element.classList.add('show'));
    setTimeout(() => element.remove(), 2400);
  }

  function escapeHtml(value) {
    const element = document.createElement('span');
    element.textContent = value;
    return element.innerHTML;
  }

  function formatBytes(value) {
    const units = ['B', 'KB', 'MB', 'GB'];
    let index = 0;
    while (value >= 1024 && index < units.length - 1) {
      value /= 1024;
      index++;
    }
    return `${value.toFixed(value < 10 && index ? 1 : 0)} ${units[index]}`;
  }

  function fileIcon(type) {
    return type.startsWith('image/') ? '🖼️' : type.startsWith('video/') ? '🎬' : type.startsWith('audio/') ? '🎵' : '📎';
  }

  document.addEventListener('m5:enhance', enhance);
  enhance();
})();
