using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Discord;
using Discord.Interactions;
using SubgenreSheetBot.Services;

namespace SubgenreSheetBot.Autocomplete;

public class SubgenreAutocomplete : AutocompleteHandler
{
    private readonly SheetService sheetService;

    public SubgenreAutocomplete(SheetService sheetService)
    {
        this.sheetService = sheetService;
    }

    public override Task<AutocompletionResult> GenerateSuggestionsAsync(IInteractionContext context, IAutocompleteInteraction autocompleteInteraction, IParameterInfo parameter, IServiceProvider services)
    {
        var subgenres = SheetService.GetMostCommonSubgenres();
        var value = autocompleteInteraction.Data.Current.Value.ToString();

        IEnumerable<string> results;
        if (string.IsNullOrWhiteSpace(value))
            results = subgenres.Take(25);
        else
            results = subgenres.Where(sg => sg.Contains(value, StringComparison.OrdinalIgnoreCase)).Take(25);

        return Task.FromResult(AutocompletionResult.FromSuccess(results.Select(sg => new AutocompleteResult(sg, sg))));
    }
}