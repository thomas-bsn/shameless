using System.ComponentModel.DataAnnotations;

namespace Shameless.Api.Models.Requests.Companion;

/// <summary>Le signal de présence du Companion : le PUUID du compte connecté au client LoL.</summary>
public sealed record PresenceRequest([Required(AllowEmptyStrings = false)] string Puuid);
