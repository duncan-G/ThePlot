using ThePlot.Database.Abstractions;

namespace ThePlot.Core.ContentGeneration;

public interface IGenerationAttemptRepository : IRepository<GenerationAttempt, Guid>;
