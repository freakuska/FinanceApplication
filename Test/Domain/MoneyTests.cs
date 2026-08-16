using FinanceApp.Dbo.Models;
using FluentAssertions;

namespace FinanceApp.Tests.Domain;

public class MoneyTests
{
    [Fact]
    public void Constructor_ShouldRoundAmountAndUppercaseCurrency()
    {
        var money = new Money(10.126m, "usd");

        money.Amount.Should().Be(10.13m);
        money.Currency.Should().Be("USD");
    }

    [Fact]
    public void Constructor_ShouldThrow_WhenAmountNegative()
    {
        var action = () => new Money(-1m, "USD");

        action.Should().Throw<ArgumentOutOfRangeException>();
    }

    [Fact]
    public void Add_ShouldThrow_WhenCurrenciesDoNotMatch()
    {
        var left = new Money(1m, "USD");
        var right = new Money(1m, "EUR");

        var action = () => left.Add(right);

        action.Should().Throw<InvalidOperationException>();
    }

    [Fact]
    public void Add_ShouldReturnNewMoney_WhenCurrenciesMatch()
    {
        var left = Money.From(10m, "usd");
        var right = Money.From(5.55m, "USD");

        var result = left.Add(right);

        result.Amount.Should().Be(15.55m);
        result.Currency.Should().Be("USD");
    }

    [Fact]
    public void Constructor_ShouldThrow_WhenCurrencyIsInvalid()
    {
        var action = () => new Money(1m, "US");

        action.Should().Throw<ArgumentException>();
    }
}
