using System.Collections.Generic;
using System.Text.Json;

namespace TypeSafe.AI;

/// <summary>Metadata for an available model.</summary>
public sealed class ModelCard
{
    internal ModelCard(string name, string description, string releaseDate, IReadOnlyDictionary<string, JsonElement> additionalProperties)
    {
        Name = name;
        Description = description;
        ReleaseDate = releaseDate;
        AdditionalProperties = additionalProperties;
    }

    /// <summary>Gets the model name, for use as a request model.</summary>
    public string Name { get; }

    /// <summary>Gets the model description.</summary>
    public string Description { get; }

    /// <summary>Gets the release date as sent by the server; its format is not guaranteed.</summary>
    public string ReleaseDate { get; }

    /// <summary>Gets any other fields sent by the server.</summary>
    public IReadOnlyDictionary<string, JsonElement> AdditionalProperties { get; }
}
