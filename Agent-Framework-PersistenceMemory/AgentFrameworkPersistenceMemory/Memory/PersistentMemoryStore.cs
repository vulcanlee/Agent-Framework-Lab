using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;

namespace AgentFrameworkPersistenceMemory.Memory;

public sealed class PersistentMemoryStore
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = true,
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping
    };

    private readonly string _filePath;

    public PersistentMemoryStore(string filePath)
    {
        _filePath = filePath;
    }

    public async Task<IReadOnlyList<PersistentMemoryRecord>> LoadAsync(CancellationToken cancellationToken)
    {
        if (!File.Exists(_filePath))
        {
            return [];
        }

        await using var stream = File.OpenRead(_filePath);
        var records = await JsonSerializer.DeserializeAsync<List<PersistentMemoryRecord>>(stream, JsonOptions, cancellationToken);
        return records ?? [];
    }

    public async Task SaveAsync(IEnumerable<PersistentMemoryRecord> records, CancellationToken cancellationToken)
    {
        var directory = Path.GetDirectoryName(_filePath);
        if (!string.IsNullOrWhiteSpace(directory))
        {
            Directory.CreateDirectory(directory);
        }

        var json = JsonSerializer.Serialize(records, JsonOptions);
        await File.WriteAllTextAsync(_filePath, json, new UTF8Encoding(encoderShouldEmitUTF8Identifier: false), cancellationToken);
    }

    public async Task UpsertAsync(PersistentMemoryRecord record, CancellationToken cancellationToken)
    {
        var records = (await LoadAsync(cancellationToken)).ToList();
        var index = records.FindIndex(existing => string.Equals(existing.Id, record.Id, StringComparison.OrdinalIgnoreCase));

        if (index >= 0)
        {
            records[index] = record;
        }
        else
        {
            records.Add(record);
        }

        await SaveAsync(records, cancellationToken);
    }
}
