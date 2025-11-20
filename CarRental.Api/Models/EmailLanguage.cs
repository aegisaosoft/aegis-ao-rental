namespace CarRental.Api.Models;

/// <summary>
/// Supported languages for email templates
/// </summary>
public enum EmailLanguage
{
    English,
    Spanish,
    Portuguese,
    French,
    German
}

/// <summary>
/// Language code mappings
/// </summary>
public static class LanguageCodes
{
    public const string English = "en";
    public const string Spanish = "es";
    public const string Portuguese = "pt";
    public const string French = "fr";
    public const string German = "de";

    public static EmailLanguage FromCode(string? code)
    {
        return code?.ToLower() switch
        {
            "en" => EmailLanguage.English,
            "es" => EmailLanguage.Spanish,
            "pt" => EmailLanguage.Portuguese,
            "fr" => EmailLanguage.French,
            "de" => EmailLanguage.German,
            _ => EmailLanguage.English // Default to English
        };
    }

    public static string ToCode(EmailLanguage language)
    {
        return language switch
        {
            EmailLanguage.English => English,
            EmailLanguage.Spanish => Spanish,
            EmailLanguage.Portuguese => Portuguese,
            EmailLanguage.French => French,
            EmailLanguage.German => German,
            _ => English
        };
    }
}

