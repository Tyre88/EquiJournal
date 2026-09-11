// App-shell cache for offline use. Never cache hashed JS chunks — that breaks login after deploys.
const CACHE = 'equine-admin-v2';

self.addEventListener('install', () => {
  self.skipWaiting();
});

self.addEventListener('message', event => {
  if (event.data?.type === 'SKIP_WAITING') self.skipWaiting();
});

self.addEventListener('activate', event => {
  event.waitUntil(
    caches.keys().then(keys =>
      Promise.all(keys.filter(k => k !== CACHE).map(k => caches.delete(k)))
    ).then(() => self.clients.claim())
  );
});

function shouldBypass(url, request) {
  if (url.pathname.startsWith('/api/')) return true;
  // Hashed bundles and lazy chunks must always come from the network.
  if (url.pathname.endsWith('.js') || url.pathname.endsWith('.map')) return true;
  if (url.pathname.includes('chunk-')) return true;
  if (url.search.includes('import')) return true;
  return false;
}

self.addEventListener('fetch', event => {
  const { request } = event;
  if (request.method !== 'GET') return;

  const url = new URL(request.url);
  if (url.origin !== self.location.origin) return;
  if (shouldBypass(url, request)) return;

  // HTML navigations: network-first, cache for offline fallback only.
  if (request.mode === 'navigate' || url.pathname === '/' || url.pathname === '/index.html') {
    event.respondWith(
      fetch(request)
        .then(response => {
          if (response.ok) {
            const clone = response.clone();
            caches.open(CACHE).then(cache => cache.put('/index.html', clone));
          }
          return response;
        })
        .catch(() => caches.match('/index.html'))
    );
    return;
  }

  // Other static assets (CSS, fonts): network-first with cache update.
  event.respondWith(
    fetch(request)
      .then(response => {
        if (response.ok) {
          const clone = response.clone();
          caches.open(CACHE).then(cache => cache.put(request, clone));
        }
        return response;
      })
      .catch(() => caches.match(request))
  );
});
