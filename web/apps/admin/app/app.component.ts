import { Component, OnInit, inject } from '@angular/core';
import { RouterOutlet } from '@angular/router';
import { AttachmentQueueService } from './attachment-queue.service';
import { ThemeService } from './theme.service';

@Component({
  selector: 'app-root',
  standalone: true,
  imports: [RouterOutlet],
  template: `<router-outlet />`,
  styles: []
})
export class AppComponent implements OnInit {
  private attachments = inject(AttachmentQueueService);
  private theme = inject(ThemeService);

  ngOnInit(): void {
    void this.theme;
    if ('serviceWorker' in navigator) {
      navigator.serviceWorker.register(new URL('sw.js', document.baseURI).href).then(reg => {
        // Pick up SW fixes immediately instead of waiting for the next tab close.
        reg.update().catch(() => undefined);
        if (reg.waiting) reg.waiting.postMessage({ type: 'SKIP_WAITING' });
      }).catch(() => undefined);
      navigator.serviceWorker.addEventListener('controllerchange', () => {
        if (!sessionStorage.getItem('sw-reloaded')) {
          sessionStorage.setItem('sw-reloaded', '1');
          location.reload();
        }
      });
    }
    window.addEventListener('online', () => void this.attachments.flush());
    if (navigator.onLine) void this.attachments.flush();
  }
}
