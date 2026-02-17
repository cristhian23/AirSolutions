# task-tracker (.NET Core)

App full-stack para gestionar tareas (CRUD) con backend ASP.NET Core + SQLite y frontend HTML/CSS/JS (Materialize), servido por el mismo backend.

## Requisitos
- .NET SDK 10.0+ (o compatible con `net10.0`)
- Windows PowerShell

## Instalacion
```powershell
cd .\task-tracker
$env:DOTNET_CLI_HOME = (Get-Location).Path

dotnet restore .\task-tracker.slnx
```

## Ejecutar la app
### Modo desarrollo
```powershell
cd .\task-tracker
$env:DOTNET_CLI_HOME = (Get-Location).Path

dotnet run --project .\src\TaskTracker.Api\TaskTracker.Api.csproj
```
Abrir: `http://localhost:5000` o la URL indicada en consola.

### Modo start (equivalente produccion local)
```powershell
cd .\task-tracker
$env:DOTNET_CLI_HOME = (Get-Location).Path

dotnet run --configuration Release --project .\src\TaskTracker.Api\TaskTracker.Api.csproj
```

## Ejecutar tests
```powershell
cd .\task-tracker
$env:DOTNET_CLI_HOME = (Get-Location).Path

dotnet test .\tests\TaskTracker.Api.Tests\TaskTracker.Api.Tests.csproj
```

## Reset de base de datos
La DB se crea en `./data/app.db`.

```powershell
cd .\task-tracker
.\scripts\db-reset.ps1
```

## API
Formato de respuesta consistente:
```json
{
  "ok": true,
  "data": {},
  "error": null
}
```

En error:
```json
{
  "ok": false,
  "data": null,
  "error": {
    "message": "Validation failed.",
    "details": ["..."]
  }
}
```

### Endpoints
- `GET /api/tasks`
- `GET /api/tasks/{id}`
- `POST /api/tasks`
- `PUT /api/tasks/{id}`
- `PATCH /api/tasks/{id}/status`
- `DELETE /api/tasks/{id}`

### Ejemplos curl
Crear:
```powershell
curl -X POST http://localhost:5000/api/tasks `
  -H "Content-Type: application/json" `
  -d '{"title":"Preparar demo","priority":"high","dueDate":"2026-03-01"}'
```

Listar con filtros:
```powershell
curl "http://localhost:5000/api/tasks?status=todo&priority=high&search=demo&sort=dueDate&order=asc"
```

Cambiar estado:
```powershell
curl -X PATCH http://localhost:5000/api/tasks/1/status `
  -H "Content-Type: application/json" `
  -d '{"done":true}'
```

Eliminar:
```powershell
curl -X DELETE http://localhost:5000/api/tasks/1
```

## Notas de diseno
- Arquitectura simple por capas: endpoint minimal API + repositorio SQLite.
- Validaciones centralizadas en `Validation/TaskValidator.cs`.
- Consultas SQL parametrizadas para evitar inyeccion SQL.
- Frontend desacoplado en `wwwroot` consumiendo API via `fetch`.
- Logging basico de requests con middleware custom.