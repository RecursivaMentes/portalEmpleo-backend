# PortalEmpleo — Backend

Backend de **RecursivaMentes**, desarrollado con **.NET 10, C#, ASP.NET Core, Entity Framework Core y PostgreSQL**. Implementa servicios de usuarios y CV, ofertas y postulaciones, asistente conversacional y recomendaciones laborales.

> **Estado de validación:** compilación correcta y **129/129 pruebas unitarias aprobadas** en la última verificación local (15/11/2026). Las pruebas integrales de generación, aprobación, cancelación y envío mediante IA siguen pendientes de completarse con un proveedor de IA operativo. La compilación y los tests no sustituyen esas pruebas de integración.

## 1. Arquitectura

El repositorio contiene cuatro microservicios. Cada uno administra su propio modelo de datos y base PostgreSQL; las referencias entre servicios se transmiten como identificadores y **no se crean claves foráneas entre bases diferentes**.

| Microservicio | Puerto HTTP local | Responsabilidad principal |
|---|---:|---|
| **Usuarios** | `5078` | Cuentas, autenticación JWT, perfil laboral, CV, archivos, preferencias y configuración pública de postulaciones. |
| **EmpleosYpostulaciones** | `5077` | Ofertas, pendientes, postulaciones, preparaciones, estados, historial, configuración operativa y procesamiento autónomo. |
| **Chatbot** | `5079` | Conversaciones, herramientas de perfil, generación y verificación de propuestas de postulación mediante IA. |
| **Recomendaciones** | `5080` | Análisis de compatibilidad y recomendaciones a partir de perfiles y ofertas. |

Cada servicio está organizado en proyectos de cuatro capas:

```text
src/
└── Services/
    ├── Usuarios/
    │   ├── Api/
    │   ├── Application/
    │   ├── Domain/
    │   └── Infrastructure/
    ├── EmpleosYpostulaciones/
    │   ├── Api/
    │   ├── Application/
    │   ├── Domain/
    │   └── Infrastructure/
    ├── Chatbot/
    │   ├── Api/
    │   ├── Application/
    │   ├── Domain/
    │   └── Infrastructure/
    └── Recomendaciones/
        ├── Api/
        ├── Application/
        ├── Domain/
        └── Infrastructure/

tests/
├── Usuarios.Tests/
├── EmpleosYpostulaciones.Tests/
├── Chatbot.Tests/
└── Recomendaciones.Tests/
```

- **Domain:** entidades, enumeraciones y reglas de negocio.
- **Application:** casos de uso, validaciones y contratos de integración.
- **Infrastructure:** EF Core, repositorios, almacenamiento y clientes HTTP.
- **Api:** controladores/endpoints, autenticación, autorización y composición de dependencias.

## 2. Requisitos

