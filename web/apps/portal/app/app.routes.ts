import { Routes } from '@angular/router';
import { LoginComponent } from './login.component';
import { ExchangeComponent } from './exchange.component';
import { HomeComponent } from './home.component';

export const routes: Routes = [
  { path: '', component: HomeComponent },
  { path: 'in', component: ExchangeComponent },
  { path: 'login', component: LoginComponent },
  { path: '**', redirectTo: '' }
];
