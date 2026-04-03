using ThePlot.Database.Abstractions;

namespace ThePlot.Core.Characters;

public interface ICharacterQuery : IQuery<Character>
{
    ICharacterQuery ByIds(IEnumerable<Guid> ids);
    ICharacterQuery ByVoiceIds(IEnumerable<Guid> voiceIds);
    ICharacterQuery IncludeVoice();
}
