using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Shameless.Application.Entities;

namespace Shameless.Infrastructure.Data.Configurations;

/// <summary>Les messages du chat d'une Draft de champions.</summary>
public sealed class MessageDraftChampionsConfiguration : IEntityTypeConfiguration<MessageDraftChampions>
{
    public void Configure(EntityTypeBuilder<MessageDraftChampions> entity)
    {
        entity.ToTable("drafts_champions_messages");
        entity.HasKey(e => e.Id);
        entity.Property(e => e.Id).UseIdentityByDefaultColumn();
        entity.Property(e => e.Emetteur).IsRequired().HasMaxLength(100);
        entity.Property(e => e.Auteur).IsRequired().HasMaxLength(100);
        entity.Property(e => e.Equipe).HasMaxLength(1);
        entity.Property(e => e.Contenu).IsRequired().HasMaxLength(MessageDraftChampions.LongueurMax);
        entity.Property(e => e.EnvoyeLe).HasColumnType("timestamp without time zone");
        entity.HasIndex(e => new { e.DraftChampionsId, e.Emetteur, e.EnvoyeLe });

        entity.HasOne<DraftChampions>()
              .WithMany()
              .HasForeignKey(e => e.DraftChampionsId)
              .OnDelete(DeleteBehavior.Cascade);
    }
}
