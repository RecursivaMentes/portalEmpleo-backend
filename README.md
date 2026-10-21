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
## Arquitectura, limites y contratos entre microservicios

El PR #3 divide el backend en cuatro servicios desplegables, con proyectos `Api`,
`Application`, `Domain` e `Infrastructure`. Cada servicio es responsable de su
modelo y de su base PostgreSQL; no hay claves foraneas entre bases distintas.

| Servicio | Datos de los que es responsable |
| --- | --- |
| Usuarios | Usuario, PerfilLaboral, Cv, Empresa y Notificacion |
| EmpleosYpostulaciones | Empleo y Postulacion |
| Chatbot | Chat y Mensaje |
| Recomendaciones | Recomendacion |

Las dependencias entre capas siguen este criterio:

- `Domain`: entidades, enumeraciones y reglas del dominio; no depende de EF Core ni de HTTP.
- `Application`: casos de uso y contratos/interfaces; depende de `Domain`, no de `Infrastructure`.
- `Infrastructure`: EF Core, repositorios y servicios tecnicos; implementa contratos de `Application`.
- `Api`: endpoints HTTP, autenticacion, autorizacion y composicion de dependencias.

### Identificadores que cruzan servicios

- `Empleo.EmpresaId` y `Empleo.UsuarioId`: referencias a identidades administradas por Usuarios.
- `Postulacion.UsuarioId` y `Postulacion.CvId`: referencias a Usuarios. `Postulacion.EmpleoId` es una relacion interna del servicio EmpleosYpostulaciones.
- `Chat.UsuarioId`: referencia al servicio Usuarios.
- `Recomendacion.UsuarioId` y `Recomendacion.EmpleoId`: referencias a Usuarios y EmpleosYpostulaciones, respectivamente.
- `Notificacion.ConversacionId`: referencia opcional a Chatbot; `Notificacion.UsuarioId` referencia a Usuarios dentro de su propia base.

Estas referencias externas son identificadores, **no claves foraneas entre bases**.
Cuando se implementen los casos de uso correspondientes, `Application` debera
validar existencia, pertenencia y autorizacion mediante contratos, con los
adaptadores de comunicacion en `Infrastructure`. El PR #3 **no implementa todavia**
esos casos de uso ni introduce clientes HTTP artificiales.

## Preparacion local de PostgreSQL y migraciones

Se necesita una sola instalacion de PostgreSQL. Como ejemplo, en el entorno local
se han utilizado estas bases independientes (los nombres son configurables):

| Proyecto de API | Base de datos de ejemplo | Puerto HTTP local |
| --- | --- | --- |
| `src/Services/Usuarios/Api/Api.csproj` | `portalempleo_usuarios` | 5078 |
| `src/Services/EmpleosYpostulaciones/Api/Api.csproj` | `portalempleo_postulaciones` | 5077 |
| `src/Services/Chatbot/Api/Api.csproj` | `portalempleo_chatbot` | 5079 |
| `src/Services/Recomendaciones/Api/Api.csproj` | `portalempleo_recomendaciones` | 5080 |

Crear las bases faltantes en PostgreSQL y configurar **por separado**
`ConnectionStrings:DefaultConnection` en los User Secrets de cada proyecto
`Api`. Por ejemplo, desde la raiz del backend:

```powershell
dotnet user-secrets set "ConnectionStrings:DefaultConnection" "Host=localhost;Port=5432;Database=portalempleo_usuarios;Username=postgres;Password=CAMBIAR" --project src/Services/Usuarios/Api/Api.csproj
```

Repetirlo para los otros tres servicios, modificando el proyecto y el nombre
de la base. No publicar credenciales ni cadenas de conexion reales en Git.

Las migraciones se aplican **manualmente**, no al iniciar las APIs:

```powershell
$servicios = @('Usuarios', 'EmpleosYpostulaciones', 'Chatbot', 'Recomendaciones')
foreach ($servicio in $servicios) {
    dotnet ef database update `
        --project "src/Services/$servicio/Infrastructure/Infrastructure.csproj" `
        --startup-project "src/Services/$servicio/Api/Api.csproj"
    if ($LASTEXITCODE -ne 0) { throw "Error de migracion en $servicio" }
}
```

Revisar cada migracion y confirmar la base de destino **antes** de ejecutarla
sobre una base que contenga datos importantes. `dotnet build PortalEmpleo.slnx`
permite compilar la solucion sin ejecutar migraciones.

## Login y JWT (Usuarios)

