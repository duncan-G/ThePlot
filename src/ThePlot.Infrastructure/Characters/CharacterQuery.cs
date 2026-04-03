using Microsoft.EntityFrameworkCore;
using ThePlot.Core.Characters;
using ThePlot.Database.Abstractions;

namespace ThePlot.Infrastructure.Characters;

internal sealed class CharacterQuery(ThePlotContext context) : ICharacterQuery, IExecutableQuery<Character>
{
    private IQueryable<Character> _query = context.Characters.AsNoTracking();

    IQueryable<Character> IExecutableQuery<Character>.AsQueryable() => _query;

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
