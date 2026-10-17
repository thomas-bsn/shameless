namespace Shameless.Api.Models.Requests.Clips;

/// <summary>La Game à laquelle rattacher un clip, à la main (#64).</summary>
public sealed record RattacherClipRequest(int GameId);