- [.NET SDK 10](https://dotnet.microsoft.com/download/dotnet/10.0).
- [PostgreSQL](https://www.postgresql.org/download/), con acceso a cuatro bases independientes.
- Herramienta `dotnet-ef` para aplicar migraciones: `dotnet tool install --global dotnet-ef` (solo si falta).
- Para la generación real con IA: **Anthropic** con credenciales válidas o **Ollama** instalado y configurado localmente.
- PowerShell para los ejemplos de esta guía; los servicios también pueden ejecutarse con otros entornos compatibles con .NET.

Los comandos siguientes se ejecutan **desde la raíz del repositorio backend**.

## 3. Configuración inicial

### 3.1. Bases de datos

Crear cuatro bases independientes. Los nombres son ejemplos del entorno de desarrollo:

| Servicio | Base local de ejemplo |
|---|---|
| Usuarios | `portalempleo_usuarios` |
| EmpleosYpostulaciones | `portalempleo_postulaciones` |
| Chatbot | `portalempleo_chatbot` |
| Recomendaciones | `portalempleo_recomendaciones` |

Configurar `ConnectionStrings:DefaultConnection` **por separado** en los User Secrets del proyecto `Api` de cada servicio. Ejemplo para Usuarios:

```powershell
dotnet user-secrets set "ConnectionStrings:DefaultConnection" "Host=localhost;Port=5432;Database=portalempleo_usuarios;Username=postgres;Password=REEMPLAZAR" --project src/Services/Usuarios/Api/Api.csproj
```

Repetir el comando con el proyecto y la base correspondientes a los otros tres servicios. No agregar contraseñas ni cadenas de conexión con credenciales reales al repositorio.

### 3.2. JWT

Usuarios emite los JWT y los cuatro servicios utilizan **la misma clave de firma** (`Jwt:Key`), con el mismo `Jwt:Issuer` y `Jwt:Audience`. Los valores de desarrollo establecidos en Usuarios son `Infrastructure` y `API`, respectivamente. **EmpleosYpostulaciones y Recomendaciones no los traen definidos en sus `appsettings.json`**, por lo que hay que configurarlos antes de iniciarlos.

En una **instalación nueva**, generar una sola clave segura y configurarla en las cuatro API:

```powershell
$claveJwt = [Convert]::ToBase64String([System.Security.Cryptography.RandomNumberGenerator]::GetBytes(32))
$servicios = @('Usuarios', 'EmpleosYpostulaciones', 'Chatbot', 'Recomendaciones')
foreach ($servicio in $servicios) {
    $proyecto = "src/Services/$servicio/Api/Api.csproj"
    dotnet user-secrets set "Jwt:Key" $claveJwt --project $proyecto
    if ($LASTEXITCODE -ne 0) { throw "No se pudo configurar el JWT de $servicio" }
    dotnet user-secrets set "Jwt:Issuer" "Infrastructure" --project $proyecto
    if ($LASTEXITCODE -ne 0) { throw "No se pudo configurar Jwt:Issuer en $servicio" }
    dotnet user-secrets set "Jwt:Audience" "API" --project $proyecto
    if ($LASTEXITCODE -ne 0) { throw "No se pudo configurar Jwt:Audience en $servicio" }
}
Remove-Variable claveJwt
```

**No ejecutar la generación de una nueva clave en un entorno ya configurado sin coordinarlo:** invalidaría los tokens existentes y podría dejar los servicios con claves diferentes. En ese caso, conservar la clave compartida vigente y comprobar que emisor y audiencia coincidan. La expiración de los tokens emitidos por Usuarios está configurada en 60 minutos.

### 3.3. Autenticación interna entre microservicios

Las rutas `/internal/...` usan los encabezados `X-Internal-Service` y `X-Internal-Key`, **no el JWT de una persona**. El nombre del servicio llamador y su clave deben coincidir con los valores configurados en el receptor. La ruta `/api/postulaciones/control/...` es distinta: utiliza JWT validado y sirve para coordinar cambios con Usuarios.

| Clave de configuración | Proyectos que deben compartirla | Uso |
|---|---|---|
| `InternalServices:EmpleosYpostulaciones:Key` | EmpleosYpostulaciones, Usuarios, Chatbot, Recomendaciones | Consultas internas originadas por Postulaciones. |
| `InternalServices:Chatbot:Key` | Chatbot, Usuarios, EmpleosYpostulaciones | Lectura interna de perfil, CV y oferta para generar propuestas. |
| `InternalServices:Recomendaciones:Key` | Recomendaciones, Usuarios | Consulta interna de perfil para analizar compatibilidad. |

Cada servicio llamador utiliza **una clave distinta**, codificada en base64 y generada con al menos 32 bytes aleatorios. En un entorno nuevo se pueden crear y distribuir automáticamente así:

```powershell
$receptores = @{
    EmpleosYpostulaciones = @('EmpleosYpostulaciones', 'Usuarios', 'Chatbot', 'Recomendaciones')
    Chatbot = @('Chatbot', 'Usuarios', 'EmpleosYpostulaciones')
    Recomendaciones = @('Recomendaciones', 'Usuarios')
}
foreach ($llamador in $receptores.Keys) {
    $claveInterna = [Convert]::ToBase64String([System.Security.Cryptography.RandomNumberGenerator]::GetBytes(32))
    foreach ($servicio in $receptores[$llamador]) {
        dotnet user-secrets set "InternalServices:${llamador}:Key" $claveInterna `
            --project "src/Services/$servicio/Api/Api.csproj"
        if ($LASTEXITCODE -ne 0) { throw "No se pudo configurar la clave de $llamador en $servicio" }
    }
}
Remove-Variable claveInterna
```

Al igual que con JWT, **no volver a generar estas claves sobre una instalación en uso** sin actualizar de forma coordinada a todos los servicios que las comparten. No publicarlas en Git, capturas ni documentación.

### 3.4. Direcciones locales entre servicios

En `appsettings.Development.json` están configuradas las direcciones HTTP utilizadas en desarrollo:

| Origen | Configuración | Destino |
|---|---|---|
| Usuarios | `Services:EmpleosYpostulaciones:BaseUrl` | `http://localhost:5077` |
| EmpleosYpostulaciones | `Services:Usuarios:BaseUrl` | `http://localhost:5078` |
| EmpleosYpostulaciones | `Services:Chatbot:BaseUrl` | `http://localhost:5079` |
| EmpleosYpostulaciones | `Services:Recomendaciones:BaseUrl` | `http://localhost:5080` |
| Chatbot | `Servicios:Usuarios:BaseUrl` | `http://localhost:5078` |
| Chatbot | `Servicios:Empleos:BaseUrl` | `http://localhost:5077` |
| Recomendaciones | `Microservices:UsuariosUrl` | `http://localhost:5078` |
| Recomendaciones | `Microservices:EmpleosUrl` | `http://localhost:5077` |

