import { api } from '../api.js';
import { esc, formatBytes, icon, toast } from '../ui.js';

const ACCEPT = '.mp4,.m4v,.mov,.webm,.mkv,.avi,.ts,.wmv,.flv,video/*';

export async function render(root) {
  root.innerHTML = `
    <div class="page">
      <h1 class="page-title">Enviar vídeos</h1>
      <label class="dropzone" data-drop>
        ${icon('upload')}
        <h3>Arraste filmes e vídeos para cá</h3>
        <p>ou clique para escolher • MP4, MKV, MOV, WEBM, AVI… Convertemos automaticamente para tocar em qualquer aparelho.</p>
        <input type="file" accept="${ACCEPT}" multiple hidden>
      </label>
      <div class="upload-list" data-list></div>
    </div>`;

  const drop = root.querySelector('[data-drop]');
  const input = drop.querySelector('input');
  const list = root.querySelector('[data-list]');
  const queue = [];
  let busy = false;
  let leaving = false;

  const addFiles = (files) => {
    for (const file of files) {
      const item = { file, el: document.createElement('div') };
      item.el.className = 'upload-item';
      item.el.innerHTML = `
        <div class="row">
          <input class="input" value="${esc(file.name.replace(/\.[^.]+$/, ''))}" maxlength="200" aria-label="Título">
          <button class="icon-btn" title="Remover">${icon('close')}</button>
        </div>
        <div class="progress"><div></div></div>
        <div class="status">${esc(file.name)} • ${formatBytes(file.size)} • na fila</div>`;
      item.el.querySelector('button').onclick = () => {
        if (item.started) return;
        queue.splice(queue.indexOf(item), 1);
        item.el.remove();
      };
      list.appendChild(item.el);
      queue.push(item);
    }
    processQueue();
  };

  const processQueue = async () => {
    if (busy) return;
    busy = true;
    while (queue.length && !leaving) {
      const item = queue.shift();
      item.started = true;
      const status = item.el.querySelector('.status');
      const bar = item.el.querySelector('.progress > div');
      const title = item.el.querySelector('input');
      title.disabled = true;
      item.el.querySelector('button').remove();
      try {
        const started = Date.now();
        const { id } = await api.videos.upload(item.file, { title: title.value.trim() }, (p) => {
          bar.style.width = `${(p * 100).toFixed(1)}%`;
          const speed = (item.file.size * p) / Math.max(1, (Date.now() - started) / 1000);
          status.textContent = `${Math.round(p * 100)}% • ${formatBytes(speed)}/s`;
        });
        bar.style.width = '100%';
        status.innerHTML = `Enviado! Convertendo no servidor… <a href="#/video/${id}" style="color:var(--accent);font-weight:700">Ver vídeo</a>`;
        window.dispatchEvent(new Event('library:changed'));
      } catch (err) {
        bar.style.background = 'var(--danger)';
        status.textContent = err.message;
        if (!leaving) toast(err.message, 'error');
      }
    }
    busy = false;
  };

  input.addEventListener('change', () => { addFiles(input.files); input.value = ''; });
  drop.addEventListener('dragover', (e) => { e.preventDefault(); drop.classList.add('over'); });
  drop.addEventListener('dragleave', () => drop.classList.remove('over'));
  drop.addEventListener('drop', (e) => {
    e.preventDefault();
    drop.classList.remove('over');
    addFiles([...e.dataTransfer.files].filter((f) => f.type.startsWith('video/') || /\.(mkv|ts|avi|wmv|flv)$/i.test(f.name)));
  });

  const warn = (e) => { if (busy) { e.preventDefault(); e.returnValue = ''; } };
  window.addEventListener('beforeunload', warn);
  return () => {
    leaving = true;
    window.removeEventListener('beforeunload', warn);
    if (busy) { api.videos.upload.abort?.(); toast('Envio cancelado: você saiu da página.', 'error'); }
  };
}
