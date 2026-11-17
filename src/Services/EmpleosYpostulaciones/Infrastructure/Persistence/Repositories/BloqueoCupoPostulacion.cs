using EmpleosYpostulaciones.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;

namespace EmpleosYpostulaciones.Infrastructure.Persistence.Repositories;

internal static class BloqueoCupoPostulacion
{
    // Requiere transaccion abierta. Usa un mismo bloqueo por usuario en todos los flujos.
    public static async Task TomarAsync(AppDbContext db, Guid usuarioId, CancellationToken ct)
    {
        await using var cmd = db.Database.GetDbConnection().CreateCommand();
        cmd.Transaction = db.Database.CurrentTransaction!.GetDbTransaction();
        cmd.CommandText = "SELECT pg_advisory_xact_lock(hashtext(@id))";
        var p = cmd.CreateParameter();
        p.ParameterName = "id"; p.Value = usuarioId.ToString("N");
        cmd.Parameters.Add(p);
        await cmd.ExecuteScalarAsync(ct);
    }
}