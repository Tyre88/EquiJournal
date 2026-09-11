# Erasure procedure (radering)

A client may request erasure. Journals that must be kept under SJVFS 2023:19 / lagen (2009:302) are **not** erased while the retention clock runs. Everything else that can be separated **is**.

## Erase (or anonymise)

| Data | How |
|---|---|
| Owner marketing consent | `SetMarketingConsent(false)` |
| Owner account / portal `UserId` | Unlink; delete Identity user if only that owner |
| Owner contact used only for booking/marketing | Soft-delete owner (`DeletedAt`) **if** no retained signed journal points at them via horse |
| Horse without signed journals | Soft-delete / archive |
| Bookings not tied to a retained journal | Cancelled/completed rows may be deleted or anonymised after the booking is no longer needed |
| Notification log rows for that email | Already purged at 12 months; can delete earlier if no legal hold |
| Web-push endpoints | Delete |

## Retain (legal obligation)

| Data | Reason |
|---|---|
| `journal_entries` including `OwnerSnapshot` | Journal must identify the animal **and** the keeper at the time |
| `journal_amendments` | Corrections must remain dated and signed |
| `attachments` | Underlying documents for the journal |
| `audit_log` rows for journal sign/read/amend | Accountability for the same period |
| Booking line / visit if it is the only proof of *when* the consultation occurred and is referenced by `JournalEntry.BookingLineId` | Supporting the journal |

## Procedure

1. Confirm identity of the requester (email used on the owner record).
2. List horses and signed journals (`HasSignedJournals`).
3. If signed journals exist: soft-delete is **not** enough to forget the person — explain retention; offer a register extract; stop marketing; disable portal login.
4. If no signed journals: soft-delete owner and horses, cancel future bookings, delete portal user.
5. Write an audit note (who requested, date, what was kept).
6. Confirm in writing.

Application users (`equine_app`) cannot `DELETE` from `journal_entries`, `journal_amendments`, or `audit_log`. Do not work around that with the migrate role unless a court or retention expiry requires it.