El endpoint disponible es `POST http://localhost:5078/api/auth/login`.
Acepta JSON con `email` y `password`, devuelve `accessToken` y
`expiresAtUtc` si las credenciales son correctas, `401` para credenciales
invalidas o usuario inactivo, y `400` si el cuerpo no supera la validacion.
Las contrasenas de los usuarios deben estar almacenadas como hash compatible
con `PasswordHasher<Usuario>`, no en texto plano.

En `Usuarios/Api/appsettings.json` estan `Jwt:Issuer`, `Jwt:Audience` y
`Jwt:ExpirationMinutes`. La clave `Jwt:Key` **no se versiona** y, en desarrollo,
debe cargarse en los User Secrets del mismo proyecto de API. Ejemplo para
generar y guardar una clave aleatoria de al menos 32 bytes:

```powershell
$bytes = New-Object byte[] 32
$rng = [System.Security.Cryptography.RandomNumberGenerator]::Create()
$rng.GetBytes($bytes)
$rng.Dispose()
$clave = [Convert]::ToBase64String($bytes)
dotnet user-secrets set "Jwt:Key" $clave --project src/Services/Usuarios/Api/Api.csproj
Remove-Variable clave
```

**No regenerar una clave existente** si hay tokens emitidos que deban seguir
siendo validos. Para entornos distintos de Development, configurar la clave
en el gestor de secretos correspondiente.

La API de Usuarios emite tokens JWT y configura su validacion. Eso **no
significa** que las APIs de los otros tres servicios ya protejan endpoints:
sus mecanismos de autenticacion se incorporaran con los casos de uso que lo
requieran. Actualmente no se dispone de un endpoint protegido para demostrar
un `401` producido por el middleware de JWT.

## Comprobaciones locales de la integracion

Ejecutar cada API con `dotnet run --project src/Services/<Servicio>/Api`.
En Development, la descripcion OpenAPI se publica en `/openapi/v1.json`
(por ejemplo, `http://localhost:5078/openapi/v1.json`).
Los archivos `.http` de cada API contienen solicitudes de ejemplo.

El frontend en desarrollo consume `GET /api/empleos`; **ese controlador aun
no forma parte del PR #3**, que prepara la estructura de microservicios.
No se incorpora aqui ese endpoint, ni CORS, ni clientes entre servicios sin
su caso de uso correspondiente.

## Chatbot (asistente de IA)

El servicio `Chatbot` expone un chat con historial respaldado por Claude (API de Anthropic). Escucha en `http://localhost:5079` y **todos sus endpoints requieren un token JWT** emitido por el servicio Usuarios (`POST /api/auth/login`). El id del usuario se toma del claim `sub` del token.

| Método | Ruta | Descripción |
| --- | --- | --- |
| `POST` | `/api/chats` | Crea un chat. Cuerpo opcional: `{ "tipo": "Asistente" }` |
| `GET` | `/api/chats` | Lista los chats del usuario |
| `GET` | `/api/chats/{chatId}/mensajes` | Devuelve el historial del chat |
| `POST` | `/api/chats/{chatId}/mensajes` | Envía `{ "contenido": "..." }`, guarda el mensaje y la respuesta del asistente y devuelve ambos |

Un usuario solo puede ver y escribir en sus propios chats (`404` en caso contrario). Si el asistente no está disponible la API responde `503` y **no se guarda nada**, para que el historial no quede con mensajes sin respuesta. Al modelo se le envían como contexto los últimos 50 mensajes del chat.

Configuración local (desde la raíz del backend; los valores van en user-secrets, nunca en git):

```powershell
# Base de datos del servicio (ver la sección de PostgreSQL)
dotnet user-secrets set "ConnectionStrings:DefaultConnection" "Host=localhost;Port=5432;Database=portalempleo_chatbot;Username=postgres;Password=CAMBIAR" --project src/Services/Chatbot/Api/Api.csproj

# Debe ser la MISMA clave que usa el servicio Usuarios para firmar los tokens
dotnet user-secrets set "Jwt:Key" "<la misma clave de Usuarios>" --project src/Services/Chatbot/Api/Api.csproj

# Clave de la API de Anthropic (alternativa: variable de entorno ANTHROPIC_API_KEY)
dotnet user-secrets set "Anthropic:ApiKey" "<tu clave>" --project src/Services/Chatbot/Api/Api.csproj
```

El modelo y el máximo de tokens por respuesta se cambian en `Anthropic:Model` y `Anthropic:MaxTokens` de `appsettings.json`. Las solicitudes de ejemplo están en `src/Services/Chatbot/Api/Api.http`.
