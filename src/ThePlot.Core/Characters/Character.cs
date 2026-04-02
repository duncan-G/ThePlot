using ThePlot.Core.Voices;

namespace ThePlot.Core.Characters;

public sealed class Character
{
    private Character()
    {
    }

    public Guid Id { get; private init; }
    public string Name { get; private init; } = null!;
    public IReadOnlyList<string> Aliases { get; private init; } = [];
    public Guid? VoiceId { get; private set; }

    public Voice? Voice { get; private init; }

    public static Character Create(string name, string[]? aliases = null, Guid? voiceId = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);

        return new Character
        {
            Id = Guid.CreateVersion7(),
            Name = name.Trim(),
            Aliases = aliases ?? [],
            VoiceId = voiceId
        };
    }

    public void AssignVoice(Guid? voiceId) => VoiceId = voiceId;
}
