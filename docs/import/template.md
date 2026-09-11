# Historisk import — CSV-mall

Importera befintliga kunder, hästar och behandlingar med `tools/Equine.Import`. Kör alltid dry-run först. Fyll i mallarna; hitta inte på kliniskt innehåll som saknas i källan.

## Format

- Semikolon-separerat, UTF-8 (BOM tillåten)
- Datum: `yyyy-MM-dd` eller `yyyy-MM-dd HH:mm` (tolkas som UTC om zon saknas)
- Tom cell = fältet lämnas tomt
- Hästar kräver `birth_year` **eller** `age_group`

Exempelfiler: `owners.sample.csv`, `horses.sample.csv`, `journals.sample.csv`.

### owners.csv

`name;email;phone;address_street;address_postcode;address_city;notes`

### horses.csv

`owner_email;name;species;sex;birth_year;age_group;identity;breed;colour;markings;stable_city;background`

- `species`: `Hast` (standard), `Ko`, `Sva`, `Katt`, `Hund`
- `sex`: `Sto`, `Valack`, `Hingst`, `Okant`

### journals.csv

`owner_email;horse_name;performed_at;treatment_name;anamnes;status_klinisk;atgarder;diagnos;differentialdiagnoser;prognos_och_plan`

Importerade journaler signeras med `source = Import` och originaldatum. Tomma journalfält förblir tomma.

## Körning

```bash
# Förhandsgranska
dotnet run --project tools/Equine.Import -- --owners owners.csv --horses horses.csv --journals journals.csv

# Skriv (först mot staging)
dotnet run --project tools/Equine.Import -- --owners owners.csv --horses horses.csv --journals journals.csv --commit --report import-report.json
```

Anslutning: `ConnectionStrings__Default` eller `--connection`.

## Dubbletter

- Ägare: samma e-post (citext) eller samma telefon (endast siffror)
- Häst: samma namn + ägare (skiftlägesokänsligt)
- Journal: samma häst + samma kalenderdag + samma behandlingsnamn

Dubbletter hoppas över, de skrivs inte över.

## Avstämning

`import-report.json` innehåller antal in, skapade, överhoppade (rad + orsak) och dubblettlista. Praktiker spot-checkar 20 slumpade rader mot källan innan produktion.
