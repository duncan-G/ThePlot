using Microsoft.EntityFrameworkCore;
using ThePlot.Core.Voices;

namespace ThePlot.Infrastructure.Voices;

public sealed class VoiceQuery(ThePlotContext context) : IVoiceQuery
{
    private IQueryable<Voice> _query = context.Voices.AsNoTracking();

    public IQueryable<Voice> AsQueryable() => _query;

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
