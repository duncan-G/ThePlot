using ThePlot.Core.Voices;
using ThePlot.Database;
using ThePlot.Database.Abstractions;

namespace ThePlot.Infrastructure.Voices;

public sealed class VoiceRepository(PagingTokenHelper pagingTokenHelper) :
    Repository<Voice, Guid>(pagingTokenHelper), IVoiceRepository;
