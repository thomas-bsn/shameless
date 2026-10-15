using Shameless.Application.Entities;

namespace Shameless.Api.Models.Responses.Soiree;

public sealed record SoireeResponse(int Id, SoireeStatus Status, DateTime OpenedAt, DateTime? ClosedAt)
{
    public static SoireeResponse From(Application.Entities.Soiree soiree) =>
        new(soiree.Id, soiree.Status, soiree.OpenedAt, soiree.ClosedAt);
}

/// <summary>La Soirée ouverte, ou <c>soiree: null</c> s'il n'y en a pas.</summary>
public sealed record OpenSoireeResponse(SoireeResponse? Soiree);
