using ThePlot.Database.Abstractions;

namespace ThePlot.Core.Scenes;

public interface ISceneQuery : IQuery<Scene>
{
    ISceneQuery ByScreenplayId(Guid screenplayId);
    ISceneQuery OrderByDateCreated();
}
