using EmpleosYpostulaciones.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;

namespace EmpleosYpostulaciones.Infrastructure.Persistence.Repositories;

internal static class BloqueoOfertaUsuario
{
    public static async Task TomarAsync(AppDbContext db, Guid usuarioId,
        Guid empleoId, CancellationToken ct)
    {
        await using var comando = db.Database.GetDbConnection().CreateCommand();
        comando.Transaction = db.Database.CurrentTransaction!.GetDbTransaction();
        comando.CommandText = "SELECT pg_advisory_xact_lock(hashtext(@usuario), hashtext(@empleo))";
        var u = comando.CreateParameter();
        u.ParameterName = "usuario"; u.Value = usuarioId.ToString("N");
        comando.Parameters.Add(u);
        var e = comando.CreateParameter();
        e.ParameterName = "empleo"; e.Value = empleoId.ToString("N");
        comando.Parameters.Add(e);
        await comando.ExecuteScalarAsync(ct);
    }
}