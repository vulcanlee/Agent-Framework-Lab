using System.Text;
using AgentFrameworkPersistenceMemory;

Console.InputEncoding = new UTF8Encoding(false);
Console.OutputEncoding = new UTF8Encoding(false);

var cancellationSource = new CancellationTokenSource();
Console.CancelKeyPress += (_, eventArgs) =>
{
    eventArgs.Cancel = true;
    cancellationSource.Cancel();
};

try
{
    var app = AppBootstrapper.Create(Console.In, Console.Out);
    await app.RunAsync(cancellationSource.Token);
}
catch (Exception ex)
{
    await Console.Out.WriteLineAsync($"啟動失敗：{ex.Message}");
}
