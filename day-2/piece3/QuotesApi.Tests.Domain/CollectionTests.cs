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
