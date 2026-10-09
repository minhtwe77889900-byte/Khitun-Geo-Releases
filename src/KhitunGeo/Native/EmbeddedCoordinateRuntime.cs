using System.Threading;
using Jint;

namespace KhitunGeo.Native;

// Browser-free host for the opt-in native workspace; mathematics remains JavaScript.
// No CLR exposure, browser services, worker, timer or shared engine.
internal sealed class EmbeddedCoordinateRuntime(string core, string adapter)
{
    internal Engine CreateEngine(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var engine = new Engine(options => options
            .LimitMemory(128_000_000)
            .MaxStatements(100_000_000)
            .TimeoutInterval(TimeSpan.FromSeconds(30))
            .CancellationToken(cancellationToken));
        engine.Execute(core);
        engine.Execute(adapter);
        engine.Execute("function khitunConvertJson(request) { return KhitunCoordinateAdapter.convertJson(request); }");
        return engine;
    }

    public string Convert(string request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (request.Length > 8_000_000) throw new ArgumentException("Слишком большой запрос.", nameof(request));
        try
        {
            var engine = CreateEngine(cancellationToken);
            // JSON is a function argument, never executable source text.
            var response = engine.Invoke("khitunConvertJson", request).AsString();
            cancellationToken.ThrowIfCancellationRequested();
            return response;
        }
        catch (Jint.Runtime.ExecutionCanceledException) when (cancellationToken.IsCancellationRequested)
        { throw new OperationCanceledException(cancellationToken); }
    }
}