Las URLs y los puertos corresponden al desarrollo local; en otros entornos deben reemplazarse por direcciones accesibles desde cada servicio.

### 3.5. Migraciones

Las migraciones son independientes. **Revisar la base de destino y disponer de una copia de seguridad antes de aplicarlas sobre datos importantes.** Desde la raíz del repositorio:

```powershell
$servicios = @('Usuarios', 'EmpleosYpostulaciones', 'Chatbot', 'Recomendaciones')
foreach ($servicio in $servicios) {
    dotnet ef database update `
        --project "src/Services/$servicio/Infrastructure/Infrastructure.csproj" `
        --startup-project "src/Services/$servicio/Api/Api.csproj"
    if ($LASTEXITCODE -ne 0) { throw "Fallo la migración de $servicio" }
}
```

Las migraciones de EmpleosYpostulaciones incluyen pendientes, preparaciones, seguimiento, copia histórica de CV, controles de concurrencia y la migración `20261114180000_ConfiguracionYReservasCambio`, que incorpora `configuraciones_postulacion` y `reservas_cambio_usuario`. Usuarios incorpora operaciones idempotentes de CV mediante IA.

Las migraciones **no se aplican automáticamente** al ejecutar las API.

## 4. Ejecutar el backend

Abrir **cuatro terminales** y ejecutar un comando en cada una:

**Usuarios — puerto 5078**

```powershell
dotnet run --project src/Services/Usuarios/Api/Api.csproj --launch-profile http
```

**EmpleosYpostulaciones — puerto 5077**

```powershell
dotnet run --project src/Services/EmpleosYpostulaciones/Api/Api.csproj --launch-profile http
```

**Chatbot — puerto 5079**

```powershell
dotnet run --project src/Services/Chatbot/Api/Api.csproj --launch-profile http
```

**Recomendaciones — puerto 5080**

```powershell
dotnet run --project src/Services/Recomendaciones/Api/Api.csproj --launch-profile http
```

En `Development` se puede comprobar el arranque mediante:

```text
http://localhost:5077/openapi/v1.json
http://localhost:5078/openapi/v1.json
http://localhost:5079/openapi/v1.json
http://localhost:5080/openapi/v1.json
```

Una respuesta HTTP 200 en OpenAPI **verifica el arranque**, no garantiza la disponibilidad de la base, las integraciones ni el proveedor de IA.

### 4.1. CORS y frontend

El origen de desarrollo habilitado es `http://localhost:5173`. Si el frontend utiliza otro origen, se debe revisar la política CORS de las API afectadas. El frontend usa JWT Bearer para rutas autenticadas; **nunca debe conocer ni enviar claves de comunicación interna**.

## 5. Funcionalidades principales

### 5.1. Usuarios y CV

