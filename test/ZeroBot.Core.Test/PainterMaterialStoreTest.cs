using Microsoft.Extensions.Logging.Abstractions;
using ZeroBot.Painter;

namespace ZeroBot.Core.Test;

public class PainterMaterialStoreTest : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), $"painter-material-test-{Guid.NewGuid():N}");
    private readonly PainterMaterialStore _store;

    public PainterMaterialStoreTest()
    {
        _store = new PainterMaterialStore(NullLogger<PainterMaterialStore>.Instance, _root);
    }

    public void Dispose()
    {
        if (Directory.Exists(_root)) Directory.Delete(_root, recursive: true);
    }

    [Fact]
    public void Hash_ShouldBeDeterministicHex16()
    {
        var hash = PainterMaterialStore.Hash("猫");
        Assert.Equal(16, hash.Length);
        Assert.Equal(hash, PainterMaterialStore.Hash("猫"));
        Assert.NotEqual(hash, PainterMaterialStore.Hash("狗"));
        Assert.All(hash, c => Assert.Contains(c, "0123456789abcdef"));
    }

    [Theory]
    [InlineData("猫", true)]
    [InlineData("my cat-1", true)]
    [InlineData("", false)]
    [InlineData("   ", false)]
    [InlineData("a:cat", false)]
    [InlineData("a：cat", false)]
    [InlineData("{cat}", false)]
    [InlineData("a\nb", false)]
    public void IsValidName_ShouldValidate(string name, bool expected)
    {
        Assert.Equal(expected, PainterMaterialStore.IsValidName(name, out _));
    }

    [Fact]
    public void IsValidName_TooLong_ShouldFail()
    {
        Assert.False(PainterMaterialStore.IsValidName(new string('a', PainterMaterialStore.MaxNameLength + 1), out var error));
        Assert.NotNull(error);
    }

    [Fact]
    public async Task Add_Find_List_ShouldWork()
    {
        await _store.AddAsync(100, "猫", [(new byte[] { 1, 2 }, "png")]);
        await _store.AddAsync(100, "狗", [(new byte[] { 3 }, "jpg"), (new byte[] { 4 }, "png")]);

        Assert.NotNull(_store.Find(100, "猫"));
        Assert.Null(_store.Find(100, "鸟"));

        var materials = _store.List(100);
        Assert.Equal(new[] { "狗", "猫" }, materials.Select(m => m.Name).OrderBy(n => n, StringComparer.Ordinal));
        Assert.Equal(2, materials.Single(m => m.Name == "狗").Files.Count);
    }

    [Fact]
    public async Task Add_SameName_ShouldOverwrite()
    {
        await _store.AddAsync(100, "猫", [(new byte[] { 1 }, "png"), (new byte[] { 2 }, "png")]);
        await _store.AddAsync(100, "猫", [(new byte[] { 9 }, "jpg")]);

        var images = await _store.LoadImagesAsync(100, "猫");
        Assert.Single(images);
        Assert.Equal(new byte[] { 9 }, images[0]);
    }

    [Fact]
    public async Task LoadImages_ShouldKeepWriteOrder()
    {
        await _store.AddAsync(100, "猫", [(new byte[] { 1 }, "png"), (new byte[] { 2 }, "jpg")]);

        var images = await _store.LoadImagesAsync(100, "猫");
        Assert.Equal(2, images.Count);
        Assert.Equal(new byte[] { 1 }, images[0]);
        Assert.Equal(new byte[] { 2 }, images[1]);
    }

    [Fact]
    public async Task Delete_ShouldRemoveMaterial()
    {
        await _store.AddAsync(100, "猫", [(new byte[] { 1 }, "png")]);

        Assert.True(await _store.DeleteAsync(100, "猫"));
        Assert.False(await _store.DeleteAsync(100, "猫"));
        Assert.Null(_store.Find(100, "猫"));
        Assert.Empty(_store.List(100));
    }

    [Fact]
    public async Task Materials_ShouldBeIsolatedPerGroup()
    {
        await _store.AddAsync(100, "猫", [(new byte[] { 1 }, "png")]);

        Assert.Null(_store.Find(200, "猫"));
        Assert.Empty(_store.List(200));
    }

    [Fact]
    public void List_WithoutDirectory_ShouldReturnEmpty()
    {
        Assert.Empty(_store.List(999));
        Assert.Null(_store.Find(999, "猫"));
    }
}
