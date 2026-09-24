import { Component, OnDestroy, OnInit, inject, signal, viewChild } from '@angular/core';
import { Router, RouterLink, RouterLinkActive, RouterOutlet } from '@angular/router';
import { EjConfirmDialogComponent, EjIconComponent, EjToastHostComponent } from '@equijournal/ui';
import { Api } from '../api';
import { AuthService } from '../auth.service';
import { GlobalSearchComponent } from './global-search.component';
import { SyncStatusService } from '../sync-status.service';

interface InboxItem {
  id: string;
  title: string;
  body: string;
  link?: string | null;
  readAt?: string | null;
}

@Component({
  selector: 'app-shell',
  standalone: true,
  imports: [RouterOutlet, RouterLink, RouterLinkActive, EjIconComponent, EjToastHostComponent, EjConfirmDialogComponent, GlobalSearchComponent],
  template: `
    <a class="skip-link" href="#main">Hoppa till innehåll</a>
    <div class="app-shell">
      <aside class="app-sidebar">
        <a routerLink="/schema" class="app-brand">HästJournal</a>
        <div class="nav-cta">
          <a routerLink="/bookings/new"><ej-icon name="plus" [size]="18" /> Ny bokning</a>
        </div>
        @for (group of groups; track group.title) {
          <nav class="nav-group" [attr.aria-label]="group.title">
            <h2>{{ group.title }}</h2>
            @for (item of group.items; track item.path) {
              <a [routerLink]="item.path" routerLinkActive="active" [routerLinkActiveOptions]="item.exact ? { exact: true } : { exact: false }" [attr.aria-current]="null">
                <ej-icon [name]="item.icon" [size]="18" />
                {{ item.label }}
              </a>
            }
          </nav>
        }
        <div class="sidebar-user">
          <button type="button" class="nav-link" (click)="toggleInbox()">
            <ej-icon name="bell" [size]="18" /> Aviseringar
            @if (unread() > 0) { <span class="notify-badge">{{ unread() }}</span> }
          </button>
          <a routerLink="/settings/account" class="user-chip">{{ auth.currentUser()?.displayName }}</a>
          <button type="button" class="nav-link" (click)="logout()">
            <ej-icon name="log-out" [size]="18" /> Logga ut
          </button>
        </div>
      </aside>

      <div class="app-content">
        <header class="app-topbar">
          <a routerLink="/schema" class="app-brand">HästJournal</a>
          <div class="app-topbar-actions">
            @if (sync.state() !== 'online') {
              <span class="sync-chip" [class]="'sync-' + sync.state()" role="status">
                {{ syncLabel() }}
              </span>
            }
            <button type="button" class="icon-btn" (click)="openSearch()" aria-label="Sök (Ctrl+K)">
              <ej-icon name="search" />
            </button>
            <a routerLink="/bookings/new" class="icon-btn" aria-label="Ny bokning">
              <ej-icon name="plus" />
            </a>
            <button type="button" class="icon-btn notify-btn" (click)="toggleInbox()" [attr.aria-label]="'Aviseringar' + (unread() ? ' (' + unread() + ')' : '')">
              <ej-icon name="bell" />
              @if (unread() > 0) { <span class="notify-badge">{{ unread() }}</span> }
            </button>
            <button type="button" class="icon-btn" (click)="logout()" aria-label="Logga ut">
              <ej-icon name="log-out" />
            </button>
          </div>
        </header>

        <main id="main" class="app-main">
          <router-outlet />
        </main>

        <nav class="app-tabbar" aria-label="Huvudmeny">
          <a routerLink="/schema" routerLinkActive="active">
            <ej-icon name="clock" [size]="20" /> Schema
          </a>
          <a routerLink="/calendar" routerLinkActive="active">
            <ej-icon name="calendar" [size]="20" /> Kalender
          </a>
          <a routerLink="/journals" routerLinkActive="active" [routerLinkActiveOptions]="{ exact: true }">
            <ej-icon name="file-text" [size]="20" /> Journaler
          </a>
          <a routerLink="/owners" routerLinkActive="active">
            <ej-icon name="users" [size]="20" /> Kunder
          </a>
          <button type="button" (click)="moreOpen.set(true)">
            <ej-icon name="more" [size]="20" /> Mer
          </button>
        </nav>
      </div>
    </div>

    @if (moreOpen()) {
      <div class="more-sheet" (click)="moreOpen.set(false)">
        <div class="more-sheet-panel" (click)="$event.stopPropagation()">
          <h2 class="page-title" style="font-size:1.1rem;margin-bottom:0.75rem">Mer</h2>
          <a routerLink="/horses" routerLinkActive="active" (click)="moreOpen.set(false)"><ej-icon name="horse" /> Hästar</a>
          <a routerLink="/journals/drafts" routerLinkActive="active" (click)="moreOpen.set(false)"><ej-icon name="pen" /> Utkast</a>
          <a routerLink="/booking-requests" routerLinkActive="active" (click)="moreOpen.set(false)"><ej-icon name="file-text" /> Förfrågningar</a>
          <a routerLink="/availability" routerLinkActive="active" (click)="moreOpen.set(false)"><ej-icon name="clock" /> Tillgänglighet</a>
          <a routerLink="/uppfoljningar" routerLinkActive="active" (click)="moreOpen.set(false)"><ej-icon name="clock" /> Uppföljningar</a>
          <a routerLink="/settings/practice" routerLinkActive="active" (click)="moreOpen.set(false)"><ej-icon name="map-pin" /> Verksamhet</a>
          <a routerLink="/settings/widget" routerLinkActive="active" (click)="moreOpen.set(false)"><ej-icon name="settings" /> Widget</a>
          <a routerLink="/settings/account" routerLinkActive="active" (click)="moreOpen.set(false)"><ej-icon name="settings" /> Konto</a>
          <a routerLink="/treatment-types" routerLinkActive="active" (click)="moreOpen.set(false)"><ej-icon name="settings" /> Behandlingstyper</a>
          <a routerLink="/reports" routerLinkActive="active" (click)="moreOpen.set(false)"><ej-icon name="file-text" /> Rapporter</a>
          <a routerLink="/audit" routerLinkActive="active" (click)="moreOpen.set(false)"><ej-icon name="file-text" /> Granskningslogg</a>
          <a routerLink="/bookings/new" (click)="moreOpen.set(false)"><ej-icon name="plus" /> Ny bokning</a>
        </div>
      </div>
    }

    @if (inboxOpen()) {
      <div class="notify-backdrop" (click)="inboxOpen.set(false)">
        <div class="notify-panel" (click)="$event.stopPropagation()">
          <div class="notify-head">
            <strong>Aviseringar</strong>
            <button type="button" class="btn-ghost" (click)="markAll()">Markera lästa</button>
          </div>
          @for (n of inbox(); track n.id) {
            <a [routerLink]="n.link || '/schema'" (click)="markOne(n)" [class.unread]="!n.readAt">
              <strong>{{ n.title }}</strong>
              <span>{{ n.body }}</span>
            </a>
          } @empty {
            <p class="muted">Inga aviseringar.</p>
          }
        </div>
      </div>
    }

    <app-global-search />
    <ej-toast-host />
    <ej-confirm-dialog />
  `
})
export class AppShellComponent implements OnInit, OnDestroy {
  auth = inject(AuthService);
  sync = inject(SyncStatusService);
  private api = inject(Api);
  private router = inject(Router);
  private search = viewChild(GlobalSearchComponent);
  moreOpen = signal(false);
  inboxOpen = signal(false);
  inbox = signal<InboxItem[]>([]);
  unread = signal(0);
  private poll: ReturnType<typeof setInterval> | undefined;

