# M1 — Records & Journals

**Goal:** the practitioner can register clients and horses, configure treatment types with journal templates, and write, sign and amend legally compliant journals. This milestone goes into **real production use** before M3 starts.
**Estimated effort:** 3 weeks
**Prerequisites:** M0 complete. Read `00-CONTEXT.md`, especially the regulatory section.

> This is the milestone with legal weight. Get the journal model right before optimising anything.

---

## Scope

### In
Owners, horses, treatment types, journal templates, journal entries with signing and amendments, attachments, PDF export, full-text search.

### Out
Bookings, availability, calendar, the widget, notifications. A journal in M1 is created manually, not from a booking.

---

## Tasks

### 1. Owner entity
Fields: id, name, email (`citext`), phone, address (street, postcode, city), optional coordinates, notes, marketing consent (bool + timestamp), `created_at`, `updated_at`, `deleted_at`.

Endpoints: create, update, get, list with search (name/email/phone, trigram-indexed), soft delete, restore.

### 2. Horse entity
Fields: id, owner_id, name, species (default `Häst` — the regulation requires *djurslag* explicitly, so store it), breed, sex (`Sto`/`Valack`/`Hingst`/`Okänt`), birth year **or** age group (one required), identity (UELN / chip / passport number), colour and markings, stable location (address + optional coordinates, defaults to owner address), background/free text, status (`Aktiv`/`Arkiverad`/`Avliden`), soft delete.

Endpoints: CRUD, list by owner, search across horse name + owner name.

**Rule:** a horse cannot be hard-deleted if it has journal entries. Archive instead.

### 3. Treatment types
Fields per `00-CONTEXT.md` and the plan:
name, short description, public description, duration minutes, buffer before/after minutes, colour, price excl. VAT, VAT rate, bookable online (bool), requires approval (bool), min notice hours, max advance days, allowed location types, follow-up interval days (nullable), active (bool), journal template.

Endpoints: CRUD (deactivate, never delete), list active, list all.

**Critical rule:** treatment types are referenced by historic journals. Deactivate, never delete. When duration or price changes, existing records keep their captured values — so `Booking` and `JournalEntry` denormalise `treatment_name`, `duration_minutes` and `price` at creation time.

### 4. Journal templates
Stored as JSONB on the treatment type. Definition format:

```json
{
  "version": 1,
  "sections": [
    { "key": "palpation", "label": "Palpationsfynd", "type": "textarea" },
    { "key": "rorelse", "label": "Rörelsebedömning", "type": "select",
      "options": ["Ua", "Lindrig avvikelse", "Tydlig avvikelse"] },
    { "key": "vas", "label": "Smärtskattning (0–10)", "type": "number", "min": 0, "max": 10 },
    { "key": "kroppskarta", "label": "Markerade områden", "type": "bodymap" }
  ]
}
```

Supported field types: `text`, `textarea`, `number`, `select`, `multiselect`, `checkbox`, `date`, `bodymap`.

Validate the template definition on save. Store the template `version` on each journal entry so an old entry still renders correctly after the template changes.

### 5. Journal entry — the core

**Base fields are real typed columns, not JSONB.** These are legally required and must always exist and be searchable:

| Column | Notes |
|---|---|
| `horse_id`, `owner_snapshot` | Owner name/address/phone captured at time of writing (JSONB snapshot — the regulation wants the details as they were) |
| `species`, `sex`, `age_or_age_group`, `animal_identity` | Snapshotted from the horse |
| `performed_at` | Date **and** time, `timestamptz` |
| `anamnes` | text, required |
| `status_klinisk` | text, required |
| `atgarder` | text — examinations/treatments performed **and the reason for them**, required |
| `diagnos` | text |
| `differentialdiagnoser` | text |
| `prognos_och_plan` | text — includes home-care advice (*hemgångsråd*) |
| `treatment_type_id` + denormalised name | |
| `template_data` | JSONB — the extra template fields |
| `template_version` | int |
| `status` | `Draft` \| `Signed` |
| `signed_at`, `signed_by`, `content_hash` | |
| `created_at`, `created_by` | |

**No `deleted_at`. No update after signing. Ever.**

