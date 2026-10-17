import { api } from '../api.js';
import { bindCards, esc, icon, videoCard } from '../ui.js';

export async function render(root, { query, isCurrent }) {
  const initial = query.get('q') ?? '';
  root.innerHTML = `
    <div class="page">
      <label class="search-box" style="margin-bottom:24px">${icon('search')}
        <input type="search" placeholder="O que você quer assistir?" value="${esc(initial)}" autofocus>
      </label>
      <div data-results></div>
    </div>`;

  const input = root.querySelector('input');
  const results = root.querySelector('[data-results]');
  bindCards(results);

  let timer;
  let lastToken = 0;
  const run = async (term) => {
    const token = ++lastToken;
    const videos = await api.videos.list({ search: term });
    if (!isCurrent() || token !== lastToken) return;
    results.innerHTML = videos.length
      ? `<div class="section-head"><h2>${term ? 'Resultados' : 'Navegar por tudo'}</h2></div><div class="grid">${videos.map(videoCard).join('')}</div>`
      : `<div class="empty-state"><h3>Nada encontrado para "${esc(term)}"</h3><p>Confira a ortografia ou tente outra palavra.</p></div>`;
  };

  input.addEventListener('input', () => {
    clearTimeout(timer);
    timer = setTimeout(() => {
      history.replaceState(null, '', `#/search${input.value ? `?q=${encodeURIComponent(input.value)}` : ''}`);
      run(input.value.trim());
    }, 250);
  });
  input.focus();
  await run(initial);
  return () => clearTimeout(timer);
}
