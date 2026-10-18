using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Shameless.Application.Entities;

namespace Shameless.Infrastructure.Data.Configurations;

/// <summary>Les Clips de <c>#clips</c> (ADR 0014) ; la vidéo est un fichier du stockage des clips.</summary>
public sealed class ClipConfiguration : IEntityTypeConfiguration<Clip>
{
    public void Configure(EntityTypeBuilder<Clip> entity)
    {
        entity.ToTable("clips");
        entity.HasKey(c => c.Id);
        entity.Property(c => c.Id).UseIdentityByDefaultColumn();
        entity.Property(c => c.AuteurNom).HasMaxLength(100);
        entity.Property(c => c.NomFichier).HasMaxLength(255);
        entity.Property(c => c.ContentType).HasMaxLength(100);
        entity.Property(c => c.Fichier).HasMaxLength(100);
        entity.Property(c => c.Rattachement).HasColumnType("text");
        entity.Property(c => c.PublieLe).HasColumnType("timestamp without time zone");
        entity.Property(c => c.RecuLe).HasColumnType("timestamp without time zone");
        entity.Ignore(c => c.RattachementEffectif);

        // Le clip survit à sa Game (game en attente d'une Série arrêtée, ADR 0009) : il redevient sans Game.
        entity.HasOne(c => c.Game).WithMany().HasForeignKey(c => c.GameId).OnDelete(DeleteBehavior.SetNull);

        // Un seul clip par pièce jointe Discord : le dépôt du bot est idempotent.
        entity.HasIndex(c => c.DiscordAttachmentId).IsUnique();
        entity.HasIndex(c => c.GameId);

        entity.ToTable(t => t.HasCheckConstraint("ck_clips_rattachement", "rattachement IN ('automatique', 'manuel')"));
    }
}