Behaviour:
- Drafts are freely editable and autosaved.
- `POST /api/app/journals/{id}/sign` validates all required fields, computes a SHA-256 hash of the canonical content, sets `status = Signed`, and writes to `audit_log`.
- Any attempt to update a signed entry returns `409 Conflict`.
- Reads of journal entries are audit-logged too, not just writes.

### 6. Journal amendments
`journal_amendments`: id, journal_entry_id, text, reason, `created_at`, `created_by`.

`POST /api/app/journals/{id}/amendments` is the **only** way to change a signed journal. Amendments are immutable and render below the original entry in chronological order, each showing its date and signer. This satisfies the "corrections dated and signed" requirement.

### 7. Attachments
- Upload to S3-compatible object storage (MinIO locally). Store key, original filename, content type, size, SHA-256 checksum, `uploaded_at`, `uploaded_by`.
- Scoped to a journal entry. Attachments to a signed entry are permitted (lab results arrive late) but are themselves immutable and audit-logged.
- Serve via short-lived signed URLs, never public buckets.
- Accept: images (jpg/png/heic), PDF, video (mp4, size-capped). Validate content type by magic bytes, not extension.

### 8. Search and retrieval
- `tsvector` generated column over `anamnes`, `status_klinisk`, `atgarder`, `diagnos`, `prognos_och_plan` using a **Swedish** text search configuration. GIN index.
- Index `(horse_id, performed_at DESC)` — this is the "find consultations for a specific animal at a given time" requirement.
- Endpoints: journals by horse (paged, newest first), journals by date range, full-text search across journals.

### 9. PDF export
- Single journal entry → PDF, including amendments, attachment list and signature block.
- Full journal history for one horse → PDF.
- Use QuestPDF. Swedish labels, practitioner details and signature block on every page.
- This exists in M1 deliberately: you must be able to hand journals to an inspector or a new system from day one.

### 10. Admin UI (Angular)

**Screens:**
1. **Klienter** — list, search, create/edit, archive. Client detail shows their horses.
2. **Hästar** — list, search, create/edit, archive. Horse detail shows profile + journal history newest first.
3. **Journal editor** — the most important screen. Base fields + dynamically rendered template fields + body map + attachment upload. Autosave draft every few seconds and on blur. Explicit **Signera** button with a confirmation dialog stating that the entry becomes locked.
4. **Signed journal view** — read-only, with an **Lägg till tillägg** (amendment) action.
5. **Behandlingstyper** — CRUD plus a visual journal template builder (add/reorder/remove fields, choose type, set options).
6. **Osignerade journaler** — list of drafts, sorted oldest first.

**Body map component:** clickable horse silhouette (SVG, left and right side). Clicking places a numbered marker; each marker gets a label and optional note. Stored as `{ markers: [{ x, y, side, label, note }] }` in `template_data`. Build this properly — it is the feature that saves the most typing in a stable aisle.

**Mobile first.** This will be used one-handed, on a phone, in a cold barn. Large tap targets, minimal typing, no hover-dependent interactions.

### 11. Offline-tolerant drafts
Journal drafts persist to IndexedDB and sync when connectivity returns. Show a clear sync indicator. Stable wifi is not a thing.

---

## Acceptance criteria

- [ ] An owner and horse can be created, and a journal written against that horse
- [ ] A journal cannot be signed while any legally required field is empty
- [ ] A signed journal cannot be edited or deleted via any endpoint — verified by an integration test that attempts it
- [ ] An amendment can be added to a signed journal and displays with its date and author
- [ ] Journal entries for a given horse in a given date range are retrievable in one query
- [ ] Swedish full-text search returns a journal by a word in its `anamnes`
- [ ] An attachment uploads, is retrievable via a signed URL, and the URL expires
- [ ] A journal exports as a PDF containing all base fields, template fields, amendments and a signature block
- [ ] Changing a treatment type's journal template does not alter how an existing signed journal renders
- [ ] The journal editor is fully usable on a 390px-wide viewport
- [ ] A draft written offline survives a page reload and syncs when back online
- [ ] `audit_log` contains entries for journal create, sign, amend and read

## Definition of done

The practitioner writes real journals in this system for at least one working week before M2 begins. Any friction found in that week is fixed before moving on.
