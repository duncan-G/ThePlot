using Microsoft.EntityFrameworkCore;
using ThePlot.Core.Characters;

namespace ThePlot.Infrastructure.Characters;

public sealed class CharacterQuery(ThePlotContext context) : ICharacterQuery
{
    private IQueryable<Character> _query = context.Characters.AsNoTracking();

    public IQueryable<Character> AsQueryable() => _query;
}