- Registro, inicio de sesión y consulta/actualización del perfil de cuenta.
- Perfil laboral, validación de completitud y preferencias.
- Gestión de múltiples CV con un principal y control de versión.
- Carga de PDF y DOCX de hasta 5 MiB, extracción de texto y descarga de documentos.
- Protección de acceso: cada persona consulta y modifica únicamente sus propios CV.
- Copia histórica del CV al confirmar una postulación; los cambios posteriores al CV original no deben modificar lo enviado.

Los archivos originales de CV se almacenan en la ruta configurada en `Almacenamiento:RutaCvs` (actualmente `App_Data/cvs` en Usuarios). **Ese directorio no se versiona.** En despliegues con contenedores o discos efímeros, configurar un volumen persistente o un almacenamiento compatible con la estrategia del proyecto.

### 5.2. Ofertas y recomendaciones

- Consulta y filtrado de ofertas con `GET /api/empleos`.
- Análisis de compatibilidad entre un perfil laboral y una oferta.
- Ordenamiento de pendientes por criterios de prioridad, compatibilidad, fecha de cierre, antigüedad o incorporación reciente.
- Comunicación entre servicios mediante HTTP, con autenticación JWT o interna según el contrato de la ruta.

### 5.3. Postulaciones inteligentes

La configuración pública admite los modos siguientes:

| Modo | Comportamiento esperado |
|---|---|
| `Desactivada` | No ejecuta procesamiento autónomo de postulaciones. |
| `Asistida` | Prepara una propuesta y requiere la confirmación correspondiente del usuario antes del envío. |
| `Automatica` | Permite el procesamiento y envío automático, sujeto a los límites, validaciones y autorizaciones implementadas. |

Valores de configuración:

| Campo | Valores/reglas |
|---|---|
| `modo` | `Desactivada`, `Asistida`, `Automatica` |
| `compatibilidadMinima` | Entero de 1 a 100 |
| `limiteDiario` | Entero de 1 a 10 |
| `ordenPendientes` | `Prioridad`, `Compatibilidad`, `FechaCierre`, `Antiguos`, `Recientes` |
| `zonaHoraria` | Identificador válido de zona horaria, por ejemplo `America/Argentina/Buenos_Aires` |
| `preferencias` | Puestos, ubicaciones, modalidades, contratos, expectativa salarial y exclusiones |

La configuración inicial es `Desactivada`, compatibilidad mínima `60` y límite diario `5`. **La configuración operativa** (modo, límites, orden y zona horaria) se guarda en **EmpleosYpostulaciones**; las **preferencias laborales** se conservan en **Usuarios**. La API de Usuarios coordina el contrato público de lectura y escritura.

El flujo funcional contempla agregar ofertas pendientes, solicitar preparación con IA, revisar o descartar cambios, aprobar propuestas de CV, enviar, cancelar y consultar historial. Las preparaciones tienen estados como `Preparando`, `EnModificacion`, `PendienteAprobacionCv`, `AplicandoCv`, `PendienteAprobacion`, `Enviando`, `CancelacionSolicitada`, `Enviada`, `Cancelada` y `Fallida`.

**Concurrencia y seguridad:** el sistema aplica bloqueos, validaciones de pertenencia, versiones de edición, controles contra duplicados y verificaciones de estado. No debe confundirse una propuesta preparada con una postulación enviada.

## 6. API HTTP: rutas relevantes

Salvo las excepciones públicas indicadas, los endpoints requieren `Authorization: Bearer <token>` emitido por Usuarios. Las rutas `/internal/...` están destinadas a comunicación entre servicios y **no son endpoints del frontend**. Las respuestas y contratos HTTP reales pueden consultarse en `/openapi/v1.json` de cada API.

### 6.1. Usuarios — `http://localhost:5078`

