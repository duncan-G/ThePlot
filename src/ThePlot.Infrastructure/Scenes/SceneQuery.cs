using Microsoft.EntityFrameworkCore;
using ThePlot.Core.Scenes;
using ThePlot.Database.Abstractions;

namespace ThePlot.Infrastructure.Scenes;

internal sealed class SceneQuery(ThePlotContext context) : ISceneQuery, IExecutableQuery<Scene>
{
    private IQueryable<Scene> _query = context.Scenes.AsNoTracking();

    IQueryable<Scene> IExecutableQuery<Scene>.AsQueryable() => _query;

    public ISceneQuery ByScreenplayId(Guid screenplayId)
    {
        _query = _query.Where(s => s.ScreenplayId == screenplayId);
        return this;
    }

    public ISceneQuery OrderByDateCreated()
    {
        _query = _query.OrderBy(s => s.DateCreated);
        return this;
    }
}
