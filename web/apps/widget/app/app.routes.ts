import { Routes } from '@angular/router';

export const routes: Routes = [
  { path: '', loadComponent: () => import('./booking.component').then(m => m.BookingComponent) },
  { path: 'verify', loadComponent: () => import('./verify.component').then(m => m.VerifyComponent) },
  { path: 'b/:token', loadComponent: () => import('./manage.component').then(m => m.ManageComponent) },
  { path: '**', redirectTo: '' }
];
