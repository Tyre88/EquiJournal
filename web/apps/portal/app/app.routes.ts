import { Routes } from '@angular/router';
import { LoginComponent } from './login.component';
import { ExchangeComponent } from './exchange.component';
import { HomeComponent } from './home.component';
import { MissingTenantComponent } from './missing-tenant.component';

export const routes: Routes = [
  { path: ':slug/in', component: ExchangeComponent },
  { path: ':slug/login', component: LoginComponent },
  { path: ':slug', component: HomeComponent },
  { path: '', component: MissingTenantComponent },
  { path: '**', redirectTo: '' }
];
