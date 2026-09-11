(() => {
  const script = document.currentScript as HTMLScriptElement | null;
  if (!script) return;

  const targetSel = script.getAttribute('data-target') || '#hastbokning';
  const target = document.querySelector(targetSel);
  if (!target) {
    console.warn('HastBokning: target not found', targetSel);
    return;
  }

  const src = new URL(script.src);
  const origin = src.origin;
  const treatment = script.getAttribute('data-treatment') || '';
  const lang = script.getAttribute('data-lang') || 'sv';
  const params = new URLSearchParams();
  if (treatment) params.set('treatment', treatment);
  if (lang) params.set('lang', lang);

  const iframe = document.createElement('iframe');
  iframe.src = `${origin}/widget/${params.toString() ? '?' + params.toString() : ''}`;
  iframe.title = 'Boka behandling';
  iframe.style.cssText = 'width:100%;border:0;display:block;min-height:320px;';
  iframe.setAttribute('scrolling', 'no');
  target.appendChild(iframe);

  window.addEventListener('message', (event: MessageEvent) => {
    if (event.origin !== origin) return;
    const data = event.data as { type?: string; height?: number } | null;
    if (!data || data.type !== 'hastbokning:resize' || typeof data.height !== 'number') return;
    iframe.style.height = `${Math.max(320, data.height)}px`;
  });

  (window as unknown as { HastBokning: { version: string } }).HastBokning = { version: '1' };
})();
