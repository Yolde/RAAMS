namespace RAAMS.Application.Assessments;

public sealed record EscalationEmailFile(string FileName, string ContentType, byte[] Data);
