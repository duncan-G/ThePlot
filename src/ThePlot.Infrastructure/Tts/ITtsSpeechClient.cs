namespace ThePlot.Infrastructure.Tts;

public interface ITtsSpeechClient
{
    Task<TtsSpeechResult> GetSpeechAsync(string userText, CancellationToken cancellationToken = default);
}

public sealed record TtsSpeechResult(string Text, string? AudioBase64, string AudioFormat);
