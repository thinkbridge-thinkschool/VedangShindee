Test Class - 

using FluentAssertions;
using QuotesApi.Models;
using Xunit;

namespace QuotesApi.Tests.Domain;

public class CollectionTests
{
    [Fact]
    public void Create_EmptyName_Throws()
    {
        var act = () => Collection.Create("", "owner1");

        act.Should().Throw<ArgumentException>()
           .WithMessage("*between 3 and 80*");
    }

    [Fact]
    public void Create_NameExceeds80Chars_Throws()
    {
        var longName = new string('x', 81);

        var act = () => Collection.Create(longName, "owner1");

        act.Should().Throw<ArgumentException>()
           .WithMessage("*between 3 and 80*");
    }

    [Fact]
    public void AddItem_51stItem_Throws()
    {
        var collection = Collection.Create("My List", "owner1");
        for (var i = 1; i <= 50; i++) collection.AddItem(i);

        var act = () => collection.AddItem(51);

        act.Should().Throw<InvalidOperationException>()
           .WithMessage("*more than 50 items*");
    }

    [Fact]
    public void AddItem_DuplicateQuoteId_Throws()
    {
        var collection = Collection.Create("My List", "owner1");
        collection.AddItem(42);

        var act = () => collection.AddItem(42);

        act.Should().Throw<InvalidOperationException>()
           .WithMessage("*already in this collection*");
    }

    [Fact]
    public void RemoveItem_NonExistentQuote_Throws()
    {
        var collection = Collection.Create("My List", "owner1");

        var act = () => collection.RemoveItem(99);

        act.Should().Throw<InvalidOperationException>()
           .WithMessage("*not in this collection*");
    }

    [Fact]
    public void AddThenRemove_LeavesZeroItems()
    {
        var collection = Collection.Create("My List", "owner1");
        collection.AddItem(7);

        collection.RemoveItem(7);

        collection.Items.Should().BeEmpty();
    }
}


--------------------------------------------------------------------------



Test output (from the run above):

dotnet test
Restore complete (1.7s)
  QuotesApi net10.0 succeeded (5.0s) → D:\Vedang\thinkschool\VedangShindee\day-2\piece3\QuotesApi\bin\Debug\net10.0\QuotesApi.dll
  QuotesApi.Tests.Domain net10.0 succeeded (1.8s) → bin\Debug\net10.0\QuotesApi.Tests.Domain.dll
[xUnit.net 00:00:00.01] xUnit.net VSTest Adapter v2.8.2+699d445a1a (64-bit .NET 10.0.8)
  QuotesApi.Tests.Domain net10.0 succeeded (1.8s) → bin\Debug\net10.0\QuotesApi.Tests.Domain.dll
[xUnit.net 00:00:00.01] xUnit.net VSTest Adapter v2.8.2+699d445a1a (64-bit .NET 10.0.8)
[xUnit.net 00:00:00.32]   Discovering: QuotesApi.Tests.Domain
[xUnit.net 00:00:00.54]   Discovered:  QuotesApi.Tests.Domain
[xUnit.net 00:00:00.55]   Starting:    QuotesApi.Tests.Domain
[xUnit.net 00:00:01.57]   Finished:    QuotesApi.Tests.Domain
  QuotesApi.Tests.Domain test net10.0 succeeded (5.4s)

Test summary: total: 6, failed: 0, succeeded: 6, skipped: 0, duration: 5.4s
  QuotesApi.Tests.Domain test net10.0 succeeded (5.4s)

Test summary: total: 6, failed: 0, succeeded: 6, skipped: 0, duration: 5.4s
Test summary: total: 6, failed: 0, succeeded: 6, skipped: 0, duration: 5.4s
Build succeeded in 16.4s
