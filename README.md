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

Beschikbare rollen: `Finance`, `Sales`, `Marketing`, `Customerservice`, `Productdevelopment`, `IT` en `Beheerder`. Een beheerder kan alle afdelingen, `/admin` en `/admin/users` openen. Home en uitloggen blijven beschikbaar voor alle ingelogde gebruikers, ook zonder afdelingsrollen.

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

### Tests uitvoeren

```bash
dotnet test tui.IntegrationTests/tui.IntegrationTests.csproj
```

De tests voor afscherming gebruiken afzonderlijke testidentiteiten met Finance, Sales, Beheerder, Admin, Finance+Sales en zonder rollen. Ze draaien zonder databaseverbinding, met de echte Blazor-pagina's, policies, cookie-validatie en navigatie en een vervangende rollenbron. Ze controleren directe URL-toegang, zichtbare navigatie en knoppen, rollenwijzigingen, foutafhandeling, sessieverloop en uitloggen.

De integratietests voor gebruikersbeheer draaien tegen echte MariaDB zodra `VOYAGEVISTA_TEST_DATABASE` een serververbinding bevat met rechten om tijdelijke databases te maken. Elke test maakt een eigen schema met de prefix `voyagevista_role_tests_` en ruimt dit daarna op. De applicatiedatabase wordt niet gebruikt. Zonder deze variabele worden alleen de MariaDB-tests overgeslagen.

```powershell
$env:VOYAGEVISTA_TEST_DATABASE = 'Server=127.0.0.1;Port=3306;User=<testbeheerder>;Password=<testwachtwoord>;'
dotnet test tui.IntegrationTests/tui.IntegrationTests.csproj
```

Deze tests controleren migratie en behoud van bestaande rollen, ophalen en zoeken, toevoegen/verwijderen, dubbele en ongeldige rollen, opnieuw laden, 401/403, antiforgery, gelijktijdige beheerderwijzigingen en rollback na een schrijffout.

Voor een handmatige controle: registreer lokale testaccounts via de bestaande registratiepagina en wijs via `/admin/users` Finance, Sales, Beheerder en Finance+Sales toe. Controleer de afdelingspagina's en navigatie, ook na het vernieuwen van de pagina. Wijzig een rol terwijl die gebruiker is ingelogd en controleer de wijziging na maximaal 30 seconden. Log uit en controleer dat directe afdeling-URL's naar de loginpagina leiden. Gebruik eigen lokale testwachtwoorden; er worden geen vaste testwachtwoorden meegeleverd.
