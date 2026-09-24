import { ApplicationConfig, LOCALE_ID } from '@angular/core';
import { provideRouter, TitleStrategy } from '@angular/router';
import { provideHttpClient, withInterceptors } from '@angular/common/http';
import { registerLocaleData } from '@angular/common';
import localeSv from '@angular/common/locales/sv';

import { routes } from './app.routes';
import { authInterceptor } from './auth.interceptor';
import { HastJournalTitleStrategy } from './title.strategy';

registerLocaleData(localeSv);

export const appConfig: ApplicationConfig = {
  providers: [
    provideRouter(routes),
    provideHttpClient(withInterceptors([authInterceptor])),
    { provide: LOCALE_ID, useValue: 'sv-SE' },
    { provide: TitleStrategy, useClass: HastJournalTitleStrategy }
  ]
};
