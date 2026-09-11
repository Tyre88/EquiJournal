# Journal field mapping — SJVFS 2023:19

Show this table to an inspector. Have a colleague in the field review it before go-live.

Required content (00-CONTEXT / SJVFS 2023:19) mapped to HästJournal.

| Required | System field | Where |
|---|---|---|
| Owner name | `Owner.Name` at write time; frozen as `JournalEntry.OwnerSnapshot` JSON `Name` | Journal + owner |
| Owner address | `Owner.AddressStreet`, `AddressPostcode`, `AddressCity` → `OwnerSnapshot` | Journal snapshot |
| Owner phone | `Owner.Phone` → `OwnerSnapshot` | Journal snapshot |
| Species (*djurslag*) | `JournalEntry.Species` (from `Horse.Species`) | Journal |
| Sex | `JournalEntry.Sex` (from `Horse.Sex`: Sto / Valack / Hingst / Okant) | Journal |
| Age or age group | `JournalEntry.AgeOrAgeGroup` (`Horse.BirthYear` or `Horse.AgeGroup`) | Journal |
| Animal identity | `JournalEntry.AnimalIdentity` (`Horse.Identity`) | Journal |
| Date and time of consultation | `JournalEntry.PerformedAt` (`timestamptz`, displayed Europe/Stockholm) | Journal |
| Anamnesis | `JournalEntry.Anamnes` | Journal (required to sign in normal practice) |
| Clinical status | `JournalEntry.StatusKlinisk` | Journal (required to sign) |
| Examinations / treatments and why | `JournalEntry.Atgarder` plus optional `TemplateDataJson` | Journal (required to sign) |
| Diagnosis | `JournalEntry.Diagnos` | Journal (optional if not established) |
| Differential diagnoses | `JournalEntry.Differentialdiagnoser` | Journal |
| Prognosis and plan incl. home care | `JournalEntry.PrognosOchPlan` | Journal |
| Who wrote / signed | `SignedBy`, `SignedAt`, `CreatedBy` | Journal |
| Corrections dated and signed | `JournalAmendment.Text`, `Reason`, `CreatedBy`, `CreatedAt` | Amendments (append-only) |
| Underlying documents | `Attachment` + object storage | Attachments |
| Find animal at a given time | Index `(HorseId, PerformedAt)`; search + schema | API / admin |
| Language | UI and journal fields in Swedish | Admin `sv-SE` |

Imported historic rows use `JournalEntry.Source = Import` and may have empty clinical fields if the source had none — we do not invent content. Mark those for the inspector as transcribed history.

**Field review:** name, date, and role of the reviewer — _pending_.
