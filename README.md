## Start het project
```bash
dotnet watch
```

## Setup database verbinding
### Als je xampp gebruikt hoef je dit niet te doen.
```bash
dotnet user-secrets set "Db:password" ""
dotnet user-secrets set "Db:host" "localhost"
dotnet user-secrets set "Db:port" 3306
dotnet user-secrets set "Db:name" "tui"
dotnet user-secrets set "Db:user" "root"
```

## Hoe maak je een migratie?
### Installeer dotnet ef
```bash
dotnet tool install --global dotnet-ef
```
### Maak de migratie
```bash
dotnet ef migrations add <naam>
```

## Rollen en toegang

De bestaande cookie-login leest de toegewezen rollen via `AppUsers → UserRoles → Roles` en maakt voor iedere rol een afzonderlijke role-claim. `Roles` bevat de bestaande rollen; `UserRoles` legt de koppelingen vast met een samengestelde primaire sleutel, zodat een rol maar één keer aan dezelfde gebruiker gekoppeld kan worden. Ontbrekende rollen geven geen afdelingstoegang.

Beschikbare afdelingsrollen: `Finance`, `Sales`, `Marketing`, `Customerservice`, `Productdevelopment`, `IT` en `Beheerder`. Daarnaast bestaan de factuurrollen `medewerker`, `controller` en `read_only`. Een beheerder kan alle afdelingen, `/admin` en `/admin/users` openen. Home en uitloggen blijven beschikbaar voor alle ingelogde gebruikers, ook zonder afdelingsrollen.

Het e-mailadres `ADMIN@ADMIN.COM` geeft geen automatische beheerdersrechten. Het bestaande lokale account heeft expliciet de rol `Beheerder`. Nieuwe registraties krijgen geen afdelingsrol; een beheerder kan deze via gebruikersbeheer toewijzen.

De migratie `NormalizeUserRoles` maakt de rollentabellen aan, vult de bestaande rollen en neemt toewijzingen uit het oude veld `AppUsers.UserRole` over. Daarbij worden komma's, puntkomma's, spaties en hoofdletterverschillen verwerkt en wordt `Admin` omgezet naar `Beheerder`. Pas daarna vervalt het oude veld. Een rollback zet de rollen terug in het tekstveld. De applicatie voert migraties bij het opstarten uit; `dotnet ef database update` kan dit ook vooraf doen.

### Gebruikersbeheer

Open **IT & Innovatie → Gebruikers** in de navigatie of ga naar `/admin/users`. Zoek op e-mailadres, selecteer **Wijzigen**, vink één of meerdere rollen aan of uit en klik op **Wijzigingen opslaan**. Het overzicht toont daarna de opgeslagen rollen en een bevestiging. **Herstellen** zet niet-opgeslagen keuzes terug; **Vernieuwen** haalt het overzicht opnieuw op.

De frontend doet browseraanvragen naar de C#-API met de bestaande sessiecookie:

| Endpoint | Functie |
| --- | --- |
| `GET /api/admin/users?search=...` | Gebruikers met hun huidige rollen; zoeken op deel van het e-mailadres |
| `GET /api/admin/roles` | Beschikbare rollen uit MariaDB |
| `GET /api/admin/antiforgery` | Beveiligingstoken voor de ingelogde beheerder |
| `PUT /api/admin/users/{userId}/roles` | Volledige rolselectie opslaan, bijvoorbeeld `{"roles":["Finance","Sales"]}` |

Iedere aanvraag vereist Beheerder. De API geeft 401 zonder sessie en 403 zonder beheerdersrol. Wijzigingen vereisen bovendien het antiforgerytoken in de header `RequestVerificationToken`. De API controleert de actuele beheerdersrol opnieuw binnen de schrijftransactie, valideert rollen en slaat alles via Entity Framework op in MariaDB. Onbekende gebruikers geven 404, ongeldige rollen 400 en het verwijderen van de laatste beheerder 409. Een lege rollenlijst verwijdert alle rollen, behalve wanneer daardoor geen beheerder meer zou overblijven.

