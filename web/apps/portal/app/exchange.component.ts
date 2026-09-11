import { Component, OnInit, inject } from '@angular/core';
import { ActivatedRoute, Router } from '@angular/router';
import { HttpClient } from '@angular/common/http';
import { environment } from '../environments/environment';

@Component({
  selector: 'app-exchange',
  standalone: true,
  template: `<div class="page"><p>Loggar in…</p></div>`
})
export class ExchangeComponent implements OnInit {
  private route = inject(ActivatedRoute);
  private http = inject(HttpClient);
  private router = inject(Router);

  ngOnInit(): void {
    const token = this.route.snapshot.queryParamMap.get('token');
    if (!token) {
      void this.router.navigateByUrl('/login');
      return;
    }
    this.http.post<{ accessToken: string }>(`${environment.apiUrl}/api/public/auth/magic-link/exchange`, { token })
      .subscribe({
        next: res => {
          localStorage.setItem('portal:token', res.accessToken);
          void this.router.navigateByUrl('/');
        },
        error: () => void this.router.navigateByUrl('/login')
      });
  }
}
