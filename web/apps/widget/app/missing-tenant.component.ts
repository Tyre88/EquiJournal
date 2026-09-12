import { Component } from '@angular/core';

@Component({
  selector: 'app-missing-tenant',
  standalone: true,
  template: `
    <div class="wrap">
      <h1>Bokningen hittades inte</h1>
      <p>Länken saknar verksamhet. Kontrollera adressen hos din behandlare.</p>
    </div>
  `
})
export class MissingTenantComponent {}