Rolwijzigingen gebruiken een transactie met een lock op de catalogusrij Beheerder. Daardoor kunnen twee gelijktijdige aanvragen niet samen de laatste beheerders verwijderen. Bij een mislukte schrijfactie wordt de transactie teruggedraaid. De pagina biedt geen registratie, accountverwijdering, wachtwoordbeheer of het maken/verwijderen van rollen.

### Rollen gebruiken in de frontend

Gebruik dezelfde centrale policy voor een pagina, navigatielink en bedieningselement:

```razor
@attribute [Microsoft.AspNetCore.Authorization.Authorize(Policy = AccessPolicies.Finance)]

<RoleView Policy="@AccessPolicies.Finance">
    <button>Finance-actie</button>
</RoleView>
```

Policies staan in `Services/Auth/AccessPolicies.cs` en geven automatisch toegang aan Beheerder. `RoleView` reageert op wijzigingen in de authenticatiestatus. De bestaande dashboardknoppen gebruiken deze component ook. Afdelingspagina's blijven verder ongewijzigd.

Bij iedere nieuwe HTTP-aanvraag worden de actuele rollen uit de database gelezen. Een actieve Blazor-sessie controleert de rollen iedere 30 seconden en werkt navigatie, pagina-autorisatie en bedieningselementen bij. Een ingetrokken rol verdwijnt daardoor ook zonder opnieuw inloggen. Sessies verlopen na acht uur; ontbrekende gebruikers, verlopen sessies en fouten bij het laden van rollen sluiten toegang af en leiden naar de loginpagina. Er is geen refresh-tokenmechanisme in de bestaande cookie-authenticatie. Onbevoegde ingelogde gebruikers zien `Toegang geweigerd`, ook bij directe URL-toegang.

### Facturenoverzicht

Open **Financiën → Facturen** of ga naar `/finance/invoices`. De pagina en lees-API vereisen Finance, Beheerder of een van de factuurrollen `medewerker`, `controller` en `read_only`. De tabel toont de opgeslagen factuurreferentie, ontvanger (`Party.Name`), factuurdatum, totaalbedrag met valuta en betaalstatus. De status komt rechtstreeks uit `Invoice.Status`; deze pagina berekent geen betaalstatus op basis van betalingen en wijzigt geen facturen.

Via `GET /api/finance/invoices?search=...&status=Alle&page=1` worden de gegevens door Entity Framework uit MariaDB opgehaald. `status` accepteert `Alle` (standaard), `Openstaand` en `Betaald`. Zoeken werkt op een volledige of gedeeltelijke factuurreferentie of ontvanger, zonder onderscheid tussen hoofdletters en kleine letters. Zoeken en het statusfilter worden gecombineerd. De volgorde is factuurdatum aflopend, gevolgd door factuur-ID aflopend voor een vaste volgorde bij gelijke datums.

De API levert maximaal 25 facturen per pagina, het aantal overeenkomende facturen, het totale aantal geregistreerde facturen en het aantal pagina's. Een te hoog paginanummer wordt begrensd tot de laatste beschikbare pagina. Ongeldige filters en paginanummers geven 400, aanvragen zonder sessie 401 en onbevoegde gebruikers 403. Bij een laadfout verschijnt een melding met **Opnieuw proberen**.

De zoekopdracht en betaalstatus blijven behouden bij **Vorige** en **Volgende**. Wijzigen van de zoekopdracht of status begint op pagina één. **Wissen** verwijdert alleen de zoekopdracht en behoudt het actieve statusfilter. Tijdens het typen wordt kort gewacht voordat de API wordt aangeroepen; verouderde antwoorden kunnen nieuwere resultaten niet overschrijven. Een lege database toont **Er zijn nog geen facturen geregistreerd**; geen overeenkomende resultaten toont **Geen facturen gevonden**.

De overzichtspagina blijft alleen-lezen. De onderstaande backendworkflow biedt de afzonderlijke mutatie- en detailendpoints. Betalingen en export vallen daarbuiten.

### Factuurworkflow via de backend

De bestaande tabel `Invoices` is uitgebreid met `Currency`, `WorkflowStatus`, maker, beoordelaar, UTC-tijdstippen, toelichting en `Version`. Er wordt geen tweede, concurrerende tabel `invoices` gemaakt. De bestaande `Status` blijft de betaalstatus (`Openstaand`/`Betaald`); `WorkflowStatus` heeft de waarden `concept`, `ter goedkeuring`, `goedgekeurd` en `afgewezen`.

