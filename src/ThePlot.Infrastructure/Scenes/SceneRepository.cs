using ThePlot.Core.Scenes;
using ThePlot.Database;
using ThePlot.Database.Abstractions;

namespace ThePlot.Infrastructure.Scenes;

public sealed class SceneRepository(PagingTokenHelper pagingTokenHelper) :
    Repository<Scene, Guid>(pagingTokenHelper), ISceneRepository;
