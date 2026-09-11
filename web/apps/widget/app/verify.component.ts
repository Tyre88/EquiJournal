import { Component, OnInit, signal } from '@angular/core';
import { ActivatedRoute } from '@angular/router';
import { publicApi, PublicApiError } from './public-api';

@Component({
  selector: 'app-verify',
  standalone: true,
  template: `
    <div class="wrap">
      <h1>Bekräfta bokning</h1>
      @if (state() === 'working') { <p>Bekräftar…</p> }
      @if (state() === 'ok') { <p>Tack, din e-post är bekräftad. Du får ett mejl när tiden är klar.</p> }
      @if (state() === 'fail') { <p class="alert" role="alert">Länken är ogiltig eller har gått ut. Boka gärna igen.</p> }
    </div>
  `
})
export class VerifyComponent implements OnInit {
  state = signal<'working' | 'ok' | 'fail'>('working');

  constructor(private route: ActivatedRoute) {}

  async ngOnInit(): Promise<void> {
    const token = this.route.snapshot.queryParamMap.get('token');
    if (!token) { this.state.set('fail'); return; }
    try {
      await publicApi.verify(token);
      this.state.set('ok');
    } catch (err) {
      this.state.set(err instanceof PublicApiError ? 'fail' : 'fail');
    }
  }
}
