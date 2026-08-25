using Emerald.CoreX.Runtime;
using Microsoft.AspNetCore.Http;

namespace Emerald.ApiHost;

public partial class Program
{
    private static IResult GetSessionLogs(
        string gamePath,
        int? page,
        int? pageSize,
        string? level,
        IGameRuntimeService runtime)
    {
        var session = runtime.FindLatestSession(gamePath);
        if (session == null) return Results.NotFound();

        var entries = session.Entries.AsEnumerable();
        if (!string.IsNullOrEmpty(level) && level != "All")
            entries = entries.Where(e => e.LevelText.Equals(level, StringComparison.OrdinalIgnoreCase));

        var size = Math.Clamp(pageSize ?? 100, 1, 500);
        var p = Math.Max(page ?? 1, 1);
        var paged = entries.Skip((p - 1) * size).Take(size);

        return Results.Ok(new
        {
            GamePath = session.GamePath,
            TotalEntries = session.Entries.Count,
            Page = p,
            PageSize = size,
            Entries = paged.Select(e => new
            {
                e.Timestamp,
                Level = e.LevelText,
                Source = e.Source.ToString(),
                e.Message,
                e.DetailsText,
                e.ThreadName,
                e.LoggerName
            })
        });
    }
}
