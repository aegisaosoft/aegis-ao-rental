/*
 * CarRental.Tests - Unit Tests for Aegis AO Rental System
 * Copyright (c) 2025 Alexander Orlov
 */

using Xunit;
using FluentAssertions;
using CarRental.Api.Helpers;

namespace CarRental.Tests.Helpers;

/// <summary>
/// Unit tests for the CurrencyHelper
/// </summary>
public class CurrencyHelperTests
{
    #region GetCurrencyForCountry Tests

    [Theory]
    [InlineData("United States", "USD")]
    [InlineData("united states", "USD")]
    [InlineData("UNITED STATES", "USD")]
    [InlineData("US", "USD")]
    [InlineData("us", "USD")]
    [InlineData("USA", "USD")]
    public void GetCurrencyForCountry_UnitedStates_ReturnsUSD(string country, string expected)
    {
        // Act
        var result = CurrencyHelper.GetCurrencyForCountry(country);

        // Assert
        result.Should().Be(expected);
    }

    [Theory]
    [InlineData("Canada", "CAD")]
    [InlineData("CA", "CAD")]
    [InlineData("ca", "CAD")]
    public void GetCurrencyForCountry_Canada_ReturnsCAD(string country, string expected)
    {
        // Act
        var result = CurrencyHelper.GetCurrencyForCountry(country);

        // Assert
        result.Should().Be(expected);
    }

    [Theory]
    [InlineData("Mexico", "MXN")]
    [InlineData("MX", "MXN")]
    [InlineData("mx", "MXN")]
    public void GetCurrencyForCountry_Mexico_ReturnsMXN(string country, string expected)
    {
        // Act
        var result = CurrencyHelper.GetCurrencyForCountry(country);

        // Assert
        result.Should().Be(expected);
    }

    [Theory]
    [InlineData("Brazil", "BRL")]
    [InlineData("BR", "BRL")]
    [InlineData("Argentina", "ARS")]
    [InlineData("AR", "ARS")]
    [InlineData("Chile", "CLP")]
    [InlineData("CL", "CLP")]
    [InlineData("Colombia", "COP")]
    [InlineData("CO", "COP")]
    [InlineData("Peru", "PEN")]
    [InlineData("PE", "PEN")]
    public void GetCurrencyForCountry_SouthAmerica_ReturnsCorrectCurrency(string country, string expected)
    {
        // Act
        var result = CurrencyHelper.GetCurrencyForCountry(country);

        // Assert
        result.Should().Be(expected);
    }

    [Theory]
    [InlineData("Jamaica", "JMD")]
    [InlineData("Trinidad and Tobago", "TTD")]
    [InlineData("Bahamas", "BSD")]
    [InlineData("Barbados", "BBD")]
    [InlineData("Belize", "BZD")]
    public void GetCurrencyForCountry_Caribbean_ReturnsCorrectCurrency(string country, string expected)
    {
        // Act
        var result = CurrencyHelper.GetCurrencyForCountry(country);

        // Assert
        result.Should().Be(expected);
    }

    [Theory]
    [InlineData("Dominica", "XCD")]
    [InlineData("Grenada", "XCD")]
    [InlineData("Saint Lucia", "XCD")]
    [InlineData("Saint Vincent and the Grenadines", "XCD")]
    [InlineData("Antigua and Barbuda", "XCD")]
    [InlineData("Saint Kitts and Nevis", "XCD")]
    [InlineData("Montserrat", "XCD")]
    [InlineData("Anguilla", "XCD")]
    public void GetCurrencyForCountry_EasternCaribbean_ReturnsXCD(string country, string expected)
    {
        // Act
        var result = CurrencyHelper.GetCurrencyForCountry(country);

        // Assert
        result.Should().Be(expected);
    }

    [Theory]
    [InlineData("Costa Rica", "CRC")]
    [InlineData("Guatemala", "GTQ")]
    [InlineData("Honduras", "HNL")]
    [InlineData("Nicaragua", "NIO")]
    [InlineData("Panama", "PAB")]
    public void GetCurrencyForCountry_CentralAmerica_ReturnsCorrectCurrency(string country, string expected)
    {
        // Act
        var result = CurrencyHelper.GetCurrencyForCountry(country);

        // Assert
        result.Should().Be(expected);
    }

    [Theory]
    [InlineData("Puerto Rico", "USD")]
    [InlineData("US Virgin Islands", "USD")]
    [InlineData("British Virgin Islands", "USD")]
    [InlineData("El Salvador", "USD")]
    [InlineData("Ecuador", "USD")]
    [InlineData("Turks and Caicos Islands", "USD")]
    public void GetCurrencyForCountry_USDollarTerritories_ReturnsUSD(string country, string expected)
    {
        // Act
        var result = CurrencyHelper.GetCurrencyForCountry(country);

        // Assert
        result.Should().Be(expected);
    }

    [Theory]
    [InlineData(null, "USD")]
    [InlineData("", "USD")]
    [InlineData("   ", "USD")]
    [InlineData("Unknown Country", "USD")]
    [InlineData("Narnia", "USD")]
    public void GetCurrencyForCountry_InvalidOrUnknown_ReturnsUSD(string? country, string expected)
    {
        // Act
        var result = CurrencyHelper.GetCurrencyForCountry(country);

        // Assert
        result.Should().Be(expected);
    }

