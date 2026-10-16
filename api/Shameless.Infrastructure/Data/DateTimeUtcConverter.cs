using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace Shameless.Infrastructure.Data;

/// <summary>
/// Les colonnes <c>timestamp without time zone</c> portent une heure UTC (ADR 0007, comportement legacy
/// de Npgsql) : écrite telle quelle, relue avec <see cref="DateTimeKind.Utc"/>.
/// </summary>
public sealed class DateTimeUtcConverter() : ValueConverter<DateTime, DateTime>(
    v => v,
    v => DateTime.SpecifyKind(v, DateTimeKind.Utc));
