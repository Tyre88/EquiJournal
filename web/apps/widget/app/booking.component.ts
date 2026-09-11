import { DatePipe } from '@angular/common';
import { Component, computed, inject, OnInit, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { ActivatedRoute } from '@angular/router';
import { publicApi, PublicApiError, PublicTreatment, TreatmentsResponse } from './public-api';

@Component({
  selector: 'app-booking',
  standalone: true,
  imports: [FormsModule, DatePipe],
  template: `
    <div class="wrap">
      <h1>Boka behandling</h1>
      @if (offline()) {
        <div class="alert" role="alert">
          Bokningen går inte att nå just nu. Ring {{ config()?.contactPhone || 'oss' }}
          @if (config()?.contactEmail) { eller mejla <a [href]="'mailto:' + config()!.contactEmail">{{ config()!.contactEmail }}</a> }.
        </div>
      } @else {
        <p class="step-label">Steg {{ step() }} av {{ maxStep() }}</p>

        @if (step() === 1) {
          <fieldset>
            <legend>Välj behandling</legend>
            @for (t of treatments(); track t.id) {
              <label class="choice">
                <input type="radio" name="treatment" [value]="t.id" [checked]="treatmentId() === t.id" (change)="treatmentId.set(t.id)" />
                <span>
                  <strong>{{ t.name }}</strong>
                  <small>{{ t.durationMinutes }} min
                    @if (t.price != null) { · {{ t.price }} kr }</small>
                  @if (t.publicDescription) { <span class="desc">{{ t.publicDescription }}</span> }
                </span>
              </label>
            }
          </fieldset>
          <button type="button" class="btn" [disabled]="!treatmentId()" (click)="go(2)">Nästa</button>
        }

        @if (step() === 2) {
          <label for="postcode">Var finns hästen? Postnummer</label>
          <input id="postcode" name="postcode" inputmode="numeric" autocomplete="postal-code" [(ngModel)]="postcode" />
          <div class="row">
            <button type="button" class="btn ghost" (click)="go(1)">Tillbaka</button>
            <button type="button" class="btn" [disabled]="!validPostcode()" (click)="loadSlots()">Visa tider</button>
          </div>
        }

        @if (step() === 3) {
          <fieldset>
            <legend>Välj tid</legend>
            @if (days().length === 0) {
              <p>Inga lediga tider de närmaste två veckorna.</p>
            }
            <div class="days" role="list">
              @for (day of days(); track day) {
                <button type="button" class="chip" [class.active]="selectedDay() === day" (click)="selectedDay.set(day)">
                  {{ day | date:'EEE d MMM':'Europe/Stockholm':'sv-SE' }}
                </button>
              }
            </div>
            <div class="slots">
              @for (slot of slotsForDay(); track slot) {
                <button type="button" class="chip" [class.active]="startsAt() === slot" (click)="startsAt.set(slot)">
                  {{ slot | date:'HH:mm':'Europe/Stockholm':'sv-SE' }}
                </button>
              }
            </div>
          </fieldset>
          <div class="row">
            <button type="button" class="btn ghost" (click)="go(2)">Tillbaka</button>
            <button type="button" class="btn" [disabled]="!startsAt()" (click)="go(4)">Nästa</button>
          </div>
        }

        @if (step() === 4) {
          <fieldset>
            <legend>Hästens uppgifter</legend>
            <label for="horseName">Namn</label>
            <input id="horseName" name="horseName" required [(ngModel)]="horseName" />
            <label for="ageGroup">Ålder eller åldersgrupp</label>
            <input id="ageGroup" name="ageGroup" [(ngModel)]="ageGroup" placeholder="t.ex. 8 år eller unghäst" />
            <label for="breed">Ras</label>
            <input id="breed" name="breed" [(ngModel)]="breed" />
            <label for="issues">Kända besvär</label>
            <textarea id="issues" name="issues" rows="3" [(ngModel)]="issues"></textarea>
          </fieldset>
          <div class="row">
            <button type="button" class="btn ghost" (click)="go(3)">Tillbaka</button>
            <button type="button" class="btn" [disabled]="!horseName.trim() || !ageGroup.trim()" (click)="go(5)">Nästa</button>
          </div>
        }

        @if (step() === 5) {
          <fieldset>
            <legend>Dina uppgifter</legend>
            <label for="ownerName">Namn</label>
            <input id="ownerName" name="name" autocomplete="name" required [(ngModel)]="ownerName" />
            <label for="email">E-post</label>
            <input id="email" name="email" type="email" autocomplete="email" required [(ngModel)]="email" />
            <label for="phone">Telefon</label>
            <input id="phone" name="phone" type="tel" autocomplete="tel" required [(ngModel)]="phone" />
            <label for="street">Stalladress</label>
            <input id="street" name="street" autocomplete="street-address" required [(ngModel)]="street" />
            <label class="hp" for="website">Webbplats</label>
            <input id="website" name="website" tabindex="-1" autocomplete="off" [(ngModel)]="website" />
          </fieldset>
          <div class="row">
            <button type="button" class="btn ghost" (click)="go(4)">Tillbaka</button>
            <button type="button" class="btn" [disabled]="!ownerReady()" (click)="go(6)">Nästa</button>
          </div>
        }

        @if (step() === 6) {
          <section>
            <h2>Bekräfta</h2>
            <dl>
              <dt>Behandling</dt><dd>{{ selectedTreatment()?.name }}</dd>
              <dt>Tid</dt><dd>{{ startsAt() | date:'yyyy-MM-dd HH:mm':'Europe/Stockholm':'sv-SE' }}</dd>
              <dt>Häst</dt><dd>{{ horseName }}</dd>
              <dt>Du</dt><dd>{{ ownerName }}, {{ email }}</dd>
              <dt>Adress</dt><dd>{{ street }}, {{ postcode }}</dd>
            </dl>
            <label class="choice">
              <input type="checkbox" [(ngModel)]="consent" />
              <span>Jag godkänner
                <a [href]="config()?.privacyPolicyUrl || '#'" target="_blank" rel="noopener">integritetspolicyn</a>
                och bokningsvillkoren.
              </span>
            </label>
            @if (config()?.bookingTerms) {
              <details><summary>Bokningsvillkor</summary><p>{{ config()!.bookingTerms }}</p></details>
            }
            @if (error()) { <div class="alert" role="alert">{{ error() }}</div> }
          </section>
          <div class="row">
            <button type="button" class="btn ghost" (click)="go(5)">Tillbaka</button>
            <button type="button" class="btn" [disabled]="!consent || submitting()" (click)="submit()">Skicka bokning</button>
          </div>
        }

        @if (step() === 7) {
          <section>
            <h2>Klart</h2>
            <p>Kontrollera din e-post för att bekräfta bokningen.</p>
            @if (reference()) { <p>Referens: <strong>{{ reference() }}</strong></p> }
          </section>
        }
      }
    </div>
  `
})
export class BookingComponent implements OnInit {
  private route = inject(ActivatedRoute);
  private openedAt = new Date(Date.now() - 4000).toISOString();

  treatments = signal<PublicTreatment[]>([]);
  config = signal<TreatmentsResponse | null>(null);
  step = signal(1);
  maxStep = computed(() => this.skipTreatment() ? 6 : 7);
  skipTreatment = signal(false);
  treatmentId = signal('');
  postcode = '';
  slots = signal<string[]>([]);
  selectedDay = signal('');
  startsAt = signal('');
  horseName = '';
  ageGroup = '';
  breed = '';
  issues = '';
  ownerName = '';
  email = '';
  phone = '';
  street = '';
  website = '';
  consent = false;
  submitting = signal(false);
  error = signal('');
  offline = signal(false);
  reference = signal('');

  days = computed(() => {
    const set = new Set(this.slots().map(s => s.slice(0, 10)));
    return [...set];
  });

  slotsForDay = computed(() => this.slots().filter(s => s.startsWith(this.selectedDay())));
  selectedTreatment = computed(() => this.treatments().find(t => t.id === this.treatmentId()) ?? null);

  async ngOnInit(): Promise<void> {
    try {
      const data = await publicApi.treatments();
      this.config.set(data);
      this.treatments.set(data.treatments);
      const preset = this.route.snapshot.queryParamMap.get('treatment');
      if (preset) {
        const match = data.treatments.find(t => t.slug === preset || t.id === preset);
        if (match) {
          this.treatmentId.set(match.id);
          this.skipTreatment.set(true);
          this.step.set(2);
        }
      }
    } catch {
      this.offline.set(true);
    }
  }

  validPostcode(): boolean {
    return /^\d{3}\s?\d{2}$/.test(this.postcode.trim());
  }

  ownerReady(): boolean {
    return !!(this.ownerName.trim() && this.email.includes('@') && this.phone.trim() && this.street.trim());
  }

  go(step: number): void {
    this.error.set('');
    this.step.set(step);
  }

  async loadSlots(): Promise<void> {
    if (!this.validPostcode()) return;
    const from = new Date();
    const to = new Date(from.getTime() + 13 * 86400000);
    const iso = (d: Date) => d.toISOString().slice(0, 10);
    try {
      const result = await publicApi.slots(this.treatmentId(), iso(from), iso(to), this.postcode.trim());
      this.slots.set(result.starts);
      this.selectedDay.set(result.starts[0]?.slice(0, 10) ?? '');
      this.startsAt.set('');
      this.go(3);
    } catch (err) {
      this.handle(err);
    }
  }

  async submit(): Promise<void> {
    this.submitting.set(true);
    this.error.set('');
    try {
      const result = await publicApi.create({
        treatmentId: this.treatmentId(),
        startsAt: this.startsAt(),
        horse: { name: this.horseName.trim(), ageGroup: this.ageGroup.trim(), breed: this.breed.trim() || null, knownIssues: this.issues.trim() || null },
        owner: {
          name: this.ownerName.trim(),
          email: this.email.trim(),
          phone: this.phone.trim(),
          addressStreet: this.street.trim(),
          addressPostcode: this.postcode.trim()
        },
        note: this.issues.trim() || null,
        consent: this.consent,
        website: this.website,
        formOpenedAt: this.openedAt
      });
      this.reference.set(result.reference);
      this.go(7);
    } catch (err) {
      this.handle(err);
    } finally {
      this.submitting.set(false);
    }
  }

  private handle(err: unknown): void {
    if (err instanceof PublicApiError) {
      if (err.status === 0) { this.offline.set(true); return; }
      if (err.status === 429) { this.error.set('För många försök. Vänta en stund och prova igen.'); return; }
      if (err.status === 409) {
        this.slots.set(err.slots ?? []);
        this.startsAt.set('');
        this.go(3);
        this.error.set('Tiden är inte längre ledig. Välj en ny tid.');
        return;
      }
      this.error.set(err.message === 'error' ? 'Kunde inte slutföra bokningen.' : err.message);
      return;
    }
    this.error.set('Kunde inte slutföra bokningen.');
  }
}
