import { Routes } from '@angular/router';
import { authGuard } from './auth.guard';

export const routes: Routes = [
  { path: '', redirectTo: 'login', pathMatch: 'full' },
  { path: 'login', title: 'Logga in', loadComponent: () => import('./login/login.component').then(m => m.LoginComponent) },
  { path: 'register', title: 'Skapa verksamhet', loadComponent: () => import('./register/register.component').then(m => m.RegisterComponent) },
  { path: '2fa', title: 'Tvåfaktorsverifiering', loadComponent: () => import('./two-factor/two-factor.component').then(m => m.TwoFactorComponent) },
  {
    path: '',
    canActivate: [authGuard],
    loadComponent: () => import('./layout/app-shell.component').then(m => m.AppShellComponent),
    children: [
      { path: 'schema', title: 'Dagens schema', loadComponent: () => import('./schema/schema.component').then(m => m.SchemaComponent) },
      { path: 'dashboard', redirectTo: 'schema', pathMatch: 'full' },
      { path: 'calendar', title: 'Kalender', loadComponent: () => import('./calendar/calendar.component').then(m => m.CalendarComponent) },
      { path: 'bookings/new', title: 'Ny bokning', loadComponent: () => import('./bookings/booking-form.component').then(m => m.BookingFormComponent) },
      { path: 'bookings/:id', title: 'Bokning', loadComponent: () => import('./bookings/booking-detail.component').then(m => m.BookingDetailComponent) },
      { path: 'availability', title: 'Tillgänglighet', loadComponent: () => import('./availability/availability.component').then(m => m.AvailabilityComponent) },
      { path: 'widget-settings', redirectTo: 'settings/widget', pathMatch: 'full' },
      {
        path: 'settings',
        loadComponent: () => import('./settings/settings-shell.component').then(m => m.SettingsShellComponent),
        children: [
          { path: '', redirectTo: 'account', pathMatch: 'full' },
          { path: 'account', title: 'Konto', loadComponent: () => import('./settings/account.component').then(m => m.AccountSettingsComponent) },
          { path: 'practice', title: 'Verksamhet', loadComponent: () => import('./settings/practice.component').then(m => m.PracticeSettingsComponent) },
          { path: 'notifications', title: 'Aviseringar', loadComponent: () => import('./settings/notifications.component').then(m => m.NotificationSettingsComponent) },
          { path: 'widget', title: 'Widget', loadComponent: () => import('./widget/widget-settings.component').then(m => m.WidgetSettingsComponent) },
          { path: 'export', title: 'Exportera arkiv', loadComponent: () => import('./settings/export.component').then(m => m.ExportSettingsComponent) }
        ]
      },
      { path: 'follow-ups', redirectTo: 'uppfoljningar', pathMatch: 'full' },
      { path: 'uppfoljningar', title: 'Uppföljningar', loadComponent: () => import('./follow-ups/follow-ups.component').then(m => m.FollowUpsComponent) },
      { path: 'marketing', redirectTo: 'utskick', pathMatch: 'full' },
      { path: 'utskick', title: 'Utskick', loadComponent: () => import('./marketing/marketing.component').then(m => m.MarketingComponent) },
      { path: 'booking-requests', title: 'Inkomna förfrågningar', loadComponent: () => import('./widget/booking-requests.component').then(m => m.BookingRequestsComponent) },
      { path: 'owners', title: 'Kunder', loadComponent: () => import('./owners/owners.component').then(m => m.OwnersComponent) },
      { path: 'owners/create', title: 'Ny kund', loadComponent: () => import('./owners/owner-form/owner-form.component').then(m => m.OwnerFormComponent) },
      { path: 'owners/:id/edit', title: 'Redigera kund', loadComponent: () => import('./owners/owner-form/owner-form.component').then(m => m.OwnerFormComponent) },
      { path: 'owners/:id', title: 'Kund', loadComponent: () => import('./owners/owner-detail/owner-detail.component').then(m => m.OwnerDetailComponent) },
      { path: 'horses', title: 'Hästar', loadComponent: () => import('./horses/horses.component').then(m => m.HorsesComponent) },
      { path: 'horses/create', title: 'Ny häst', loadComponent: () => import('./horses/horse-form/horse-form.component').then(m => m.HorseFormComponent) },
      { path: 'horses/:id/edit', title: 'Redigera häst', loadComponent: () => import('./horses/horse-form/horse-form.component').then(m => m.HorseFormComponent) },
      { path: 'horses/:id', title: 'Häst', loadComponent: () => import('./horses/horse-detail/horse-detail.component').then(m => m.HorseDetailComponent) },
      { path: 'treatment-types', title: 'Behandlingstyper', loadComponent: () => import('./treatment-types/treatment-types.component').then(m => m.TreatmentTypesComponent) },
      { path: 'treatment-types/create', title: 'Ny behandlingstyp', loadComponent: () => import('./treatment-types/treatment-type-form/treatment-type-form.component').then(m => m.TreatmentTypeFormComponent) },
      { path: 'treatment-types/:id/edit', title: 'Redigera behandlingstyp', loadComponent: () => import('./treatment-types/treatment-type-form/treatment-type-form.component').then(m => m.TreatmentTypeFormComponent) },
      { path: 'journals', title: 'Journaler', loadComponent: () => import('./journals/journals.component').then(m => m.JournalsComponent) },
      { path: 'journals/new', title: 'Ny journal', loadComponent: () => import('./journals/journal-editor/journal-editor.component').then(m => m.JournalEditorComponent) },
      { path: 'journals/drafts', title: 'Utkast', loadComponent: () => import('./journals/drafts/drafts.component').then(m => m.DraftsComponent) },
      { path: 'journals/:id/edit', title: 'Redigera journal', loadComponent: () => import('./journals/journal-editor/journal-editor.component').then(m => m.JournalEditorComponent) },
      { path: 'journals/:id', title: 'Journal', loadComponent: () => import('./journals/journal-view/journal-view.component').then(m => m.JournalViewComponent) },
      { path: 'reports', title: 'Rapporter', loadComponent: () => import('./reports/reports.component').then(m => m.ReportsComponent) },
      { path: 'audit', title: 'Granskningslogg', loadComponent: () => import('./audit/audit.component').then(m => m.AuditComponent) }
    ]
  },
  { path: '**', redirectTo: 'login' }
];
