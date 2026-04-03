using ThePlot.Database.Abstractions;

namespace ThePlot.Core.SceneElements;

public interface ISceneElementQuery : IQuery<SceneElement>
{
    ISceneElementQuery BySceneIds(IEnumerable<Guid> sceneIds);
    ISceneElementQuery OrderBySequenceOrder();
}
