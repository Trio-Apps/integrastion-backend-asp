using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;

namespace OrderXChange.Application.Integrations.Foodics;

internal static class FoodicsModifierSanitizer
{
    public static List<FoodicsModifierDto>? SanitizeForMenuProjection(List<FoodicsModifierDto>? modifiers)
    {
        if (modifiers == null || modifiers.Count == 0)
        {
            return modifiers;
        }

        return DistinctOrderedModifiers(modifiers)
            .Select(SanitizeModifier)
            .Where(modifier => modifier.Options is { Count: > 0 })
            .ToList();
    }

    public static IEnumerable<FoodicsModifierOptionDto> GetVisibleOptions(FoodicsModifierDto modifier)
    {
        if (modifier.Options == null || modifier.Options.Count == 0)
        {
            return Enumerable.Empty<FoodicsModifierOptionDto>();
        }

        var excludedOptionIds = modifier.Pivot?.ExcludedOptionIds?
            .Where(id => !string.IsNullOrWhiteSpace(id))
            .ToHashSet(StringComparer.OrdinalIgnoreCase)
            ?? new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        return OrderModifierOptions(modifier.Options)
            .Where(IsVisibleOption)
            .Where(option => !excludedOptionIds.Contains(option.Id))
            .GroupBy(option => option.Id, StringComparer.OrdinalIgnoreCase)
            .Select(group => group.First())
            .GroupBy(BuildDisplayKey)
            .Select(group => group.First());
    }

    private static FoodicsModifierDto SanitizeModifier(FoodicsModifierDto modifier)
    {
        var visibleOptions = GetVisibleOptions(modifier).ToList();
        var visibleOptionIds = visibleOptions
            .Select(option => option.Id)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        return new FoodicsModifierDto
        {
            Id = modifier.Id,
            Name = modifier.Name,
            NameLocalized = modifier.NameLocalized,
            MinAllowed = modifier.MinAllowed,
            MaxAllowed = modifier.MaxAllowed,
            Pivot = SanitizePivot(modifier.Pivot, visibleOptionIds),
            Options = visibleOptions
        };
    }

    private static FoodicsModifierPivotDto? SanitizePivot(
        FoodicsModifierPivotDto? pivot,
        ISet<string> visibleOptionIds)
    {
        if (pivot == null)
        {
            return null;
        }

        return new FoodicsModifierPivotDto
        {
            Index = pivot.Index,
            MinimumOptions = pivot.MinimumOptions,
            MaximumOptions = pivot.MaximumOptions,
            FreeOptions = pivot.FreeOptions,
            UniqueOptions = pivot.UniqueOptions,
            DefaultOptionIds = pivot.DefaultOptionIds?
                .Where(id => !string.IsNullOrWhiteSpace(id) && visibleOptionIds.Contains(id))
                .ToList(),
            ExcludedOptionIds = new List<string>()
        };
    }

    private static IEnumerable<FoodicsModifierDto> DistinctOrderedModifiers(IEnumerable<FoodicsModifierDto> modifiers)
    {
        return modifiers
            .OrderBy(m => m.Pivot?.Index ?? int.MaxValue)
            .ThenBy(m => m.Id, StringComparer.OrdinalIgnoreCase)
            .Where(m => !string.IsNullOrWhiteSpace(m.Id))
            .GroupBy(m => m.Id, StringComparer.OrdinalIgnoreCase)
            .Select(g => g.First());
    }

    private static IEnumerable<FoodicsModifierOptionDto> OrderModifierOptions(IEnumerable<FoodicsModifierOptionDto> options)
    {
        return options
            .OrderBy(o => o.Index ?? int.MaxValue)
            .ThenBy(o => o.Id, StringComparer.OrdinalIgnoreCase)
            .Where(o => !string.IsNullOrWhiteSpace(o.Id));
    }

    private static bool IsVisibleOption(FoodicsModifierOptionDto option)
    {
        if (string.IsNullOrWhiteSpace(option.Id))
        {
            return false;
        }

        if (option.IsDeleted == true || !string.IsNullOrWhiteSpace(option.DeletedAt) || option.IsActive == false)
        {
            return false;
        }

        if (option.Branches is { Count: > 0 } &&
            option.Branches.All(branch => branch.IsActive == false || branch.Pivot?.IsActive == false))
        {
            return false;
        }

        return true;
    }

    private static string BuildDisplayKey(FoodicsModifierOptionDto option)
    {
        return NormalizeOptionName(option.Name)
            ?? NormalizeOptionName(option.NameLocalized)
            ?? $"ID:{option.Id}";
    }

    private static string? NormalizeOptionName(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        var normalized = value.Normalize(NormalizationForm.FormKC);
        var builder = new StringBuilder(normalized.Length);
        var previousWasSpace = false;

        foreach (var c in normalized)
        {
            var category = char.GetUnicodeCategory(c);
            if (category is UnicodeCategory.Control or UnicodeCategory.Format)
            {
                continue;
            }

            if (char.IsWhiteSpace(c) || char.IsPunctuation(c) || char.IsSeparator(c))
            {
                if (!previousWasSpace && builder.Length > 0)
                {
                    builder.Append(' ');
                    previousWasSpace = true;
                }

                continue;
            }

            builder.Append(char.ToUpperInvariant(c));
            previousWasSpace = false;
        }

        var result = builder.ToString().Trim();
        return result.Length == 0 ? null : result;
    }
}
