using System.Text.Json;
using KiwiTraffic.Core.Settings;
using KiwiTraffic.Infrastructure.Storage;

namespace KiwiTraffic.Infrastructure.Configuration;

/// <summary>Stores settings as JSON in the application data directory.</summary>
public sealed class JsonSettingsStore : ISettingsStore
{
    private readonly AppPaths _paths;

    public JsonSettingsStore(AppPaths paths)
    {
        ArgumentNullException.ThrowIfNull(paths);
        _paths = paths;
    }

    public async Task<AppSettings?> LoadAsync(CancellationToken cancellationToken = default)
    {
        var path = _paths.SettingsFile;
        var text = await AtomicFile.ReadAsync(path, cancellationToken).ConfigureAwait(false);
        if (text is null)
        {
            return null;
        }

        AppSettings? settings = null;
        string? problem = null;

        try
        {
            settings = JsonSerializer.Deserialize<AppSettings>(text, JsonStoreFormat.Options);
            if (settings is null)
            {
                problem = "文件内容为空或不是 JSON 对象。";
            }
        }
        catch (JsonException ex)
        {
            problem = $"JSON 解析失败：{ex.Message}";
        }

        if (problem is null && settings!.SchemaVersion > AppSettings.CurrentSchemaVersion)
        {
            problem = $"配置由更新版本的程序写入（schemaVersion={settings.SchemaVersion}，" +
                      $"当前支持 {AppSettings.CurrentSchemaVersion}）。";
        }

        if (problem is null)
        {
            var errors = SettingsValidator.Validate(settings!);
            if (errors.Count > 0)
            {
                problem = "配置内容不合法：" + string.Join(' ', errors);
            }
        }

        if (problem is not null)
        {
            var backup = AtomicFile.MoveToBackup(path);
            throw new CorruptStoreException(
                path,
                backup,
                $"settings.json 无法使用：{problem} 原文件已备份为 {backup}，请重新配置。");
        }

        return settings;
    }

    public async Task SaveAsync(AppSettings settings, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(settings);

        var json = JsonSerializer.Serialize(settings, JsonStoreFormat.Options);
        await AtomicFile.WriteAsync(_paths.SettingsFile, json, cancellationToken).ConfigureAwait(false);
    }
}
