import { Routes } from '@angular/router';

export const routes: Routes = [
  { path: ':slug/verify', loadComponent: () => import('./verify.component').then(m => m.VerifyComponent) },
  { path: ':slug/b/:token', loadComponent: () => import('./manage.component').then(m => m.ManageComponent) },
  { path: ':slug', loadComponent: () => import('./booking.component').then(m => m.BookingComponent) },
  { path: '', loadComponent: () => import('./missing-tenant.component').then(m => m.MissingTenantComponent) },
  { path: '**', redirectTo: '' }
];
