using ThePlot.Database.Abstractions;

namespace ThePlot.Core.Characters;

public interface ICharacterQuery : IQuery<Character>
{
    ICharacterQuery ByVoiceIds(IEnumerable<Guid> voiceIds);
}
