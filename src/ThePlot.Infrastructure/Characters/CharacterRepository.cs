using ThePlot.Core.Characters;
using ThePlot.Database;
using ThePlot.Database.Abstractions;

namespace ThePlot.Infrastructure.Characters;

public sealed class CharacterRepository(PagingTokenHelper pagingTokenHelper) :
    Repository<Character, Guid>(pagingTokenHelper), ICharacterRepository;
