# Användarmanual (kort)

Språk i appen: svenska. Tider visas i svensk tid.

## Logga in

1. Öppna adminadressen. E-post och lösenord.
2. Aktivera tvåfaktorsinloggning (TOTP) under **Inställningar → Konto** om det inte redan är gjort.
3. Vid problem: be operatören återställa lösenord; rotera inte JWT-nyckeln i onödan (alla loggas ut).

## Dagens schema

**Schema** visar dagens besök i körordning. Därifrån öppnar du bokning och journal.

## Boka

**Kalender** eller **Ny bokning**. Välj häst, behandling och tid. Bekräfta widgetförfrågningar under **Inkomna förfrågningar**.

## Journal

1. Öppna hästen eller bokningen → ny journal / utkast.
2. Fyll anamnes, status och åtgärder (krävs för signering). Lämna tomt hellre än att hitta på.
3. **Signera** i anslutning till besöket. Efter signering ändrar du bara via **ändring** (daterad).
4. Bilagor laddas upp på journalen.

Osignerade utkast syns under **Journaler → Utkast** och påminns efter 24 timmar.

## Uppföljning och utskick

**Uppföljningar** för hästar som ska tillbaka. **Utskick** endast till de som gett samtycke.

## Widget

**Inställningar → Widget**: tillåtna webbplatser, villkor, länk till integritetspolicy. Klistra in loader-scriptet på den publika sajten.

## Export och säkerhet

**Inställningar → Exportera**: fullständigt arkiv (ZIP) och journal-PDF per häst. Använd vid byta system eller inspektion.

## Om något strejkar

- Appen går inte att öppna: vänta på SMS från uptime, kontakta operatören.
- Bekräftelsemejl saknas: titta i aviseringsloggen; boka inte om i ett kalkylark.
- Backup: operatören kör restore-övning enligt driftpärmen — du behöver bara känna till att den finns.
