namespace RAAMS.Application.Authorization;

public record WorkflowTransitionResult(bool Allowed, string? Reason = null);
