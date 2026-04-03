using Microsoft.EntityFrameworkCore;
using ThePlot.Core.Characters;

namespace ThePlot.Infrastructure.Characters;

public sealed class CharacterQuery(ThePlotContext context) : ICharacterQuery
{
    private IQueryable<Character> _query = context.Characters.AsNoTracking();

    public IQueryable<Character> AsQueryable() => _query;

    public ICharacterQuery ByIds(IEnumerable<Guid> ids)
    {
        var list = ids.ToList();
        _query = _query.Where(c => list.Contains(c.Id));
        return this;
    }

    public ICharacterQuery ByVoiceIds(IEnumerable<Guid> voiceIds)
    {
        var ids = voiceIds.ToList();
        _query = _query.Where(c => c.VoiceId != null && ids.Contains(c.VoiceId.Value));
        return this;
    }

    public ICharacterQuery IncludeVoice()
    {
        _query = _query.Include(c => c.Voice);
        return this;
    }
}
