using ThePlot.Core.Screenplays;
using ThePlot.Database;
using ThePlot.Database.Abstractions;

namespace ThePlot.Infrastructure.Screenplays;

public sealed class ScreenplayRepository(PagingTokenHelper pagingTokenHelper) :
    Repository<Screenplay, Guid>(pagingTokenHelper), IScreenplayRepository;
