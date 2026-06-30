import { ApiClient } from './types';

const BASE_URL = import.meta.env.VITE_API_BASE_URL || '';

// The antiforgery token is initially embedded in the index.html <meta> tag by
// the server, but it is bound to the current identity, so it must be refreshed
// whenever the auth state changes (login / register / logout).
let csrfToken: string | null = null;

const getCsrfToken = () => {
  if (csrfToken) return csrfToken;
  const meta = document.querySelector('meta[name="csrf-token"]');
  return meta ? meta.getAttribute('content') : '';
};

async function refreshCsrf() {
  try {
    const res = await fetch(`${BASE_URL}/api/auth/csrf`, {
      credentials: 'include',
      headers: { 'X-Requested-With': 'XMLHttpRequest', Accept: 'application/json' },
    });
    if (res.ok) {
      const data = await res.json();
      if (data && data.token) csrfToken = data.token;
    }
  } catch {
    // Ignore; the existing token may still be valid.
  }
}

async function fetchApi(endpoint: string, options: RequestInit = {}) {
  const url = `${BASE_URL}${endpoint}`;
  const headers = new Headers(options.headers || {});

  headers.set('X-Requested-With', 'XMLHttpRequest');

  if (options.method && options.method !== 'GET' && options.method !== 'HEAD') {
    const csrf = getCsrfToken();
    if (csrf) headers.set('X-CSRF-TOKEN', csrf);
  }

  // Auto set content-type for JSON if not FormData
  if (!(options.body instanceof FormData) && options.body && !headers.has('Content-Type')) {
    headers.set('Content-Type', 'application/json');
  }

  if (!headers.has('Accept')) {
    headers.set('Accept', 'application/json');
  }

  const response = await fetch(url, {
    ...options,
    headers,
    credentials: 'include', // Important for cookies
  });

  if (!response.ok) {
    let message = response.statusText;
    try {
      const errorData = await response.json();
      message = errorData.message || errorData.title || message;
    } catch {
      // Ignore JSON parse error on non-JSON error responses
    }
    const error = new Error(message);
    (error as any).status = response.status;
    throw error;
  }

  if (response.status === 204) return null;

  const contentType = response.headers.get('content-type');
  if (contentType && contentType.includes('application/json')) {
    return response.json();
  }

  return response.text();
}

export const backendClient: ApiClient = {
  register: async (data) => { await fetchApi('/api/auth/register', { method: 'POST', body: JSON.stringify(data) }); await refreshCsrf(); },
  login: async (data) => { const user = await fetchApi('/api/auth/login', { method: 'POST', body: JSON.stringify(data) }); await refreshCsrf(); return user; },
  logout: async () => { await fetchApi('/api/auth/logout', { method: 'POST' }); await refreshCsrf(); },
  getMe: () => fetchApi('/api/auth/me'),
  forgotPassword: (data) => fetchApi('/api/auth/forgot-password', { method: 'POST', body: JSON.stringify(data) }),
  resetPassword: (data) => fetchApi('/api/auth/reset-password', { method: 'POST', body: JSON.stringify(data) }),

  getUploads: (params) => {
    const qs = new URLSearchParams(params).toString();
    return fetchApi(`/api/uploads?${qs}`);
  },
  uploadFiles: (files, visibility, tags, isNsfw, onProgress) => {
    return new Promise((resolve, reject) => {
      const xhr = new XMLHttpRequest();
      const url = `${BASE_URL}/api/uploads`;

      xhr.open('POST', url, true);
      xhr.withCredentials = true;
      xhr.setRequestHeader('Accept', 'application/json');
      xhr.setRequestHeader('X-Requested-With', 'XMLHttpRequest');

      const csrf = getCsrfToken();
      if (csrf) xhr.setRequestHeader('X-CSRF-TOKEN', csrf);

      if (onProgress) {
        xhr.upload.onprogress = (e) => {
          if (e.lengthComputable) {
            onProgress((e.loaded / e.total) * 100);
          }
        };
      }

      xhr.onload = () => {
        if (xhr.status >= 200 && xhr.status < 300) {
          try {
            resolve(JSON.parse(xhr.responseText));
          } catch {
            resolve(xhr.responseText as any);
          }
        } else {
          let msg = xhr.statusText;
          try {
            msg = JSON.parse(xhr.responseText).message || msg;
          } catch {}
          const err = new Error(msg);
          (err as any).status = xhr.status;
          reject(err);
        }
      };

      xhr.onerror = () => reject(new Error('Network error during upload'));

      const formData = new FormData();
      files.forEach(f => formData.append('Files', f));
      formData.append('Visibility', visibility);
      formData.append('Tags', tags);
      formData.append('IsNsfw', isNsfw.toString());

      xhr.send(formData);
    });
  },
  getFile: (slug) => fetchApi(`/api/uploads/${slug}`),
  updateFile: (id, data) => fetchApi(`/api/uploads/${id}`, { method: 'PATCH', body: JSON.stringify(data) }),
  deleteFile: (id) => fetchApi(`/api/uploads/${id}`, { method: 'DELETE' }),
  reportFile: (id, reason) => fetchApi(`/api/uploads/${id}/report`, { method: 'POST', body: JSON.stringify({ reason }) }),

  getUser: (username) => fetchApi(`/api/users/${username}`),
  updateMe: (data) => fetchApi('/api/users/me', { method: 'PATCH', body: JSON.stringify(data) }),
  updatePassword: (data) => fetchApi('/api/users/me/password', { method: 'POST', body: JSON.stringify(data) }),
  updateAvatar: (file) => {
    const formData = new FormData();
    formData.append('file', file);
    return fetchApi('/api/users/me/avatar', { method: 'POST', body: formData });
  },

  getAdminStats: () => fetchApi('/api/admin/stats'),
  getAdminFiles: (params) => {
    const qs = new URLSearchParams(params).toString();
    return fetchApi(`/api/admin/files?${qs}`);
  },
  getAdminUsers: (params) => {
    const qs = new URLSearchParams(params).toString();
    return fetchApi(`/api/admin/users?${qs}`);
  },
  getAdminReports: (params) => {
    const qs = new URLSearchParams(params).toString();
    return fetchApi(`/api/admin/reports?${qs}`);
  },
  getAdminAuditLog: (params) => {
    const qs = new URLSearchParams(params).toString();
    return fetchApi(`/api/admin/audit?${qs}`);
  },
  updateUserRole: (id, role) => fetchApi(`/api/admin/users/${id}/role`, { method: 'PATCH', body: JSON.stringify({ role }) }),
  suspendUser: (id, isBanned) => fetchApi(`/api/admin/users/${id}/suspend`, { method: 'PATCH', body: JSON.stringify({ isBanned }) }),
  resolveReport: (id, action) => fetchApi(`/api/admin/reports/${id}/resolve`, { method: 'POST', body: JSON.stringify({ action }) })
};
