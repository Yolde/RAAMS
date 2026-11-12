namespace RAAMS.Application.Common;

public record PaginatedResult<T>(IReadOnlyList<T> Items, int TotalCount, int Page, int PageSize)
{
    public int TotalPages => (TotalCount + PageSize - 1) / PageSize;
}