Rollen worden gecontroleerd via de bestaande koppeling `AppUsers → UserRoles → Roles`, het equivalent van `users.roles` in deze applicatie. Ze zijn toe te wijzen via **IT & Innovatie → Gebruikers**:

| Rol | Lijst en detail | Aanmaken, concept bewerken/verwijderen, indienen | Goedkeuren/afwijzen |
| --- | --- | --- | --- |
| `medewerker` | Ja | Ja | Nee |
| `controller` | Ja | Nee | Ja, uitsluitend ter goedkeuring en nooit eigen facturen |
| `read_only` | Ja | Nee | Nee |
| `Finance` / `Beheerder` | Ja | Alleen met extra rol `medewerker` | Alleen met extra rol `controller` |
| Overige rollen | Nee | Nee | Nee |

Meerdere rollen combineren de rechten. `read_only` is geen verbod op rechten uit een andere toegewezen rol. Ook voor een beheerder is de expliciete operationele rol nodig voor schrijven of beoordelen; de controle op eigen facturen blijft altijd gelden.

| Methode en endpoint | Actie |
| --- | --- |
| `GET /api/finance/invoices` | Gepagineerde lijst, met bestaande zoek- en betaalstatusfilters en optioneel `workflowStatus` |
| `GET /api/finance/invoices/{id}` | Detail inclusief valuta, verwerkingsstatus, maker, beoordelaar en versie |
| `GET /api/finance/invoices/antiforgery` | Antiforgerytoken voor de bestaande sessiecookie |
| `POST /api/finance/invoices` | Concept aanmaken |
| `PUT /api/finance/invoices/{id}` | Concept bewerken met de opgehaalde versie |
| `DELETE /api/finance/invoices/{id}?version=1` | Concept verwijderen |
| `POST /api/finance/invoices/{id}/submit` | Concept indienen ter goedkeuring |
| `POST /api/finance/invoices/{id}/approve` | Ingediende factuur goedkeuren |
| `POST /api/finance/invoices/{id}/reject` | Ingediende factuur afwijzen |

Alle mutaties vereisen de sessiecookie en het token uit `/antiforgery` in de header `RequestVerificationToken`. Voorbeeld voor aanmaken:

```json
{
  "reference": "SUPPLIER-2026-001",
  "partyId": 1,
  "bookingId": null,
  "invoiceType": "Inkoop",
  "invoiceDate": "2026-10-27",
  "dueDate": "2026-11-27",
  "totalAmount": 125.25,
  "currency": "EUR"
}
```

