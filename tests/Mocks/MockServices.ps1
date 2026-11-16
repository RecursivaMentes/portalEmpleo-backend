
#Add-Type -AssemblyName System.Net.HttpListener

$servidores = @(
    @{ Puerto = 5081; Tipo = "Usuarios" },
    @{ Puerto = 5082; Tipo = "Empleos" }
)

$jobs = foreach ($servidor in $servidores) {
    Start-Job -ArgumentList $servidor.Puerto, $servidor.Tipo -ScriptBlock {
        param($puerto, $tipo)

        $listener = [System.Net.HttpListener]::new()
        $listener.Prefixes.Add("http://localhost:$puerto/")
        $listener.Start()

        Write-Output "$tipo escuchando en puerto $puerto"

        while ($listener.IsListening) {
            $contexto = $listener.GetContext()
            $ruta = $contexto.Request.Url.AbsolutePath

            $contexto.Response.ContentType = "application/json; charset=utf-8"

            if ($tipo -eq "Usuarios" -and
                $ruta -match '^/api/perfiles/([0-9a-fA-F-]{36})$') {

                $usuarioId = $Matches[1]

                $respuesta = @{
                    usuarioId = $usuarioId
                    habilidadesJson = '["C#","SQL","React"]'
                    resumen = "Desarrollador de software"
                } | ConvertTo-Json -Compress
            }
            elseif ($tipo -eq "Empleos" -and
                    $ruta -eq "/api/empleos/activos") {

                $respuesta = @(
                    @{
                        id = "22222222-2222-2222-2222-222222222222"
                        titulo = "Desarrollador .NET"
                        requisitos = "C#, SQL, Docker"
                        estado = 1
                        fechaCierre = $null
                    },
                    @{
                        id = "33333333-3333-3333-3333-333333333333"
                        titulo = "Desarrollador React"
                        requisitos = "React, JavaScript"
                        estado = 1
                        fechaCierre = $null
                    },
                    @{
                        id = "44444444-4444-4444-4444-444444444444"
                        titulo = "Administrador Linux"
                        requisitos = "Linux, Docker, Kubernetes"
                        estado = 1
                        fechaCierre = $null
                    }
                ) | ConvertTo-Json -Depth 5 -Compress
            }
            else {
                $contexto.Response.StatusCode = 404
                $respuesta = '{"mensaje":"Recurso no encontrado"}'
            }

            $bytes = [System.Text.Encoding]::UTF8.GetBytes($respuesta)
            $contexto.Response.ContentLength64 = $bytes.Length
            $contexto.Response.OutputStream.Write($bytes, 0, $bytes.Length)
            $contexto.Response.Close()
        }
    }
}

Write-Host "Servicios simulados iniciados."
Write-Host "Usuarios: http://localhost:5081"
Write-Host "Empleos: http://localhost:5082"
Write-Host "Para detenerlos: Get-Job | Stop-Job"

$jobs | Receive-Job
