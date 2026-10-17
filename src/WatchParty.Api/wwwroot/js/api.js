// Thin wrapper around the REST API. Errors come back as ProblemDetails; `title` holds the user-facing message.

export class ApiError extends Error {
  constructor(status, message, problem = {}) {
    super(message);
    this.status = status;
    this.problem = problem;
  }
}

async function request(method, url, body) {
  const options = { method, credentials: 'same-origin', headers: {} };
  if (body !== undefined) {
    options.headers['Content-Type'] = 'application/json';
    options.body = JSON.stringify(body);
  }

  let response;
  try {
    response = await fetch(url, options);
  } catch {
    throw new ApiError(0, 'Sem conexão com o servidor.');
  }

  if (response.status === 401 && !url.startsWith('/api/auth/')) {
    window.dispatchEvent(new Event('auth:expired'));
  }

  const text = await response.text();
  const data = text ? safeJson(text) : null;

  if (!response.ok) {
    const message = response.status === 429
      ? 'Muitas tentativas. Espere um minuto.'
      : data?.title || 'Algo deu errado.';
    throw new ApiError(response.status, message, data ?? {});
  }
  return data;
}

function safeJson(text) {
  try { return JSON.parse(text); } catch { return null; }
}

const get = (url) => request('GET', url);
const post = (url, body) => request('POST', url, body ?? {});
const put = (url, body) => request('PUT', url, body);
const del = (url) => request('DELETE', url);
const qs = (params) => {
  const s = new URLSearchParams(Object.entries(params).filter(([, v]) => v !== undefined && v !== null && v !== '')).toString();
  return s ? `?${s}` : '';
};

/** Uploads with XMLHttpRequest because fetch has no upload progress. Fields go before the file (the server streams). */
function upload(file, { title, description }, onProgress) {
  return new Promise((resolve, reject) => {
    const form = new FormData();
    if (title) form.append('title', title);
    if (description) form.append('description', description);
    form.append('file', file, file.name);

    const xhr = new XMLHttpRequest();
    xhr.open('POST', '/api/videos');
    xhr.upload.onprogress = (e) => e.lengthComputable && onProgress?.(e.loaded / e.total);
    xhr.onload = () => {
      const data = safeJson(xhr.responseText);
      if (xhr.status >= 200 && xhr.status < 300) resolve(data);
      else reject(new ApiError(xhr.status, data?.title || 'Falha no envio.', data ?? {}));
    };
    xhr.onerror = () => reject(new ApiError(0, 'A conexão caiu durante o envio.'));
    xhr.send(form);
    upload.abort = () => xhr.abort();
  });
}

export const api = {
  me: () => get('/api/auth/me'),
  login: (userName, password) => post('/api/auth/login', { userName, password }),
  register: (data) => post('/api/auth/register', data),
  logout: () => post('/api/auth/logout'),
  config: () => get('/api/config'),

  videos: {
    list: (params = {}) => get(`/api/videos${qs(params)}`),
    get: (id) => get(`/api/videos/${id}`),
    update: (id, data) => put(`/api/videos/${id}`, data),
    remove: (id) => del(`/api/videos/${id}`),
    toggleFavorite: (id) => post(`/api/videos/${id}/favorite`),
    upload,
    streamUrl: (id) => `/api/videos/${id}/stream`,
    thumbUrl: (id) => `/api/videos/${id}/thumbnail`,
  },

  playlists: {
    list: () => get('/api/playlists'),
    get: (id) => get(`/api/playlists/${id}`),
    create: (data) => post('/api/playlists', data),
    update: (id, data) => put(`/api/playlists/${id}`, data),
    remove: (id) => del(`/api/playlists/${id}`),
    addVideo: (id, videoId) => post(`/api/playlists/${id}/videos`, { videoId }),
    removeVideo: (id, videoId) => del(`/api/playlists/${id}/videos/${videoId}`),
  },

  rooms: {
    list: () => get('/api/rooms'),
    get: (code) => get(`/api/rooms/${encodeURIComponent(code)}`),
    create: (data) => post('/api/rooms', data),
    remove: (code) => del(`/api/rooms/${encodeURIComponent(code)}`),
  },
};