    [Fact]
    public void GetCurrencyForCountry_WithExtraSpaces_ReturnsCorrectCurrency()
    {
        // Arrange
        var country = "  Canada  ";

        // Act
        var result = CurrencyHelper.GetCurrencyForCountry(country);

        // Assert
        result.Should().Be("CAD");
    }

    #endregion

    #region ResolveCurrency Tests

    [Fact]
    public void ResolveCurrency_ExplicitCurrencyProvided_ReturnsExplicitCurrency()
    {
        // Act
        var result = CurrencyHelper.ResolveCurrency("EUR", "United States");

        // Assert
        result.Should().Be("EUR");
    }

    [Fact]
    public void ResolveCurrency_ExplicitCurrencyLowercase_ReturnsUppercase()
    {
        // Act
        var result = CurrencyHelper.ResolveCurrency("cad", "Mexico");

        // Assert
        result.Should().Be("CAD");
    }

    [Fact]
    public void ResolveCurrency_ExplicitCurrencyWithSpaces_ReturnsNormalized()
    {
        // Act
        var result = CurrencyHelper.ResolveCurrency("  MXN  ", "Canada");

        // Assert
        result.Should().Be("MXN");
    }

    [Fact]
    public void ResolveCurrency_NullExplicitCurrency_FallsBackToCountry()
    {
        // Act
        var result = CurrencyHelper.ResolveCurrency(null, "Brazil");

        // Assert
        result.Should().Be("BRL");
    }

    [Fact]
    public void ResolveCurrency_EmptyExplicitCurrency_FallsBackToCountry()
    {
        // Act
        var result = CurrencyHelper.ResolveCurrency("", "Japan");

        // Assert - Japan not in map, defaults to USD
        result.Should().Be("USD");
    }

    [Fact]
    public void ResolveCurrency_UnsupportedExplicitCurrency_FallsBackToCountry()
    {
        // Act
        var result = CurrencyHelper.ResolveCurrency("XYZ", "Argentina");

        // Assert
        result.Should().Be("ARS");
    }

    [Fact]
    public void ResolveCurrency_BothNull_ReturnsUSD()
    {
        // Act
        var result = CurrencyHelper.ResolveCurrency(null, null);

        // Assert
        result.Should().Be("USD");
    }

    #endregion

    #region IsSupportedCurrency Tests

    [Theory]
    [InlineData("USD", true)]
    [InlineData("usd", true)]
    [InlineData("CAD", true)]
    [InlineData("MXN", true)]
    [InlineData("BRL", true)]
    [InlineData("EUR", true)]
    public void IsSupportedCurrency_ValidCurrency_ReturnsTrue(string currency, bool expected)
    {
        // Act
        var result = CurrencyHelper.IsSupportedCurrency(currency);

        // Assert
        result.Should().Be(expected);
    }

    [Theory]
    [InlineData("XYZ", false)]
    [InlineData("ABC", false)]
    [InlineData("GBP", false)] // British Pound not in Americas list
    [InlineData("JPY", false)] // Japanese Yen not in Americas list
    [InlineData("CNY", false)] // Chinese Yuan not in Americas list
    public void IsSupportedCurrency_InvalidCurrency_ReturnsFalse(string currency, bool expected)
    {
        // Act
        var result = CurrencyHelper.IsSupportedCurrency(currency);

        // Assert
        result.Should().Be(expected);
    }

    [Theory]
    [InlineData(null, false)]
    [InlineData("", false)]
    [InlineData("   ", false)]
    public void IsSupportedCurrency_NullOrEmpty_ReturnsFalse(string? currency, bool expected)
    {
        // Act
        var result = CurrencyHelper.IsSupportedCurrency(currency);

        // Assert
        result.Should().Be(expected);
    }

    [Fact]
    public void IsSupportedCurrency_WithSpaces_ReturnsTrue()
    {
        // Act
        var result = CurrencyHelper.IsSupportedCurrency("  USD  ");

        // Assert
        result.Should().BeTrue();
    }

    #endregion

    #region SupportedCurrencies Tests

    [Fact]
    public void SupportedCurrencies_ShouldContainExpectedCurrencies()
    {
        // Assert
        CurrencyHelper.SupportedCurrencies.Should().Contain("USD");
        CurrencyHelper.SupportedCurrencies.Should().Contain("CAD");
        CurrencyHelper.SupportedCurrencies.Should().Contain("MXN");
        CurrencyHelper.SupportedCurrencies.Should().Contain("BRL");
        CurrencyHelper.SupportedCurrencies.Should().Contain("EUR");
    }

    [Fact]
    public void SupportedCurrencies_ShouldBeSortedAlphabetically()
    {
        // Assert
        CurrencyHelper.SupportedCurrencies.Should().BeInAscendingOrder();
    }

    [Fact]
    public void SupportedCurrencies_ShouldBeReadOnly()
    {
        // Assert
        CurrencyHelper.SupportedCurrencies.Should().BeOfType<System.Collections.ObjectModel.ReadOnlyCollection<string>>();
    }

    [Fact]
    public void SupportedCurrencies_ShouldHaveExpectedCount()
    {
        // Assert - 33 currencies as defined in the helper
        CurrencyHelper.SupportedCurrencies.Should().HaveCountGreaterThanOrEqualTo(30);
    }

    #endregion
}
