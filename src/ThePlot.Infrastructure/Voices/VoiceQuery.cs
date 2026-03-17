using Microsoft.EntityFrameworkCore;
using ThePlot.Core.Voices;

namespace ThePlot.Infrastructure.Voices;

public sealed class VoiceQuery(ThePlotContext context) : IVoiceQuery
{
    private IQueryable<Voice> _query = context.Voices.AsNoTracking();

    public IQueryable<Voice> AsQueryable() => _query;
}
