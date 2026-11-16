# Portal Empleo - mocks integrados (PowerShell 5.1+)
# Usuarios 5081; Empleos 5082. Mantener esta terminal abierta.
# /api/empleos conserva modalidad como texto para React.
# /api/empleos/activos devuelve modalidad numerica para EmpleoDto (.NET).
$ErrorActionPreference = 'Stop'

$jobs = foreach ($tipo in @('Usuarios','Empleos')) {
    $puerto = if ($tipo -eq 'Usuarios') { 5081 } else { 5082 }
    Start-Job -Name "Mock$tipo" -ArgumentList $tipo,$puerto -ScriptBlock {
        param($tipo,$puerto)
        $ErrorActionPreference = 'Stop'
        $empleos = @(
            [pscustomobject]@{ id='22222222-2222-2222-2222-222222222222'; titulo='Desarrollador .NET'; descripcion='APIs con C# y SQL Server'; empresa='Tech Sur'; ubicacion='Buenos Aires'; modalidad='Remoto'; tipoContrato='Tiempo completo'; salarioMin=2200000; salarioMax=2600000; requisitos='C#, SQL, Docker'; estado=1; fechaCierre=$null; fechaPublicacion='2026-10-01T09:00:00' },
            [pscustomobject]@{ id='33333333-3333-3333-3333-333333333333'; titulo='Desarrollador React'; descripcion='Aplicaciones web con React'; empresa='WebLab'; ubicacion='Cordoba'; modalidad='Hibrido'; tipoContrato='Tiempo completo'; salarioMin=1900000; salarioMax=2300000; requisitos='React, JavaScript'; estado=1; fechaCierre=$null; fechaPublicacion='2026-10-02T09:00:00' },
            [pscustomobject]@{ id='44444444-4444-4444-4444-444444444444'; titulo='Administrador Linux'; descripcion='Infraestructura Linux y contenedores'; empresa='InfraCloud'; ubicacion='Buenos Aires'; modalidad='Presencial'; tipoContrato='Tiempo completo'; salarioMin=2100000; salarioMax=2500000; requisitos='Linux, Docker, Kubernetes'; estado=1; fechaCierre=$null; fechaPublicacion='2026-10-03T09:00:00' },
            [pscustomobject]@{ id='55555555-5555-5555-5555-555555555555'; titulo='Analista de Datos SQL'; descripcion='Reportes y tableros'; empresa='DataRio'; ubicacion='Rosario'; modalidad='Remoto'; tipoContrato='Part time'; salarioMin=1300000; salarioMax=1700000; requisitos='SQL, Power BI, Python'; estado=1; fechaCierre=$null; fechaPublicacion='2026-10-04T09:00:00' },
            [pscustomobject]@{ id='66666666-6666-6666-6666-666666666666'; titulo='Backend .NET Senior'; descripcion='Microservicios y arquitectura'; empresa='NubeAR'; ubicacion='Buenos Aires'; modalidad='Hibrido'; tipoContrato='Tiempo completo'; salarioMin=3500000; salarioMax=3900000; requisitos='C#, .NET, Azure'; estado=1; fechaCierre=$null; fechaPublicacion='2026-10-05T09:00:00' },
            [pscustomobject]@{ id='77777777-7777-7777-7777-777777777777'; titulo='QA Automation'; descripcion='Automatizacion de pruebas'; empresa='Quality Hub'; ubicacion='Mendoza'; modalidad='Remoto'; tipoContrato='Contrato'; salarioMin=1700000; salarioMax=2100000; requisitos='Selenium, JavaScript'; estado=1; fechaCierre=$null; fechaPublicacion='2026-10-06T09:00:00' },
            [pscustomobject]@{ id='88888888-8888-8888-8888-888888888888'; titulo='Soporte IT'; descripcion='Mesa de ayuda y Active Directory'; empresa='Soporte Uno'; ubicacion='La Plata'; modalidad='Presencial'; tipoContrato='Tiempo completo'; salarioMin=1200000; salarioMax=1600000; requisitos='Windows, Active Directory'; estado=1; fechaCierre=$null; fechaPublicacion='2026-10-07T09:00:00' },
            [pscustomobject]@{ id='99999999-9999-9999-9999-999999999999'; titulo='Frontend Junior'; descripcion='Interfaces con React y CSS'; empresa='PixelAR'; ubicacion='Buenos Aires'; modalidad='Remoto'; tipoContrato='Pasantia'; salarioMin=850000; salarioMax=1250000; requisitos='React, CSS, HTML'; estado=1; fechaCierre=$null; fechaPublicacion='2026-10-08T09:00:00' },
            [pscustomobject]@{ id='aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa'; titulo='DevOps Engineer'; descripcion='CI/CD y cloud'; empresa='DeployNow'; ubicacion='Cordoba'; modalidad='Hibrido'; tipoContrato='Contrato'; salarioMin=3000000; salarioMax=3400000; requisitos='Docker, Kubernetes, Azure'; estado=1; fechaCierre=$null; fechaPublicacion='2026-10-09T09:00:00' },
            [pscustomobject]@{ id='bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb'; titulo='Desarrollador Python'; descripcion='Servicios backend en Python'; empresa='PyLabs'; ubicacion='Rosario'; modalidad='Remoto'; tipoContrato='Part time'; salarioMin=1600000; salarioMax=2000000; requisitos='Python, PostgreSQL'; estado=1; fechaCierre=$null; fechaPublicacion='2026-10-10T09:00:00' }
        )
        function Enviar($response, $data, [int]$status = 200) {
            $json = ConvertTo-Json -InputObject $data -Depth 8 -Compress
            $bytes = [Text.Encoding]::UTF8.GetBytes($json)
            $response.StatusCode = $status
            $response.ContentType = 'application/json; charset=utf-8'
            $response.Headers.Add('Access-Control-Allow-Origin','*')
            $response.Headers.Add('Access-Control-Allow-Headers','Content-Type')
            $response.Headers.Add('Access-Control-Allow-Methods','GET, OPTIONS')
            $response.ContentLength64 = $bytes.Length
            $response.OutputStream.Write($bytes,0,$bytes.Length)
            $response.Close()
        }
        $listener = New-Object System.Net.HttpListener
        $listener.Prefixes.Add("http://localhost:$puerto/")
        $listener.Start()
        Write-Output "$tipo iniciado en http://localhost:$puerto"
        try {
            while ($listener.IsListening) {
                $ctx = $listener.GetContext()
                $path = $ctx.Request.Url.AbsolutePath.TrimEnd('/')
                if ($ctx.Request.HttpMethod -eq 'OPTIONS') { Enviar $ctx.Response @{}; continue }
                if ($ctx.Request.HttpMethod -ne 'GET') { Enviar $ctx.Response @{error='Metodo no permitido'} 405; continue }
                if ($tipo -eq 'Usuarios') {
                    if ($path -match '^/api/perfiles/([0-9a-fA-F-]{36})$') {
                        Enviar $ctx.Response @{usuarioId=$Matches[1];habilidadesJson='["C#","SQL","React"]';resumen='Desarrollador de software'}
                    } else { Enviar $ctx.Response @{error='No encontrado'} 404 }
                    continue
                }
                if ($path -eq '/api/empleos/activos') {
                    # EmpleoDto espera: Id Guid, Titulo string, Requisitos string?,
                    # Ubicacion string?, Modalidad int, Estado int, FechaCierre DateTime?.
                    # El frontend de busqueda sigue recibiendo modalidades como texto.
                    # Codigos de modalidad de prueba: Presencial=0, Remoto=1, Hibrido=2.
                    # Ajustar si el enum real del proyecto utiliza otros valores.
                    $activos = @(
                        foreach ($empleo in $empleos) {
                            if ($empleo.estado -ne 1) { continue }
                            $modalidadNumero = switch ($empleo.modalidad) {
                                'Presencial' { 0 }
                                'Remoto'     { 1 }
                                'Hibrido'    { 2 }
                                'Híbrido'    { 2 }
                                default     { 0 }
                            }
                            [pscustomobject]@{
                                id          = $empleo.id
                                titulo      = $empleo.titulo
                                requisitos  = $empleo.requisitos
                                ubicacion   = $empleo.ubicacion
                                modalidad   = [int]$modalidadNumero
                                estado      = [int]$empleo.estado
                                fechaCierre = $empleo.fechaCierre
                            }
                        }
                    )
                    Enviar $ctx.Response $activos
                    continue
                }
                if ($path -eq '/api/empleos') {
                    $result = @($empleos | Where-Object { $_.estado -eq 1 })
                    $q = $ctx.Request.QueryString
                    foreach ($campo in @('ubicacion','modalidad')) {
                        $valor = $q[$campo]
                        if (-not [string]::IsNullOrWhiteSpace($valor)) {
                            $result = @($result | Where-Object { $_.$campo -like "*$valor*" })
                        }
                    }
                    $busqueda = $q['busqueda']
                    if (-not [string]::IsNullOrWhiteSpace($busqueda)) {
                        $result = @($result | Where-Object { $_.titulo -like "*$busqueda*" -or $_.descripcion -like "*$busqueda*" -or $_.requisitos -like "*$busqueda*" })
                    }
                    $min = 0L
                    if ([long]::TryParse($q['salarioMin'],[ref]$min)) {
                        $result = @($result | Where-Object { $_.salarioMin -ge $min })
                    }
                    switch ($q['orden']) {
                        'salario_asc'  { $result = @($result | Sort-Object salarioMin) }
                        'salario_desc' { $result = @($result | Sort-Object salarioMin -Descending) }
                        'fecha_asc'   { $result = @($result | Sort-Object fechaPublicacion) }
                        default       { $result = @($result | Sort-Object fechaPublicacion -Descending) }
                    }
                    Enviar $ctx.Response @($result)
                    continue
                }
                Enviar $ctx.Response @{error='No encontrado'} 404
            }
        } finally { $listener.Stop(); $listener.Close() }
    }
}
Start-Sleep -Milliseconds 500
foreach ($job in $jobs) {
    Write-Host "$($job.Name): $($job.State)" -ForegroundColor Cyan
}
Write-Host 'Mocks iniciados. Para detenerlos, presionar Ctrl+C y ejecutar: Get-Job -Name Mock* | Stop-Job' -ForegroundColor Green
$jobs | Receive-Job -Wait