De ontvanger moet al bestaan; een eventuele boeking ook. Referentie (maximaal 50 tekens), type (maximaal 20 tekens), ontvanger, datum, bedrag en valuta zijn verplicht. Het bedrag moet positief zijn en binnen `decimal(14,4)` passen. Valuta worden genormaliseerd naar hoofdletters en gevalideerd tegen de officiële [SIX ISO 4217 List One](https://www.six-group.com/dam/download/financial-information/data-center/iso-currrency/lists/list-one.xml), publicatie 2026-09-17. Alleen codes met numerieke minor units worden ondersteund; testcodes, 'geen valuta' en edelmetaalcodes worden geweigerd. Te veel decimalen voor de valuta worden geweigerd, nooit stilzwijgend afgerond. De overzichtspagina toont het bedrag met de opgeslagen valutacode.

Bij bewerken wordt dezelfde JSON gebruikt met het extra veld `"version": 1`. Indienen, goedkeuren en afwijzen gebruiken bijvoorbeeld `{"version":2,"comment":"Gecontroleerd"}`; de toelichting is optioneel en maximaal 1000 tekens. Maker, verwerkingsstatus, betaalstatus en beoordelaar worden door de server bepaald en kunnen niet via extra invoervelden worden overschreven. Alleen concepten zijn bewerkbaar/verwijderbaar; goedgekeurde en afgewezen facturen zijn definitief in deze PBI. Een concept met betalingen of journaalposten kan niet worden verwijderd.

De API geeft 201 bij aanmaken, 200 bij ophalen/bewerken/statuswijzigingen en 204 bij verwijderen. Validatiefouten geven 400, ontbrekende sessies 401, ontbrekende rechten of eigen beoordelingen 403, ontbrekende facturen 404 en dubbele referenties, ongeldige statusovergangen of verouderde versies 409. Bij een versieconflict moet de client het detail opnieuw ophalen. Gelijktijdige beoordelingen kunnen maar één beslissing vastleggen.

US-8 was nog niet aanwezig in deze repository. Daarom is een algemene tabel `AuditLog` toegevoegd met entiteit/id, actie, actor, UTC-tijdstip en JSON van de toestand vóór en na de wijziging. `create`, `update`, `submit`, `approve`, `reject` en `delete` worden gelogd. De auditlog blijft bestaan nadat een concept is verwijderd. Factuurmutatie en auditregel worden samen gecommit; een auditfout draait ook de factuurmutatie terug. Er zijn geen publieke API-endpoints om auditregels te wijzigen of verwijderen.

De migratie `InvoiceWorkflowAndAudit` voegt deze velden, de auditlog en de drie factuurrollen toe. Bestaande facturen behouden hun bedrag en betaalstatus en krijgen valuta EUR, verwerkingsstatus concept en versie 0. Een historische maker wordt niet verzonnen: records zonder bekende maker kunnen niet worden ingediend of beoordeeld. Nieuw aangemaakte facturen krijgen altijd de ingelogde medewerker als maker. De migratie wordt bij opstarten toegepast, of vooraf met `dotnet ef database update`. Een rollback verwijdert de nieuwe workflow-/auditgegevens en kan grotere bedragen of extra decimalen niet behouden.

### Handmatige registratie van ontvangen bronfacturen

Open **Financiën → Factuur registreren** of `/finance/invoices/new` met de rol `medewerker`. De knop staat ook op het facturenoverzicht. Kies een bestaande ontvanger, neem de afzender- en factuurgegevens over en voeg één of meerdere regels toe. Regels kunnen worden gewijzigd, verwijderd en met de pijlen verplaatst. Een nieuw ontvangeraccount maken is geen onderdeel van dit formulier.

Het Financiën-overzicht (`/finance`) biedt directe links naar facturen en registratie. Op de Finance-pagina's staat een vaste navigatiebalk met actieve paginamarkering en een broodkruimelpad. De registratielink blijft beperkt tot gebruikers met schrijfrechten.

Verplicht zijn het oorspronkelijke factuurnummer, factuurdatum, bestaande ontvanger met naam en adres, afzendernaam/-adres en minimaal één geldige factuurregel. Per regel zijn omschrijving, positief aantal, een niet-negatieve eenheidsprijs en btw-behandeling verplicht. Eenheden zijn vrij in te vullen. Leveringsdatum óf volledige leveringsperiode, onderwerp, betalingskenmerk, betalingstermijn/vervaldatum, KVK-/btw-gegevens en opmerkingen zijn beschikbaar. Een betalingstermijn berekent de vervaldatum; als beide worden opgegeven moeten ze overeenkomen. Bij verlegging zijn beide btw-identificatienummers en de informatie van de bronfactuur nodig.

De afzender wordt handmatig ingevoerd, zoals afgestemd voor deze PBI. Het systeem registreert ontvangen documenten en maakt geen nieuwe uitgaande factuur. Volgens de [Belastingdienst: factuureisen](https://www.belastingdienst.nl/wps/wcm/connect/bldcontentnl/belastingdienst/zakelijk/btw/administratie_bijhouden/facturen_maken/factuureisen/factuureisen) horen opeenvolgende, unieke factuurnummers bij de nummering van de uitreikende partij. Bij [ontvangen facturen](https://www.belastingdienst.nl/wps/wcm/connect/bldcontentnl/belastingdienst/zakelijk/btw/administratie_bijhouden/wat_administreert_u_voor_de_btw/ontvangen_facturen_administreren) wordt het nummer van de leverancier vastgelegd. Daarom blijft het oorspronkelijke nummer apart behouden en krijgt de registratie een unieke interne `REG-…`-referentie. Er wordt geen eigen uitgaande nummerreeks opgelegd aan leveranciersnummers.

Als applicatiecontrole tegen dubbele invoer wordt hetzelfde oorspronkelijke nummer bij dezelfde afzender geblokkeerd (409); hetzelfde nummer bij verschillende afzenders is toegestaan. De controle gebruikt btw-id, KVK en naam/adres, plus een genormaliseerde identiteitssleutel. Controle en opslag worden geserialiseerd binnen een transactie, zodat gelijktijdige invoer de controle niet kan omzeilen. Dit is een registratiecontrole, geen afzonderlijke wettelijke eis voor alle ontvangen facturen samen. De registratie vervangt het bewaren van het originele document niet en pretendeert geen volledige juridische factuurcontrole.

#### Berekeningen en opslag

De gedeelde C#-berekening staat in `ManualInvoiceCalculator`. De pagina en de API gebruiken dezelfde regels:

1. Hoeveelheid × eenheidsprijs exclusief btw, afgerond op de valutaprecisie.
2. Korting per regel: percentage óf vast bedrag, nooit beide. De korting wordt op valutaprecisie afgerond en mag het regelbedrag niet overschrijden.
3. Het resterende regelbedrag is de btw-grondslag. Btw wordt per regel berekend en afgerond met `MidpointRounding.AwayFromZero`.
4. Regelbedrag inclusief btw = grondslag + btw. De factuurtotalen zijn de som van deze opgeslagen regelbedragen.

Ondersteunde btw-keuzes voor deze casus zijn 21%, 9%, 0%, vrijgesteld en verlegd. Vrijstelling en verlegging blijven aparte groepen, ook al wordt er 0 btw berekend; hun toelichting wordt opgeslagen. De totalen tonen subtotaal vóór korting, korting, totaal exclusief btw, grondslag/btw per behandeling, totaal btw en totaal inclusief btw. Standaardvaluta is EUR; de bestaande ISO-valutalijst bepaalt de precisie. Een optioneel bronbedrag wordt gecontroleerd tegen het berekende totaal. Verschillen worden gemeld en niet automatisch weggeboekt.

De technische limiet is 200 regels, hoeveelheden tot 999999,999999 en maximaal 6 decimalen voor hoeveelheid/eenheidsprijs. Bedragen en factuurtotalen moeten binnen `decimal(14,4)` passen; het totale factuurbedrag moet positief zijn. De Blazor-berichtlimiet is 4 MiB om volledige registraties binnen deze invoerlimieten te kunnen uitwisselen.

`ManualInvoiceRegistrations` bewaart algemene brongegevens, ontvangersnapshot, totalen en berekeningsversie `per-line-away-from-zero-v1`. `InvoiceLines` bewaart volgorde, oorspronkelijke invoer, korting, btw-behandeling en alle berekende bedragen. Grootboekrekening en btw-stamrecord zijn voor deze bronregistratie niet verplicht; btw-informatie wordt als snapshot vastgelegd. Latere wijzigingen aan ontvanger- of btw-stamgegevens veranderen de opgeslagen factuur niet.

Factuurkop, registratiegegevens, alle regels en de auditregel `register_manual` worden samen gecommit. Een validatie- of schrijffout laat geen gedeeltelijke registratie achter. Veld-/regelfouten worden bij de invoer getoond en de invoer blijft behouden. Na opslaan verschijnt een bevestiging met de interne referentie en betaalstatus `Openstaand`.

De registratie is via het overzicht terug te vinden op interne referentie, oorspronkelijk nummer of ontvanger en kan opnieuw worden geopend op `/finance/invoices/{id}`. De volledige brongegevens, regels in oorspronkelijke volgorde en opgeslagen totalen zijn daar alleen-lezen te bekijken. De bestaande conceptworkflow blijft beschikbaar voor andere facturen; handmatige bronregistraties zijn geblokkeerd voor die mutatie-/goedkeuringsendpoints. Bewerken na opslag, goedkeuring, documentgeneratie, import/OCR en betaalverwerking vallen buiten deze PBI.

#### API voor handmatige registratie

| Endpoint | Rechten / functie |
| --- | --- |
| `GET /api/finance/invoices/manual/options` | `medewerker`: bestaande ontvangers en ondersteunde valuta |
| `POST /api/finance/invoices/manual` | `medewerker`: volledige bronfactuur registreren (201) |
| `GET /api/finance/invoices/manual/{id}` | Bestaande Finance-leesrechten: registratie inclusief snapshots, regels en totalen |

POST vereist de bestaande sessiecookie en het token uit `/api/finance/invoices/antiforgery` in de header `RequestVerificationToken`. Bedragen en interne referentie worden door de server bepaald. Bij ongeldige invoer bevat het antwoord `errors` met sleutels zoals `senderAddress`, `partyId` en `lines[0].quantity`.

```json
{
  "originalInvoiceNumber": "LEV-2026-001",
  "partyId": 1,
  "invoiceDate": "2026-10-28",
  "senderName": "Leverancier BV",
  "senderAddress": "Voorbeeldstraat 1, Utrecht",
  "senderKvkNumber": "12345678",
  "senderVatNumber": "NL999999999B01",
  "paymentTermDays": 30,
  "currency": "EUR",
  "lines": [
    { "description": "Advies", "quantity": 2, "unit": "uren", "unitPrice": 50, "discountPercentage": 10, "vatTreatment": "regular", "vatPercentage": 21 }
  ]
}
```

De migratie `ManualInvoiceRegistration` behoudt bestaande facturen en voegt de bronregistraties en regelvelden toe. De vervangende regelindex wordt vóór het verwijderen van de oude index aangemaakt, zodat de MariaDB-foreign key ondersteund blijft. Zoals bij andere datamigraties verwijdert een rollback de nieuwe registratiegegevens; terugzetten van handmatige regels naar het oude verplichte grootboekmodel vereist een afzonderlijk herstelplan.

### Tests uitvoeren

```bash
dotnet test tui.IntegrationTests/tui.IntegrationTests.csproj
```

De tests voor afscherming gebruiken afzonderlijke testidentiteiten met Finance, Sales, Beheerder, Admin, Finance+Sales en zonder rollen. Ze draaien zonder databaseverbinding, met de echte Blazor-pagina's, policies, cookie-validatie en navigatie en een vervangende rollenbron. Ze controleren directe URL-toegang, zichtbare navigatie en knoppen, rollenwijzigingen, foutafhandeling, sessieverloop en uitloggen.

De integratietests voor gebruikersbeheer en facturen draaien tegen echte MariaDB zodra `VOYAGEVISTA_TEST_DATABASE` een serververbinding bevat met rechten om tijdelijke databases te maken. Elke test maakt een eigen schema met de prefix `voyagevista_role_tests_` en ruimt dit daarna op. De applicatiedatabase wordt niet gebruikt. Zonder deze variabele worden alleen de MariaDB-tests overgeslagen.

```powershell
$env:VOYAGEVISTA_TEST_DATABASE = 'Server=127.0.0.1;Port=3306;User=<testbeheerder>;Password=<testwachtwoord>;'
dotnet test tui.IntegrationTests/tui.IntegrationTests.csproj
```

Deze tests controleren migratie en behoud van bestaande rollen, ophalen en zoeken, toevoegen/verwijderen, dubbele en ongeldige rollen, opnieuw laden, 401/403, antiforgery, gelijktijdige beheerderwijzigingen en rollback na een schrijffout.

De factuurtests controleren de tabelgegevens tegen MariaDB, vaste sortering, pagina's van maximaal 25 resultaten, gecombineerde zoek- en statusfilters, lege resultaten, fouten en bevoegdheden. De UI-tests gebruiken de echte Razor-events en een gecontroleerde browser-API om laden, foutmelding en opnieuw proberen, wissen, paginareset en het negeren van verouderde zoekresultaten te controleren.

Voor een handmatige controle: registreer lokale testaccounts via de bestaande registratiepagina en wijs via `/admin/users` Finance, Sales, Beheerder en Finance+Sales toe. Controleer de afdelingspagina's en navigatie, ook na het vernieuwen van de pagina. Wijzig een rol terwijl die gebruiker is ingelogd en controleer de wijziging na maximaal 30 seconden. Log uit en controleer dat directe afdeling-URL's naar de loginpagina leiden. Gebruik eigen lokale testwachtwoorden; er worden geen vaste testwachtwoorden meegeleverd.
