using AirSolutions.Models.Assistant;

namespace AirSolutions.Services;

public interface IAssistantService
{
    Task<AssistantInterpretResponse> InterpretAsync(AssistantInterpretRequest request, CancellationToken cancellationToken = default);
}
