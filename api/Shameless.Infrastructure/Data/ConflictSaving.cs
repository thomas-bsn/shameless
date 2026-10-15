using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace Shameless.Infrastructure.Data;

/// <summary>
/// Enregistrer en laissant la base trancher les commandes simultanées : une contrainte d'unicité
/// violée ou une ligne modifiée entre la lecture et l'écriture (jeton de concurrence) est un refus,
/// pas une erreur. Les services le traduisent en conflit (409).
/// </summary>
public static class ConflictSaving
{
    /// <summary>Enregistre les changements en une transaction. Renvoie false si la base les refuse.</summary>
    public static async Task<bool> TrySaveChangesAsync(this DbContext db)
    {
        try
        {
            await db.SaveChangesAsync();
            return true;
        }
        catch (DbUpdateConcurrencyException)
        {
            return false;
        }
        catch (DbUpdateException e) when (e.InnerException is PostgresException { SqlState: PostgresErrorCodes.UniqueViolation })
        {
            return false;
        }
    }

    /// <summary>Ajoute l'entité et l'enregistre. Renvoie false (entité détachée) si la base la refuse.</summary>
    public static async Task<bool> TryAddAsync<T>(this DbContext db, T entity) where T : class
    {
        db.Add(entity);
        if (await db.TrySaveChangesAsync()) return true;

        db.Entry(entity).State = EntityState.Detached;
        return false;
    }
}
