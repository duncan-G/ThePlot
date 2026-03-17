using Microsoft.EntityFrameworkCore;
using ThePlot.Core.SceneElements;

namespace ThePlot.Infrastructure.SceneElements;

public sealed class SceneElementQuery(ThePlotContext context) : ISceneElementQuery
{
    private IQueryable<SceneElement> _query = context.SceneElements.AsNoTracking();

    public IQueryable<SceneElement> AsQueryable() => _query;

    public ISceneElementQuery BySceneIds(IEnumerable<Guid> sceneIds)
    {
        var ids = sceneIds.ToList();
        _query = _query.Where(e => ids.Contains(e.SceneId));
        return this;
    }
}
