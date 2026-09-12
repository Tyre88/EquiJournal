import { Component } from '@angular/core';

@Component({
  selector: 'app-missing-tenant',
  standalone: true,
  template: `
    <div class="page">
      <h1>Mina sidor</h1>
      <p>Länken saknar verksamhet. Öppna adressen du fick från din behandlare.</p>
    </div>
  `
})
export class MissingTenantComponent {}