| Método | Ruta | Uso |
|---|---|---|
| `POST` | `/api/auth/register` | Registrar usuario |
| `POST` | `/api/auth/login` | Obtener token JWT |
| `GET` | `/api/auth/profile` | Consultar cuenta autenticada |
| `PUT` | `/api/auth/profile` | Actualizar cuenta autenticada |
| `GET` / `PUT` | `/api/perfil-laboral/me` | Consultar o modificar perfil laboral |
| `GET` | `/api/perfil-laboral/me/validacion` | Comprobar completitud del perfil |
| `GET` / `POST` | `/api/cvs` | Listar o crear CV |
| `GET` / `PUT` / `DELETE` | `/api/cvs/{cvId}` | Consultar, modificar o eliminar un CV propio |
| `GET` | `/api/cvs/{cvId}/validacion` | Comprobar pertenencia del CV al usuario autenticado (`204` / `404`) |
| `POST` | `/api/cvs/archivo` | Crear un CV subiendo un PDF/DOCX (`multipart/form-data`) |
| `PUT` | `/api/cvs/{cvId}/archivo` | Incorporar o reemplazar el archivo de un CV |
| `GET` | `/api/cvs/{cvId}/archivo` | Descargar el archivo original |
| `DELETE` | `/api/cvs/{cvId}/archivo` | Quitar el archivo conservando el CV y su texto |
| `POST` | `/api/cvs/{cvId}/extraer-texto` | Repetir la extracción de texto desde el archivo guardado |
| `GET` | `/api/cvs/{cvId}/descargar` | Descargar PDF generado a partir del texto del CV |
| `GET` / `PUT` | `/api/postulaciones/configuracion/me` | Consultar o modificar configuración de postulaciones |

Solo `POST /api/auth/register` y `POST /api/auth/login` son públicos dentro de esta tabla; las demás rutas requieren JWT. En `PUT /api/cvs/{cvId}` se envía la versión actual mediante el campo `version` del cuerpo JSON. En `DELETE /api/cvs/{cvId}`, `PUT/DELETE /api/cvs/{cvId}/archivo` y `POST /api/cvs/{cvId}/extraer-texto`, se envía mediante el encabezado `If-Match`, por ejemplo `If-Match: "3"`. Los archivos aceptados son PDF/DOCX de hasta **5 MiB**.

### 6.2. EmpleosYpostulaciones — `http://localhost:5077`

| Método | Ruta | Uso |
|---|---|---|
| `GET` | `/api/empleos` | Buscar/listar ofertas (pública) |
| `POST` | `/api/postulaciones/pendientes` | Agregar una oferta pendiente |
| `GET` | `/api/postulaciones/pendientes/me` | Listar pendientes del usuario |
| `GET` / `DELETE` | `/api/postulaciones/pendientes/{empleoId}` | Consultar o quitar una pendiente |
| `POST` | `/api/postulaciones/preparaciones/solicitar-con-ia` | Solicitar preparación de postulación |
| `GET` | `/api/postulaciones/preparaciones/me` | Historial de preparaciones |
| `GET` | `/api/postulaciones/preparaciones/activa/me` | Disponibilidad y preparación activa |
| `GET` | `/api/postulaciones/preparaciones/{id}` | Detalle de preparación |
| `GET` | `/api/postulaciones/preparaciones/{id}/comparacion-cv-ia` | Comparación de propuesta de CV |
| `POST` | `/api/postulaciones/preparaciones/{id}/aprobar-cv-ia` | Aprobar propuesta de CV |
| `POST` | `/api/postulaciones/preparaciones/{id}/solicitar-cambios-ia` | Solicitar modificación mediante IA |
| `POST` | `/api/postulaciones/preparaciones/{id}/proponer-cambios` | Previsualizar cambios manuales |
| `POST` | `/api/postulaciones/preparaciones/{id}/aceptar-cambios` | Aceptar cambios propuestos |
| `POST` | `/api/postulaciones/preparaciones/{id}/descartar-cambios` | Descartar cambios propuestos |
| `POST` | `/api/postulaciones/preparaciones/{id}/enviar` | Confirmar envío de una preparación |
| `POST` | `/api/postulaciones/preparaciones/{id}/cancelar` | Solicitar/cerrar cancelación según estado |
| `POST` | `/api/postulaciones/` | Crear postulación directamente (requiere `usuarioId`, `empleoId` y `cvId` del contrato) |
| `GET` | `/api/postulaciones/me` | Historial de postulaciones |
| `GET` | `/api/postulaciones/{id}` | Detalle de postulación |
| `GET` | `/api/postulaciones/empleos/{empleoId}` | Seguimiento por oferta |
| `POST` | `/api/postulaciones/{id}/estado` | Actualizar estado permitido |
| `GET` | `/api/postulaciones/{id}/cv-enviado` | Descargar copia histórica del CV enviado |

