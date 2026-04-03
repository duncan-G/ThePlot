using Microsoft.EntityFrameworkCore;
using ThePlot.Core.SceneElements;
using ThePlot.Database.Abstractions;

namespace ThePlot.Infrastructure.SceneElements;

internal sealed class SceneElementQuery(ThePlotContext context) : ISceneElementQuery, IExecutableQuery<SceneElement>
{
    private IQueryable<SceneElement> _query = context.SceneElements.AsNoTracking();

    IQueryable<SceneElement> IExecutableQuery<SceneElement>.AsQueryable() => _query;

    public ISceneElementQuery BySceneIds(IEnumerable<Guid> sceneIds)
    {
        var ids = sceneIds.ToList();
        _query = _query.Where(e => ids.Contains(e.SceneId));
        return this;
    }

    public ISceneElementQuery OrderBySequenceOrder()
    {
        _query = _query.OrderBy(e => e.SequenceOrder);
        return this;
    }
}
