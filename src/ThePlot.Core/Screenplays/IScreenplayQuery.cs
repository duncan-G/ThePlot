using ThePlot.Database.Abstractions;

namespace ThePlot.Core.Screenplays;

public interface IScreenplayQuery : IQuery<Screenplay>
{
    IScreenplayQuery ById(Guid id);
}
