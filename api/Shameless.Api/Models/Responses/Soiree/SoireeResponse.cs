using Shameless.Api.Models.Responses.Picker;
using Shameless.Application.Entities;

namespace Shameless.Api.Models.Responses.Soiree;

/// <summary>Une Soirée, avec sa session de Picker en cours (null s'il n'y en a pas).</summary>
public sealed record SoireeResponse(int Id, SoireeStatus Status, DateTime OpenedAt, DateTime? ClosedAt, PickerResponse? Picker = null)
{
    public static SoireeResponse From(Application.Entities.Soiree soiree, PickerSession? picker = null) =>
        new(soiree.Id, soiree.Status, soiree.OpenedAt, soiree.ClosedAt, picker == null ? null : PickerResponse.From(picker));
}

/// <summary>Les Séries d'une Soirée avec leur issue, de la plus ancienne à la plus récente.</summary>
public sealed record SoireeSeriesResponse(List<Application.DTOs.Match.SeriesResponse> Series);

/// <summary>La Soirée ouverte, ou <c>soiree: null</c> s'il n'y en a pas.</summary>
public sealed record OpenSoireeResponse(SoireeResponse? Soiree);
