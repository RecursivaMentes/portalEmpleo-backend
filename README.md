# portalEmpleo-backend

Backend del portal de empleos, desarrollado con **.NET 10**, Entity Framework Core y PostgreSQL, organizado mediante una arquitectura de **microservicios**.

Cada microservicio cuenta con sus propios proyectos o capas y administra su propia base de datos.

## Estructura del proyecto

```text
src/
└── Services/
    ├── Usuarios/
    │   └── Api/
    └── ...
```

Cada directorio dentro de `Services` representa un microservicio. Su estructura interna puede incluir proyectos para la API, la lógica de aplicación, el dominio y el acceso a datos, según la organización de cada servicio.

## Requisitos

* [.NET 10 SDK](https://dotnet.microsoft.com/download/dotnet/10.0)
* [PostgreSQL](https://www.postgresql.org/download/)
* Herramienta de migraciones de Entity Framework Core, si el servicio utiliza EF Core:

```bash
dotnet tool install --global dotnet-ef
```

Si ya está instalada, no hace falta ejecutar nuevamente este comando.

## Configuración de la base de datos

Cada microservicio debe utilizar su propia base de datos. Las bases locales se configuran de manera independiente.

Para configurar un servicio:

1. Crear la base de datos correspondiente en PostgreSQL.
2. Configurar la cadena de conexión mediante el mecanismo de configuración utilizado por ese servicio.
3. Aplicar las migraciones existentes, si las tiene.

No se deben compartir las credenciales ni las cadenas de conexión entre servicios si corresponden a bases de datos diferentes.

## Ejecutar un microservicio

Para iniciar el microservicio de Usuarios, ejecutar desde la raíz del repositorio:

```bash
dotnet run --project src/Services/Usuarios/Api
```

Actualmente, en el entorno de desarrollo, la API de Usuarios escucha en:

```text
http://localhost:5078
```

Los demás microservicios deben ejecutarse utilizando la ruta de su propio proyecto `Api` y el puerto configurado para cada uno.

## Trabajar con migraciones

Las migraciones deben gestionarse de forma independiente para cada microservicio que utilice Entity Framework Core.

* Si el repositorio ya contiene las migraciones, aplicar las existentes; no generar migraciones nuevas únicamente para configurar una base local.
* Cuando se modifican las entidades o sus configuraciones, generar una migración nueva en el proyecto correspondiente.
* Después de incorporar migraciones nuevas mediante Git, aplicar las pendientes a la base de datos local del servicio.
* No ejecutar comandos de migración apuntando a rutas de otro microservicio.
* Antes de aplicar migraciones, verificar qué proyecto contiene el `DbContext`, cuál es el proyecto de inicio y dónde se almacenan las migraciones.

Los comandos concretos de Entity Framework Core deben definirse según la estructura real de cada microservicio.

## Desarrollo

Los endpoints y las funcionalidades de cada microservicio se irán incorporando progresivamente. La documentación se actualizará a medida que se agreguen servicios y se definan sus configuraciones.
