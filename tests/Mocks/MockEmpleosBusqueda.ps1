# Mock de empleos para Portal Empleo - PowerShell 5.1+
# Ejecutar: powershell -ExecutionPolicy Bypass -File .\MockEmpleosBusqueda.ps1
param([int]$Puerto = 5082)

$ErrorActionPreference = 'Stop'

$empleos = @(
    [pscustomobject]@{ id=1; titulo='Desarrollador .NET'; descripcion='APIs con C# y SQL Server'; empresa='Tech Sur'; ubicacion='Buenos Aires'; modalidad='Remoto'; tipoContrato='Tiempo completo'; salario=2200000; tecnologias=@('C#','SQL','Docker'); requisitos='C#, SQL, Docker'; estado=1 },
    [pscustomobject]@{ id=2; titulo='Desarrollador React'; descripcion='Aplicaciones web con React'; empresa='WebLab'; ubicacion='Cordoba'; modalidad='Hibrido'; tipoContrato='Tiempo completo'; salario=1900000; tecnologias=@('React','JavaScript'); requisitos='React, JavaScript'; estado=1 },
    [pscustomobject]@{ id=3; titulo='Administrador Linux'; descripcion='Infraestructura Linux y contenedores'; empresa='InfraCloud'; ubicacion='Buenos Aires'; modalidad='Presencial'; tipoContrato='Tiempo completo'; salario=2100000; tecnologias=@('Linux','Docker','Kubernetes'); requisitos='Linux, Docker, Kubernetes'; estado=1 },
    [pscustomobject]@{ id=4; titulo='Analista de Datos SQL'; descripcion='Reportes y tableros de datos'; empresa='DataRio'; ubicacion='Rosario'; modalidad='Remoto'; tipoContrato='Part time'; salario=1300000; tecnologias=@('SQL','Power BI','Python'); requisitos='SQL, Power BI, Python'; estado=1 },
    [pscustomobject]@{ id=5; titulo='Backend .NET Senior'; descripcion='Microservicios y arquitectura'; empresa='NubeAR'; ubicacion='Buenos Aires'; modalidad='Hibrido'; tipoContrato='Tiempo completo'; salario=3500000; tecnologias=@('C#','.NET','Azure'); requisitos='C#, .NET, Azure'; estado=1 },
    [pscustomobject]@{ id=6; titulo='QA Automation'; descripcion='Automatizacion de pruebas'; empresa='Quality Hub'; ubicacion='Mendoza'; modalidad='Remoto'; tipoContrato='Contrato'; salario=1700000; tecnologias=@('Selenium','JavaScript'); requisitos='Selenium, JavaScript'; estado=1 },
    [pscustomobject]@{ id=7; titulo='Soporte IT'; descripcion='Mesa de ayuda y Active Directory'; empresa='Soporte Uno'; ubicacion='La Plata'; modalidad='Presencial'; tipoContrato='Tiempo completo'; salario=1200000; tecnologias=@('Windows','Active Directory'); requisitos='Windows, Active Directory'; estado=1 },
    [pscustomobject]@{ id=8; titulo='Frontend Junior'; descripcion='Interfaces con React y CSS'; empresa='PixelAR'; ubicacion='Buenos Aires'; modalidad='Remoto'; tipoContrato='Pasantia'; salario=850000; tecnologias=@('React','CSS','HTML'); requisitos='React, CSS, HTML'; estado=1 },
    [pscustomobject]@{ id=9; titulo='DevOps Engineer'; descripcion='CI/CD y plataformas cloud'; empresa='DeployNow'; ubicacion='Cordoba'; modalidad='Hibrido'; tipoContrato='Contrato'; salario=3000000; tecnologias=@('Docker','Kubernetes','Azure'); requisitos='Docker, Kubernetes, Azure'; estado=1 },
    [pscustomobject]@{ id=10; titulo='Desarrollador Python'; descripcion='Servicios backend en Python'; empresa='PyLabs'; ubicacion='Rosario'; modalidad='Remoto'; tipoContrato='Part time'; salario=1600000; tecnologias=@('Python','PostgreSQL'); requisitos='Python, PostgreSQL'; estado=1 }
)

