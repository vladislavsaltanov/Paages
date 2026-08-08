using Microsoft.JSInterop;
using Paages.Web.Services;

namespace Paages.Tests.Services;

public class TabsStateTests
{
    private readonly TabsState _sut = new(null!);

    private static readonly Guid A = Guid.NewGuid();
    private static readonly Guid B = Guid.NewGuid();
    private static readonly Guid C = Guid.NewGuid();

    [Fact]
    public void Open_NewTab_AddsAndActivatesIt()
    {
        _sut.Open(A);

        Assert.Equal([A], _sut.OpenTabsIds);
        Assert.Equal(A, _sut.ActiveTabId);
    }

    [Fact]
    public void Open_AlreadyOpenTab_DoesNotDuplicateJustActivates()
    {
        _sut.Open(A);
        _sut.Open(B);

        _sut.Open(A);

        Assert.Equal([A, B], _sut.OpenTabsIds);
        Assert.Equal(A, _sut.ActiveTabId);
    }

    [Fact]
    public void OpenBackground_NewTab_AddsWithoutChangingActiveTab()
    {
        _sut.Open(A);

        _sut.OpenBackground(B);

        Assert.Equal([A, B], _sut.OpenTabsIds);
        Assert.Equal(A, _sut.ActiveTabId);
    }

    [Fact]
    public void OpenBackground_AlreadyOpenTab_DoesNotDuplicate()
    {
        _sut.Open(A);
        _sut.Open(B);

        _sut.OpenBackground(A);

        Assert.Equal([A, B], _sut.OpenTabsIds);
        Assert.Equal(B, _sut.ActiveTabId);
    }

    [Fact]
    public void Close_TabNotOpen_ReturnsCurrentActiveTabUnchanged()
    {
        _sut.Open(A);
        _sut.Open(B);

        var result = _sut.Close(Guid.NewGuid());

        Assert.Equal(B, result);
        Assert.Equal([A, B], _sut.OpenTabsIds);
    }

    [Fact]
    public void Close_TabNotOpen_DoesNotFireOnTabsChanged()
    {
        _sut.Open(A);
        var fired = false;
        _sut.OnTabsChanged += () => fired = true;

        _sut.Close(Guid.NewGuid());

        Assert.False(fired);
    }

    [Fact]
    public void Close_NonActiveTab_RemovesItButKeepsActiveTabId()
    {
        _sut.Open(A);
        _sut.Open(B);
        _sut.Open(C);

        _sut.Close(A);

        Assert.Equal(C, _sut.ActiveTabId);
        Assert.Equal([B, C], _sut.OpenTabsIds);
    }

    [Fact]
    public void Close_ActiveTabNotLast_ActivatesTabAtSameIndex()
    {
        _sut.Open(A);
        _sut.Open(B);
        _sut.Open(C);
        _sut.Open(B);

        var result = _sut.Close(B);

        Assert.Equal(C, result);
        Assert.Equal([A, C], _sut.OpenTabsIds);
    }

    [Fact]
    public void Close_ActiveTabIsLast_ActivatesPreviousTab()
    {
        _sut.Open(A);
        _sut.Open(B);
        _sut.Open(C);

        var result = _sut.Close(C);

        Assert.Equal(B, result);
        Assert.Equal([A, B], _sut.OpenTabsIds);
    }

    [Fact]
    public void Close_LastRemainingTab_SetsActiveTabIdNull()
    {
        _sut.Open(A);

        var result = _sut.Close(A);

        Assert.Null(result);
        Assert.Empty(_sut.OpenTabsIds);
    }

    [Fact]
    public void Close_ActiveTab_FiresOnTabsChanged()
    {
        _sut.Open(A);
        var fired = false;
        _sut.OnTabsChanged += () => fired = true;

        _sut.Close(A);

        Assert.True(fired);
    }
}