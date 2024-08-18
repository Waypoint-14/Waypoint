using System.Linq;
using Robust.Shared.Prototypes;

namespace Content.Shared.Prototypes;

public abstract class LocalizedPrototype : IPrototype
{
    [IdDataField]
    public string ID { get; } = default!;

    public const string LocFormat = "{type}-{ID}-{field}";

    /// <summary>The localization string for the name of this prototype</summary>
    public string NameLoc => ToLocalizationString("name");
    /// <summary>The localized string for the name of prototype</summary>
    public string Name => Loc.GetString(NameLoc);

    public string ToLocalizationString(string field)
    {
        // Get the ID of the proto Type
        var type =
            ((PrototypeAttribute?) Attribute.GetCustomAttribute(GetType(), typeof(PrototypeAttribute)))?.Type
            ?? GetType().Name.Remove(GetType().Name.Length - 9);
        // Lowercase the first letter
        type = char.ToLowerInvariant(type[0]) + type[1..];
        // Replace every uppercase letter with a dash and the lowercase letter
        type = type.Aggregate("", (current, c) => current + (char.IsUpper(c) ? "-" + char.ToLowerInvariant(c) : c.ToString()));

        // Replace the placeholders with the actual values
        var t = LocFormat
            .Replace("{type}", type)
            .Replace("{ID}", ID)
            .Replace("{field}", field);

        return t;
    }
}
