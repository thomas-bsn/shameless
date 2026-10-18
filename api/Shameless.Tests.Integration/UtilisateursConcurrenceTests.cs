using Microsoft.EntityFrameworkCore;
using Shameless.Application.Entities;
using Shameless.Infrastructure.Data.Repositories;
using Shameless.Tests.Integration.Infrastructure;

namespace Shameless.Tests.Integration;

/// <summary>
/// <c>users.discord_id</c> est unique : deux connexions ou deux promotions simultanées d'un même compte
/// aboutissent à une seule ligne, la perdante relit la ligne existante au lieu d'échouer.
/// </summary>
public class UtilisateursConcurrenceTests(ApiFactory factory) : IntegrationTest(factory)
{
    private const long DiscordId = 4242;

    [Fact]
    public async Task Des_connexions_simultanees_creent_une_seule_ligne()
    {
        var connexions = Enumerable.Range(0, 8).Select(async i =>
        {
            await using var db = Factory.CreateDbContext();
            return await new UserRepository(db).SignInAsync(DiscordId, $"nom{i}", null, null, null);
        });

        var lignes = await Task.WhenAll(connexions);

        Assert.Single(lignes.Select(l => l.Id).Distinct());
        await using var verif = Factory.CreateDbContext();
        Assert.Equal(1, await verif.Users.CountAsync(u => u.DiscordId == DiscordId, Ct));
    }

    [Fact]
    public async Task Des_promotions_simultanees_creent_une_seule_ligne()
    {
        var promotions = Enumerable.Range(0, 8).Select(async _ =>
        {
            await using var db = Factory.CreateDbContext();
            return await new UserRepository(db).SetRoleAsync(
                DiscordId, UserRole.Superadministrateur, "ligne de commande", DateTime.UtcNow, createIfMissing: true);
        });

        var lignes = await Task.WhenAll(promotions);

        Assert.All(lignes, l => Assert.Equal(UserRole.Superadministrateur, l!.Role));
        await using var verif = Factory.CreateDbContext();
        Assert.Equal(1, await verif.Users.CountAsync(u => u.DiscordId == DiscordId, Ct));
    }
}
