# Full data export (exit path)

`POST /api/app/export/archive` (admin) returns a ZIP that a future system can re-import.

## Layout

```
manifest.json
owners.json
horses.json
visits.json
booking_lines.json
journals.json
amendments.json
attachments/index.json
attachments/{attachmentId}/{originalFileName}
```

`manifest.json` has `format: hastjournal-archive`, `version: 1`, `exportedAt`, and counts.

IDs are UUID v7 strings. Timestamps are ISO-8601 UTC (`timestamptz`). Enums are PascalCase strings (`Signed`, `Import`, `Widget`, …).

Soft-deleted owners and horses are included (`deletedAt` set). Journal and amendment rows are never deleted in the source system.

Attachment bytes are stored under `attachments/{id}/`. `attachments/index.json` lists metadata plus `included` / `error` if object storage could not supply a file.

## Human-readable journals

`POST /api/app/export/journals-pdf` returns one PDF per horse (`journals/{name}_{id}.pdf`), same layout as the admin per-horse export.

## CLI

The same services are registered in the API host. For an air-gapped run, call the endpoints against a local API or reuse `IArchiveExportService` from a small console host.