**Cuerpos JSON habituales** (campos de ejemplo, sin credenciales):

```json
{"empleoId":"00000000-0000-0000-0000-000000000001"}
```

Se utiliza para `POST /api/postulaciones/pendientes` y `POST /api/postulaciones/preparaciones/solicitar-con-ia`, reemplazando el GUID por un empleo existente.

```json
{"versionEdicion":1}
```

Se utiliza para confirmar cambios, aprobar CV o enviar una preparación. **Tomar siempre `versionEdicion` de la respuesta actual**; no reutilizar versiones obsoletas.

```json
{"versionEdicion":1,"instrucciones":"Destacá experiencia en C# sin agregar datos no comprobables."}
```

Se utiliza para `POST /api/postulaciones/preparaciones/{id}/solicitar-cambios-ia`.

Para la previsualización de cambios manuales (`POST /api/postulaciones/preparaciones/{id}/proponer-cambios`) se envía al menos uno de los campos `cvId` y `textoPresentacion`; el texto de presentación admite como máximo 4000 caracteres. Esta ruta **no confirma ni envía** cambios: primero se llama a `aceptar-cambios` o `descartar-cambios` usando la versión actualizada.

```json
{"cvId":null,"textoPresentacion":"Presentación revisada, con información verificable."}
```

**Diferencia importante:** `aprobar-cv-ia` confirma la aplicación de cambios al CV, pero en modo `Asistida` **no envía la postulación**; el envío requiere la operación `enviar` y la aprobación expresa del usuario. La respuesta a `cancelar` puede ser `200` (cancelación completada) o `202` (cancelación solicitada, todavía en proceso).

También existen rutas de control de configuración en `/api/postulaciones/control/...`, utilizadas en la coordinación con Usuarios; para el frontend debe preferirse el contrato público `/api/postulaciones/configuracion/me` del servicio Usuarios.

### 6.3. Chatbot — `http://localhost:5079`

| Método | Ruta | Uso |
|---|---|---|
| `POST` | `/api/chats` | Crear conversación |
| `GET` | `/api/chats` | Listar conversaciones |
| `GET` | `/api/chats/{chatId}/mensajes` | Obtener historial |
| `POST` | `/api/chats/{chatId}/mensajes` | Enviar mensaje |
| `POST` | `/api/analisis-cv` | Analizar un CV y proponer datos para el perfil; no guarda cambios |
| `POST` | `/api/analisis-cv/aplicar` | Incorporar al perfil los datos de la propuesta confirmada |
| `POST` | `/api/generacion-cv` | Generar un CV con IA desde el perfil y guardarlo en Usuarios |

Estas rutas públicas de Chatbot requieren JWT del usuario. El análisis de CV tiene dos pasos diferenciados: primero se muestra la propuesta y solo después de confirmarla se aplica al perfil. El endpoint interno `POST /internal/chatbot/postulaciones/preparar` es usado por EmpleosYpostulaciones y requiere una clave interna; **no debe llamarse desde el navegador**. El asistente utiliza herramientas para consultar/completar datos del perfil cuando corresponda y para generar propuestas de postulación sin inventar antecedentes laborales.

### 6.4. Recomendaciones — `http://localhost:5080`

| Método | Ruta | Uso |
|---|---|---|
| `POST` | `/api/recomendaciones/analizar` | Analizar una solicitud de compatibilidad |
| `GET` | `/api/recomendaciones/analizar/{usuarioId}` | Analizar ofertas para el usuario autenticado |
| `GET` | `/api/recomendaciones/usuario/{usuarioId}` | Consultar recomendaciones del usuario |
| `GET` | `/api/recomendaciones/compatibilidad/{empleoId}` | Analizar compatibilidad con una oferta |

El endpoint `POST /api/recomendaciones/analizar` es público en la implementación actual. Los tres `GET` anteriores requieren JWT; en las rutas con `{usuarioId}`, el identificador debe coincidir con el del token. Hay rutas internas adicionales utilizadas por EmpleosYpostulaciones para procesamiento y ordenamiento.

## 7. Configuración de inteligencia artificial

