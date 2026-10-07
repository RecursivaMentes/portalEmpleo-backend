# portalEmpleo-backend

API del portal de empleos, construida con .NET 10, Entity Framework Core y PostgreSQL.

## Estructura

```
src/
├── Api             Punto de entrada web (controladores / endpoints)
├── Application     Casos de uso
├── Domain          Entidades y enums
└── Infrastructure  Acceso a datos (AppDbContext, configuraciones EF Core, migraciones)
```

## Requisitos

- [SDK de .NET 10](https://dotnet.microsoft.com/download/dotnet/10.0)
- [PostgreSQL](https://www.postgresql.org/download/)
- Herramienta de migraciones de EF Core (se instala una sola vez):

```bash
dotnet tool install --global dotnet-ef
```

## Configurar la base de datos local

Las migraciones ya están en el repositorio (`src/Infrastructure/Persistence/Migrations`). Cada persona solo tiene que aplicarlas a su propia base local. Los comandos se ejecutan desde la carpeta raíz del repo.

### 1. Crear la base de datos

Pedirá la contraseña del usuario `postgres` que definiste al instalar PostgreSQL.

```bash
psql -U postgres -h localhost -c "CREATE DATABASE portalempleo;"
```

> En Windows, si `psql` no se reconoce, usa la ruta completa, por ejemplo
> `"C:\Program Files\PostgreSQL\18\bin\psql.exe"`.

### 2. Guardar la cadena de conexión como secreto

La contraseña **no** va en el código ni en git. Se guarda con *user-secrets*, que la deja en tu máquina. Reemplaza `TU_PASSWORD` por tu contraseña.

```bash
dotnet user-secrets set "ConnectionStrings:DefaultConnection" "Host=localhost;Port=5432;Database=portalempleo;Username=postgres;Password=TU_PASSWORD" --project src/Api
```

Este valor tiene prioridad sobre el de `appsettings.Development.json`, que no incluye contraseña.

### 3. Aplicar las migraciones

```bash
dotnet ef database update --project src/Infrastructure --startup-project src/Api
```

Esto crea todas las tablas en tu base local.

## Ejecutar la API

```bash
dotnet run --project src/Api --launch-profile http
```

Queda disponible en `http://localhost:5077`.

## Trabajar con migraciones

- **No ejecutes `migrations add` para configurar tu base.** Eso crea una migración nueva y duplicada. Para tener las tablas solo hace falta `database update`.
- Cada persona tiene su propia base local: se comparte el esquema, no los datos.
- **Si cambias las entidades o sus configuraciones**, genera una migración nueva y súbela al repo:

```bash
dotnet ef migrations add NombreDelCambio --project src/Infrastructure --startup-project src/Api --output-dir Persistence/Migrations
```

- Después de hacer `git pull` con una migración nueva, vuelve a ejecutar `database update`.
- Para deshacer la última migración, antes de subirla: `dotnet ef migrations remove --project src/Infrastructure --startup-project src/Api`.
