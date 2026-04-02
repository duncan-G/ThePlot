using Microsoft.EntityFrameworkCore;
using ThePlot.Core.Characters;

namespace ThePlot.Infrastructure.Characters;

public sealed class CharacterQuery(ThePlotContext context) : ICharacterQuery
{
    private IQueryable<Character> _query = context.Characters.AsNoTracking();

    public IQueryable<Character> AsQueryable() => _query;

    public ICharacterQuery ByVoiceIds(IEnumerable<Guid> voiceIds)
    {
        var ids = voiceIds.ToList();
        _query = _query.Where(c => c.VoiceId != null && ids.Contains(c.VoiceId.Value));
        return this;
    }
}
