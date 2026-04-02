using ThePlot.Database.Abstractions;

namespace ThePlot.Core.Voices;

public interface IVoiceQuery : IQuery<Voice>
{
    IVoiceQuery ByScreenplayId(Guid screenplayId);

    IVoiceQuery ByRole(VoiceRole role);
}
