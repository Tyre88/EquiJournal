import { afterNextRender, Component, ElementRef, inject } from '@angular/core';
import { RouterOutlet } from '@angular/router';

@Component({
  selector: 'app-root',
  standalone: true,
  imports: [RouterOutlet],
  template: `<router-outlet />`
})
export class AppComponent {
  private host = inject(ElementRef<HTMLElement>);

  constructor() {
    afterNextRender(() => {
      const notify = () => {
        const height = Math.max(document.documentElement.scrollHeight, this.host.nativeElement.scrollHeight, 320);
        if (window.parent !== window) {
          window.parent.postMessage({ type: 'hastbokning:resize', height }, '*');
        }
      };
      notify();
      const observer = new ResizeObserver(notify);
      observer.observe(document.documentElement);
    });
  }
}
