using System.Linq.Expressions;
using ThePlot.Database.Abstractions;

namespace ThePlot.Core.Screenplays;

public sealed record ScreenplayPagingToken(PagingCursor<DateTime, Guid>? Cursor, int PageSize)
    : PagingToken<Screenplay, DateTime, Guid>(Cursor, PageSize)
{
    public override Expression<Func<Screenplay, DateTime>> SortKeySelector => s => s.DateCreated;
    public override Expression<Func<Screenplay, Guid>> PrimaryKeySelector => s => s.Id;
}
