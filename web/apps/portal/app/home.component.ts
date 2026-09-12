import { Component, OnInit, inject, signal } from '@angular/core';
import { HttpClient, HttpHeaders } from '@angular/common/http';
import { ActivatedRoute, Router, RouterLink } from '@angular/router';
import { FormsModule } from '@angular/forms';
import { environment } from '../environments/environment';

interface PortalHorse {
  id: string;
  name: string;
}

interface PortalBooking {
  id: string;
  treatmentName: string;
  treatmentSlug?: string;
  horseName: string;
  horseId: string;
  status: string;
  startsAt: string;
}

interface HorseSummary {
  id: string;
  performedAt: string;
  treatmentTypeName?: string;
  plan?: string;
}

@Component({
  selector: 'app-home',
  standalone: true,
  imports: [FormsModule, RouterLink],
  template: `
    <div class="page">
      <h1>Mina sidor</h1>
      @if (!token) {
        <p><a [routerLink]="['/', slug, 'login']">Logga in med e-postlänk</a></p>
      } @else {
        <div class="card">
          <h2>Kontakt</h2>
          <input class="input" [(ngModel)]="name" placeholder="Namn" />
          <input class="input" [(ngModel)]="phone" placeholder="Telefon" />
          <label><input type="checkbox" [(ngModel)]="marketing" /> Jag vill få utskick</label>
          <button class="btn-primary" type="button" (click)="save()">Spara</button>
        </div>
        <div class="card">
          <h2>Hästar</h2>
          @for (h of horses(); track h.id) {
            <p>
              {{ h.name }}
              <button type="button" class="btn-secondary" (click)="loadSummary(h.id)">Visa sammanfattning</button>
            </p>
            @if (summaries()[h.id]; as items) {
              @if (items.length === 0) {
                <p class="muted">Ingen behandlingssammanfattning att visa.</p>
              } @else {
                @for (s of items; track s.id) {
                  <p>{{ s.treatmentTypeName }} · {{ s.performedAt }}<br />{{ s.plan }}</p>
                }
              }
            }
          }
        </div>
        <div class="card">
          <h2>Kommande och tidigare bokningar</h2>
          @for (b of bookings(); track b.id) {
            <p>{{ b.treatmentName }} · {{ b.horseName }} · {{ b.status }} · {{ b.startsAt }}</p>
            <a [href]="bookAgainUrl(b)">Boka igen</a>
          }
        </div>
      }
    </div>
  `
})
export class HomeComponent implements OnInit {
  private http = inject(HttpClient);
  private router = inject(Router);
  private route = inject(ActivatedRoute);
  slug = this.route.snapshot.paramMap.get('slug') ?? '';
  token = localStorage.getItem('portal:token');
  name = '';
  phone = '';
  marketing = false;
  horses = signal<PortalHorse[]>([]);
  bookings = signal<PortalBooking[]>([]);
  summaries = signal<Record<string, HorseSummary[]>>({});
  widgetUrl = `${environment.apiUrl}/widget/${this.slug}`;

  ngOnInit(): void {
    if (!this.token) {
      void this.router.navigateByUrl(`/${this.slug}/login`);
      return;
    }
    const headers = this.headers();
    this.http.get<{ name: string; phone?: string; marketingConsent: boolean }>(`${environment.apiUrl}/api/app/portal/me`, { headers })
      .subscribe(me => {
        this.name = me.name;
        this.phone = me.phone ?? '';
        this.marketing = me.marketingConsent;
      });
    this.http.get<PortalHorse[]>(`${environment.apiUrl}/api/app/portal/horses`, { headers })
      .subscribe(h => this.horses.set(h));
    this.http.get<PortalBooking[]>(`${environment.apiUrl}/api/app/portal/bookings`, { headers })
      .subscribe(b => this.bookings.set(b));
  }

  bookAgainUrl(b: PortalBooking): string {
    const slug = b.treatmentSlug ? `?treatment=${encodeURIComponent(b.treatmentSlug)}` : '';
    return `${this.widgetUrl}${slug}`;
  }

  loadSummary(horseId: string): void {
    this.http.get<HorseSummary[]>(`${environment.apiUrl}/api/app/portal/horses/${horseId}/summary`, { headers: this.headers() })
      .subscribe(items => this.summaries.update(cur => ({ ...cur, [horseId]: items })));
  }

  save(): void {
    if (!this.token) return;
    this.http.put(`${environment.apiUrl}/api/app/portal/me`, {
      name: this.name,
      phone: this.phone,
      marketingConsent: this.marketing
    }, { headers: this.headers() }).subscribe();
  }

  private headers(): HttpHeaders {
    return new HttpHeaders({ Authorization: `Bearer ${this.token}` });
  }
}