### 7.1. Habilitación independiente del motor autónomo

En EmpleosYpostulaciones se utilizan las siguientes opciones:

| Opción | Valor habitual para pruebas manuales | Significado |
|---|---|---|
| `PostulacionesIA:IntegracionChatbotHabilitada` | `true` | Habilita las solicitudes de preparación por Chatbot |
| `PostulacionesIA:ProcesamientoHabilitado` | `false` | Mantiene desactivado el ciclo de envío autónomo |
| `PostulacionesIA:IntervaloMinutos` | `2` (predeterminado) | Intervalo del planificador, limitado a 1–60 minutos |

Para probar **solo la preparación con IA** en una terminal nueva, sin habilitar envíos autónomos:

```powershell
$env:PostulacionesIA__IntegracionChatbotHabilitada = "true"
$env:PostulacionesIA__ProcesamientoHabilitado = "false"
dotnet run --project src/Services/EmpleosYpostulaciones/Api/Api.csproj --launch-profile http
```

> **Precaución:** habilitar `PostulacionesIA:ProcesamientoHabilitado=true` puede iniciar procesamiento automático para usuarios configurados en modo `Automatica`. Usar solo en un entorno de pruebas controlado, con datos y cuentas de prueba.

### 7.2. Claude (Anthropic)

Chatbot utiliza `Asistente:Proveedor = Claude` por defecto. Requiere una clave válida, configurada mediante `Anthropic:ApiKey` (User Secrets) o `ANTHROPIC_API_KEY` (variable de entorno):

```powershell
dotnet user-secrets set "Anthropic:ApiKey" "API_KEY_VALIDA" --project src/Services/Chatbot/Api/Api.csproj
```

Modelo y límite de tokens se configuran con `Anthropic:Model` y `Anthropic:MaxTokens`. Una API Key puede pertenecer a una cuenta de equipo siempre que esté vigente y su utilización esté autorizada. Una clave rechazada por Anthropic puede impedir las preparaciones, aunque Chatbot responda HTTP 200 en OpenAPI.

### 7.3. Ollama (alternativa local)

