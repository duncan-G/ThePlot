using Microsoft.EntityFrameworkCore;
using ThePlot.Core.Scenes;

namespace ThePlot.Infrastructure.Scenes;

public sealed class SceneQuery(ThePlotContext context) : ISceneQuery
{
    private IQueryable<Scene> _query = context.Scenes.AsNoTracking();

    public IQueryable<Scene> AsQueryable() => _query;

    public ISceneQuery ByScreenplayId(Guid screenplayId)
    {
        _query = _query.Where(s => s.ScreenplayId == screenplayId);
        return this;
    }
}
