namespace RAAMS.Application.Common.Exceptions;

public class WorkflowViolationException : Exception
{
    public WorkflowViolationException(string message) : base(message)
    {
    }
}