El proyecto soporta Ollama para desarrollo sin clave de Anthropic. Instalar [Ollama](https://ollama.com/), descargar el modelo configurado y cambiar el proveedor:

```powershell
ollama pull llama3.2
dotnet user-secrets set "Asistente:Proveedor" "Ollama" --project src/Services/Chatbot/Api/Api.csproj
```

Verificar que Ollama esté activo en la dirección indicada por `Ollama:BaseUrl` (predeterminado: `http://localhost:11434`). Para volver al proveedor predeterminado:

```powershell
dotnet user-secrets remove "Asistente:Proveedor" --project src/Services/Chatbot/Api/Api.csproj
```

La disponibilidad del proveedor no garantiza que un modelo concreto genere JSON válido o propuestas fundamentadas: esos contratos deben probarse con el modelo configurado.

## 8. Manejo de errores

EmpleosYpostulaciones utiliza respuestas de error con un mensaje para el usuario y un código estable. Ejemplo de formato:

```json
{
  "mensaje": "Los datos enviados no tienen un formato válido.",
  "codigo": 400,
  "codigoError": "DATOS_INVALIDOS",
  "seguimientoId": "identificador-de-solicitud"
}
```

Los textos pueden variar según el caso; usar **`codigoError` cuando esté disponible y el estado HTTP** para decidir el comportamiento del frontend, no comparar literalmente los mensajes. Otros servicios pueden usar `message` en vez de `mensaje` o no incluir `codigoError`, por lo que el cliente debe admitir ambos formatos. Los errores no deben mostrar información técnica sensible al usuario.

| HTTP | Interpretación habitual |
|---:|---|
| `400` | Datos incorrectos, campos faltantes o JSON inválido |
| `401` | Falta autenticación o la sesión no es válida |
| `403` | El usuario autenticado no tiene autorización |
| `404` | Recurso inexistente o inaccesible para ese usuario |
| `409` | Conflicto de estado, duplicado, versión desactualizada u operación no permitida |
| `422` | Datos insuficientes o no validables en algunos contratos de preparación |
| `502` / `503` | Respuesta inválida de dependencia o servicio/proveedor de IA no disponible |
| `500` | Error interno inesperado |

Un `401` del proveedor externo **no equivale a una sesión inválida del usuario final**: el frontend debe mostrar la indisponibilidad de IA sin exponer claves ni trazas. Los detalles técnicos deben quedar en registros internos, asociados al identificador de seguimiento cuando esté disponible.

## 9. Compilación y pruebas

### 9.1. Compilar

```powershell
dotnet build PortalEmpleo.slnx
```

### 9.2. Ejecutar todos los proyectos de pruebas

El comando `dotnet test PortalEmpleo.slnx` puede compilar la solución sin mostrar un resumen de los cuatro proyectos de tests. Para ejecutarlos expresamente:

```powershell
dotnet test tests/Usuarios.Tests/Usuarios.Tests.csproj
dotnet test tests/EmpleosYpostulaciones.Tests/EmpleosYpostulaciones.Tests.csproj
dotnet test tests/Chatbot.Tests/Chatbot.Tests.csproj
dotnet test tests/Recomendaciones.Tests/Recomendaciones.Tests.csproj
```

También pueden ejecutarse todos desde PowerShell:

```powershell
Get-ChildItem -Path tests -Recurse -Filter '*Tests.csproj' | ForEach-Object {
    dotnet test $_.FullName
    if ($LASTEXITCODE -ne 0) { throw "Fallaron las pruebas de $($_.Name)" }
}
```

Últimos resultados locales registrados (15/11/2026):

| Proyecto | Pruebas aprobadas | Fallidas |
|---|---:|---:|
| Usuarios.Tests | 27 | 0 |
| EmpleosYpostulaciones.Tests | 75 | 0 |
| Chatbot.Tests | 5 | 0 |
| Recomendaciones.Tests | 22 | 0 |
| **Total** | **129** | **0** |

### 9.3. Verificaciones funcionales realizadas

Se comprobó localmente:

- Inicio de los cuatro servicios y documentación OpenAPI.
- Autenticación JWT y rechazo de accesos sin sesión.
- Lectura y cambio de configuración, con aislamiento entre usuarios.
- Gestión de ofertas pendientes y rechazo de duplicados.
- Acceso aislado a preparaciones y CV por usuario.
- Solicitudes simultáneas de un mismo pendiente (una creación y un conflicto).
- Consulta de historial, recursos inexistentes y estados no cancelables.
- Manejo de JSON inválido con HTTP 400.
- Persistencia y descarga de archivo de CV tras reinicio del servicio Usuarios.

**Pendiente de prueba integral:** preparación real exitosa por IA, revisión/aprobación de propuestas, cancelación durante ejecución, concurrencia cancelación/envío, límites y envío autónomo completo. La última verificación del proveedor Anthropic devolvió HTTP 401 por credenciales rechazadas; el backend reflejó indisponibilidad de la dependencia (HTTP 503). No se deben marcar estos flujos como aprobados hasta probarlos.

## 10. Seguridad y desarrollo en equipo

- **No versionar** JWT privados, contraseñas, claves Anthropic, claves internas, archivos CV, datos personales ni bases locales.
- Mantener los secretos en User Secrets durante desarrollo y usar un gestor de secretos en los demás entornos.
- Mantener excluidos `bin/`, `obj/`, `.vs/` y `App_Data/` del control de versiones.
- **No ignorar `/tests/`:** las pruebas automatizadas son parte del proyecto y deben poder agregarse normalmente. Si se viene de una versión de `.gitignore` que excluía esa carpeta, quitar la regla `/tests/`.
- No habilitar el motor autónomo en un entorno compartido sin autorización y pruebas de integración.
- Aplicar migraciones en cada base de datos antes de desplegar una versión que dependa de ellas.
- Revisar los contratos OpenAPI de cada API cuando se incorporen endpoints nuevos o cambien sus cuerpos/respuestas.

## 11. Estado de entrega

El código de la rama de desarrollo puede revisarse mediante Pull Request. Las pruebas unitarias y funcionales señaladas anteriormente no reemplazan las pruebas integrales pendientes de IA. La aprobación del PR y su eventual integración a `main` deben considerar explícitamente esa limitación.
