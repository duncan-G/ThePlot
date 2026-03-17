using ThePlot.Core.SceneElements;
using ThePlot.Database;
using ThePlot.Database.Abstractions;

namespace ThePlot.Infrastructure.SceneElements;

public sealed class SceneElementRepository(PagingTokenHelper pagingTokenHelper) :
    Repository<SceneElement, Guid>(pagingTokenHelper), ISceneElementRepository;
