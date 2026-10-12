using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Logging;

namespace ZeroBot.Painter;

/// <summary>
/// 公共素材：一个名称对应磁盘上的一个目录，目录内为若干按顺序编号的图片文件。
/// </summary>
/// <param name="Name">素材名（用户书写原文）。</param>
/// <param name="Files">图片文件名（写入顺序）。</param>
public sealed record PainterMaterial(string Name, IReadOnlyList<string> Files);

/// <summary>
/// 群公共素材库，落盘于**工作目录**下：
/// <code>
/// painter/{群uid}/{name_hash}/material.json   # {"Name":"猫","Files":["0.png","1.jpg"]}
/// painter/{群uid}/{name_hash}/0.png
/// painter/{群uid}/{name_hash}/1.jpg
/// </code>
/// 目录名取名称的 SHA-256 前 16 位十六进制，避免名称含路径分隔符 / 空白等非法字符；
/// 名称原文存于 <c>material.json</c>，用于列出与按名查找。
/// 增删会在进程内串行（<see cref="_ioLock"/>），并使用临时目录 + 原子替换实现覆盖。
/// </summary>
public class PainterMaterialStore
{
    /// <summary>素材名最大长度。</summary>
    public const int MaxNameLength = 32;

    private const string MetadataFileName = "material.json";

    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };

    private readonly ILogger<PainterMaterialStore> _logger;
    private readonly string _root;
    private readonly SemaphoreSlim _ioLock = new(1, 1);

    public PainterMaterialStore(ILogger<PainterMaterialStore> logger)
        : this(logger, Path.Combine(Environment.CurrentDirectory, "painter"))
    {
    }

    public PainterMaterialStore(ILogger<PainterMaterialStore> logger, string root)
    {
        _logger = logger;
        _root = root;
    }

    /// <summary>名称 → 目录名：SHA-256 前 16 位十六进制（小写）。</summary>
    public static string Hash(string name) =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(name)))[..16].ToLowerInvariant();

    /// <summary>
    /// 素材名合法性：非空、长度不超过 <see cref="MaxNameLength"/>、不含分隔符（<c>:</c> / <c>：</c>）、
    /// 花括号（与提示词 <c>{name}</c> 引用语法冲突）或控制字符。
    /// </summary>
    public static bool IsValidName(string name, out string? error)
    {
        error = null;

        if (string.IsNullOrWhiteSpace(name))
        {
            error = "名称不能为空。";
            return false;
        }

        if (name.Length > MaxNameLength)
        {
            error = $"名称不能超过 {MaxNameLength} 个字符。";
            return false;
        }

        if (name.Any(c => c is ':' or '：' or '{' or '}' || char.IsControl(c)))
        {
            error = "名称不能包含冒号、花括号或换行。";
            return false;
        }

        return true;
    }

    /// <summary>本群全部素材（按名称排序）；目录缺失或元数据损坏的条目跳过。</summary>
    public IReadOnlyList<PainterMaterial> List(long peerId)
    {
        var directory = GroupDirectory(peerId);
        if (!Directory.Exists(directory)) return [];

        var materials = new List<PainterMaterial>();
        foreach (var materialDirectory in Directory.EnumerateDirectories(directory))
        {
            if (ReadMetadata(materialDirectory) is { } material) materials.Add(material);
        }

        return materials.OrderBy(m => m.Name, StringComparer.Ordinal).ToList();
    }

    /// <summary>按名称查找素材；不存在或元数据损坏返回 null。</summary>
    public PainterMaterial? Find(long peerId, string name) => ReadMetadata(MaterialDirectory(peerId, name));

    /// <summary>读取素材的全部图片字节（按写入顺序）；文件缺失或读取失败时跳过该文件并记日志。</summary>
    public async ValueTask<IReadOnlyList<byte[]>> LoadImagesAsync(long peerId, string name,
        CancellationToken cancellationToken = default)
    {
        if (Find(peerId, name) is not { } material) return [];

        var directory = MaterialDirectory(peerId, name);
        var images = new List<byte[]>(material.Files.Count);
        foreach (var file in material.Files)
        {
            // 防御手改元数据造成的路径穿越。
            if (Path.GetFileName(file) != file) continue;

            var path = Path.Combine(directory, file);
            try
            {
                if (File.Exists(path)) images.Add(await File.ReadAllBytesAsync(path, cancellationToken));
            }
            catch (Exception e)
            {
                _logger.LogWarning(e, "读取公共素材图片失败：peer={PeerId} name={Name} file={File}",
                    peerId, name, file);
            }
        }

        return images;
    }

    /// <summary>
    /// 写入素材（同名覆盖）：先写临时目录，成功后再原子替换旧目录，避免写入中途留下半成品。
    /// </summary>
    public async ValueTask AddAsync(long peerId, string name,
        IReadOnlyList<(byte[] Bytes, string Extension)> images, CancellationToken cancellationToken = default)
    {
        var target = MaterialDirectory(peerId, name);
        var staging = $"{target}.tmp";

        await _ioLock.WaitAsync(cancellationToken);
        try
        {
            if (Directory.Exists(staging)) Directory.Delete(staging, recursive: true);
            Directory.CreateDirectory(staging);

            var files = new List<string>(images.Count);
            for (var i = 0; i < images.Count; i++)
            {
                var fileName = $"{i}.{images[i].Extension}";
                await File.WriteAllBytesAsync(Path.Combine(staging, fileName), images[i].Bytes, cancellationToken);
                files.Add(fileName);
            }

            var metadata = JsonSerializer.Serialize(new MaterialMetadata(name, files), JsonOptions);
            await File.WriteAllTextAsync(Path.Combine(staging, MetadataFileName), metadata, cancellationToken);

            if (Directory.Exists(target)) Directory.Delete(target, recursive: true);
            Directory.Move(staging, target);
        }
        finally
        {
            _ioLock.Release();
        }
    }

    /// <summary>删除素材；不存在返回 false。</summary>
    public async ValueTask<bool> DeleteAsync(long peerId, string name,
        CancellationToken cancellationToken = default)
    {
        var target = MaterialDirectory(peerId, name);

        await _ioLock.WaitAsync(cancellationToken);
        try
        {
            if (!Directory.Exists(target)) return false;
            Directory.Delete(target, recursive: true);
            return true;
        }
        finally
        {
            _ioLock.Release();
        }
    }

    private string GroupDirectory(long peerId) => Path.Combine(_root, peerId.ToString());

    private string MaterialDirectory(long peerId, string name) => Path.Combine(GroupDirectory(peerId), Hash(name));

    private PainterMaterial? ReadMetadata(string directory)
    {
        try
        {
            var path = Path.Combine(directory, MetadataFileName);
            if (!File.Exists(path)) return null;

            var metadata = JsonSerializer.Deserialize<MaterialMetadata>(File.ReadAllText(path));
            return string.IsNullOrWhiteSpace(metadata?.Name)
                ? null
                : new PainterMaterial(metadata.Name, metadata.Files ?? []);
        }
        catch (Exception e)
        {
            _logger.LogWarning(e, "读取公共素材元数据失败：{Directory}", directory);
            return null;
        }
    }

    private sealed record MaterialMetadata(string Name, List<string>? Files);
}
