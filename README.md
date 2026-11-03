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