using Microsoft.EntityFrameworkCore;
using ThePlot.Core.Voices;
using ThePlot.Database.Abstractions;

namespace ThePlot.Infrastructure.Voices;

internal sealed class VoiceQuery(ThePlotContext context) : IVoiceQuery, IExecutableQuery<Voice>
{
    private IQueryable<Voice> _query = context.Voices.AsNoTracking();

    IQueryable<Voice> IExecutableQuery<Voice>.AsQueryable() => _query;

    public IVoiceQuery ByScreenplayId(Guid screenplayId)
    {
        _query = _query.Where(v => v.ScreenplayId == screenplayId);
        return this;
    }

    public IVoiceQuery ByRole(VoiceRole role)
    {
        _query = _query.Where(v => v.Role == role);
        return this;
    }
}
