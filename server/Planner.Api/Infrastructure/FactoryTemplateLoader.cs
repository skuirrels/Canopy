using System.Text.Json;
using Planner.Api.Application;
using Planner.Api.Domain;

namespace Planner.Api.Infrastructure;

public static class FactoryTemplateLoader
{
    private sealed record Catalogue(List<FactoryTemplate> FactoryTemplates);
    public static IReadOnlyList<FactoryTemplate> Load(string path)
    {
        // Deserialize the actual document: configuration binding can omit records
        // whose constructor contains a required, empty dependency array.
        var templates = JsonSerializer.Deserialize<Catalogue>(File.ReadAllText(path), FactoryRules.Json)?.FactoryTemplates
            ?? throw new InvalidOperationException("FactoryTemplates must be an array in the configured template file.");
        if (templates.Any(t => t is null || string.IsNullOrWhiteSpace(t.Id) || string.IsNullOrWhiteSpace(t.Source)) || templates.Select(t => t.Id).Distinct().Count() != templates.Count)
            throw new InvalidOperationException("Factory templates require unique IDs and provenance.");
        foreach (var template in templates)
        {
            try { Rules.Name(template.Id, "Template ID"); Rules.Name(template.Name, "Template name"); Rules.Name(template.Source, "Template source"); FactoryRules.RequireStructure(template.Definition); }
            catch (RuleException error) { throw new InvalidOperationException($"Invalid factory template '{template.Id}': {error.Message}", error); }
        }
        return templates;
    }
}