function Enviar-Json($response, $objeto, [int]$codigo=200) {
    $json = ConvertTo-Json -InputObject $objeto -Depth 10 -Compress
    $bytes = [System.Text.Encoding]::UTF8.GetBytes($json)
    $response.StatusCode = $codigo
    $response.ContentType = 'application/json; charset=utf-8'
    $response.Headers.Add('Access-Control-Allow-Origin', '*')
    $response.Headers.Add('Access-Control-Allow-Methods', 'GET, OPTIONS')
    $response.Headers.Add('Access-Control-Allow-Headers', 'Content-Type')
    $response.ContentLength64 = $bytes.Length
    $response.OutputStream.Write($bytes, 0, $bytes.Length)
    $response.Close()
}

$listener = New-Object System.Net.HttpListener
$listener.Prefixes.Add("http://localhost:$Puerto/")
$listener.Start()
Write-Host "Mock Empleos iniciado: http://localhost:$Puerto" -ForegroundColor Green
Write-Host 'Endpoints: /api/empleos, /api/empleos/activos, /api/empleos/{id}'
Write-Host 'Filtros: q, titulo, ubicacion, modalidad, tipoContrato, tecnologia, salarioMin, salarioMax'
Write-Host 'Detener: Ctrl+C'

try {
    while ($listener.IsListening) {
        $context = $listener.GetContext()
        $request = $context.Request
        $path = $request.Url.AbsolutePath.TrimEnd('/')
        Write-Host "[$($request.HttpMethod)] $($request.RawUrl)"
        if ($request.HttpMethod -eq 'OPTIONS') {
            Enviar-Json $context.Response @{}
            continue
        }
        if ($request.HttpMethod -ne 'GET') {
            Enviar-Json $context.Response @{ error='Metodo no permitido' } 405
            continue
        }
        if ($path -eq '/api/empleos' -or $path -eq '/api/empleos/activos') {
            $resultado = @($empleos | Where-Object { $_.estado -eq 1 })
            $qs = $request.QueryString
            foreach ($campo in @('titulo','ubicacion','modalidad','tipoContrato')) {
                $valor = $qs[$campo]
                if (-not [string]::IsNullOrWhiteSpace($valor)) {
                    $resultado = @($resultado | Where-Object { $_.$campo -like "*$valor*" })
                }
            }
            $q = $qs['q']
            if (-not [string]::IsNullOrWhiteSpace($q)) {
                $resultado = @($resultado | Where-Object { $_.titulo -like "*$q*" -or $_.descripcion -like "*$q*" -or $_.empresa -like "*$q*" })
            }
            $tecnologia = $qs['tecnologia']
            if (-not [string]::IsNullOrWhiteSpace($tecnologia)) {
                $resultado = @($resultado | Where-Object { @($_.tecnologias | Where-Object { $_ -like "*$tecnologia*" }).Count -gt 0 })
            }
            $min = 0L
            if ([long]::TryParse($qs['salarioMin'], [ref]$min)) {
                $resultado = @($resultado | Where-Object { $_.salario -ge $min })
            }
            $max = 0L
            if ([long]::TryParse($qs['salarioMax'], [ref]$max)) {
                $resultado = @($resultado | Where-Object { $_.salario -le $max })
            }
            Enviar-Json $context.Response @($resultado)
        }
        elseif ($path -match '^/api/empleos/(\d+)$') {
            $id = [int]$Matches[1]
            $empleo = $empleos | Where-Object { $_.id -eq $id } | Select-Object -First 1
            if ($null -eq $empleo) { Enviar-Json $context.Response @{error='Empleo no encontrado'} 404 }
            else { Enviar-Json $context.Response $empleo }
        }
        else {
            Enviar-Json $context.Response @{error='Ruta no encontrada'} 404
        }
    }
}
finally {
    $listener.Stop()
    $listener.Close()
}
