using System.ComponentModel;
using System.Linq;
using System.Reflection;
using LumenHut.Services;

namespace LumenHut.Tests;

/// <summary>
/// Guards the two things that silently break a bilingual UI: a string that exists in only one
/// language, and a language switch that fails to invalidate the bindings.
/// </summary>
[Collection(UiLanguageCollection.Name)]
public class LocalizationTests
{
    private static PropertyInfo[] TextProperties => typeof(Strings)
        .GetProperties(BindingFlags.Public | BindingFlags.Instance)
        .Where(p => p.PropertyType == typeof(string))
        .ToArray();

    [Theory]
    [InlineData(AppLanguage.German)]
    [InlineData(AppLanguage.English)]
    public void EveryStringHasTextInBothLanguages(AppLanguage language)
    {
        var strings = Strings.Instance;
        var previous = strings.Language;

        try
        {
            strings.Language = language;

            foreach (var property in TextProperties)
            {
                var value = property.GetValue(strings) as string;
                Assert.False(string.IsNullOrWhiteSpace(value),
                    $"{property.Name} has no text for {language}.");
            }
        }
        finally
        {
            strings.Language = previous;
        }
    }

    [Fact]
    public void SwitchingLanguageChangesTheText()
    {
        var strings = Strings.Instance;
        var previous = strings.Language;

        try
        {
            strings.Language = AppLanguage.German;
            var german = strings.NavMeasure;

            strings.Language = AppLanguage.English;

            Assert.NotEqual(german, strings.NavMeasure);
        }
        finally
        {
            strings.Language = previous;
        }
    }

    /// <summary>An empty property name tells Avalonia that every binding must be re-evaluated.</summary>
    [Fact]
    public void SwitchingLanguageInvalidatesAllBindings()
    {
        var strings = Strings.Instance;
        var previous = strings.Language;
        string? raisedFor = null;
        var raised = 0;

        void Handler(object? sender, PropertyChangedEventArgs e)
        {
            raisedFor = e.PropertyName;
            raised++;
        }

        // Start from a known language before subscribing: the default follows the system
        // culture, so counting the switch itself only works from a fixed starting point.
        strings.Language = AppLanguage.German;
        strings.PropertyChanged += Handler;

        try
        {
            strings.Language = AppLanguage.English;

            Assert.Equal(1, raised);
            Assert.True(string.IsNullOrEmpty(raisedFor));
        }
        finally
        {
            strings.PropertyChanged -= Handler;
            strings.Language = previous;
        }
    }
}
