import { HttpErrorResponse } from '@angular/common/http';
import { Injectable } from '@angular/core';

@Injectable({ providedIn: 'root' })
export class ApiErrorService {
  message(err: unknown): string {
    if (err instanceof HttpErrorResponse) {
      const body = err.error;
      if (body && typeof body === 'object' && 'error' in body && typeof body.error === 'string') {
        return body.error;
      }
      switch (err.status) {
        case 0:
          return 'Ingen nätverksanslutning. Kontrollera uppkopplingen och försök igen.';
        case 401:
          return 'Du är inte inloggad. Logga in och försök igen.';
        case 403:
          return 'Du har inte behörighet för den här åtgärden.';
        case 404:
          return 'Det du söker kunde inte hittas.';
        case 409:
          return 'Det finns en konflikt med en annan version. Ladda om sidan.';
        case 429:
          return 'För många förfrågningar. Vänta en stund och försök igen.';
        default:
          return err.status >= 500
            ? 'Serverfel. Försök igen om en stund.'
            : 'Något gick fel. Försök igen.';
      }
    }
    return 'Något gick fel. Försök igen.';
  }
}