  readonly groups = [
    {
      title: 'Idag',
      items: [
        { path: '/schema', label: 'Schema', icon: 'clock' as const, exact: true },
        { path: '/calendar', label: 'Kalender', icon: 'calendar' as const, exact: false },
        { path: '/booking-requests', label: 'Förfrågningar', icon: 'file-text' as const, exact: false },
        { path: '/uppfoljningar', label: 'Uppföljningar', icon: 'clock' as const, exact: false }
      ]
    },
    {
      title: 'Register',
      items: [
        { path: '/owners', label: 'Kunder', icon: 'users' as const, exact: false },
        { path: '/horses', label: 'Hästar', icon: 'horse' as const, exact: false }
      ]
    },
    {
      title: 'Journaler',
      items: [
        { path: '/journals', label: 'Journaler', icon: 'file-text' as const, exact: true },
        { path: '/journals/drafts', label: 'Utkast', icon: 'pen' as const, exact: true }
      ]
    },
    {
      title: 'Inställningar',
      items: [
        { path: '/settings/practice', label: 'Verksamhet', icon: 'map-pin' as const, exact: false },
        { path: '/availability', label: 'Tillgänglighet', icon: 'clock' as const, exact: false },
        { path: '/treatment-types', label: 'Behandlingstyper', icon: 'settings' as const, exact: false },
        { path: '/settings/widget', label: 'Widget', icon: 'settings' as const, exact: false },
        { path: '/settings/notifications', label: 'Aviseringar', icon: 'settings' as const, exact: false },
        { path: '/utskick', label: 'Utskick', icon: 'file-text' as const, exact: false },
        { path: '/reports', label: 'Rapporter', icon: 'file-text' as const, exact: false },
        { path: '/audit', label: 'Granskningslogg', icon: 'file-text' as const, exact: false }
      ]
    }
  ];

  openSearch(): void {
    this.search()?.show();
  }

  syncLabel(): string {
    switch (this.sync.state()) {
      case 'offline': return 'Offline';
      case 'syncing': return `Synkar (${this.sync.pendingCount()})`;
      case 'error': return this.sync.message() || 'Synkfel';
      default: return '';
    }
  }

  ngOnInit(): void {
    this.refreshInbox();
    this.poll = setInterval(() => this.refreshInbox(), 30000);
  }

  ngOnDestroy(): void {
    if (this.poll) clearInterval(this.poll);
  }

  toggleInbox(): void {
    this.inboxOpen.update(open => !open);
    if (this.inboxOpen()) this.refreshInbox();
  }

  refreshInbox(): void {
    this.api.get<{ unread: number; items: InboxItem[] }>('/api/app/notifications').subscribe({
      next: res => {
        this.unread.set(res.unread ?? 0);
        this.inbox.set(res.items ?? []);
      }
    });
  }

  markOne(item: InboxItem): void {
    this.inboxOpen.set(false);
    if (item.readAt) return;
    this.api.post(`/api/app/notifications/${item.id}/read`, {}).subscribe({
      next: () => this.refreshInbox()
    });
  }

  markAll(): void {
    this.api.post('/api/app/notifications/read-all', {}).subscribe({
      next: () => this.refreshInbox()
    });
  }

  logout(): void {
    const goLogin = () => void this.router.navigateByUrl('/login');
    this.auth.logout().subscribe({
      next: goLogin,
      error: goLogin
    });
  }
}
