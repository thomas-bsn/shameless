using Microsoft.EntityFrameworkCore.Storage.ValueConversion;
using Shameless.Application.Picker;

namespace Shameless.Infrastructure.Data.Configurations;

/// <summary>Un rôle de l'Autorole stocké en texte minuscule (« top », « adc »…).</summary>
internal static class RoleText
{
    public static readonly ValueConverter<Role, string> Converter =
        new(role => role.ToString().ToLowerInvariant(), text => Enum.Parse<Role>(text, true));
}
