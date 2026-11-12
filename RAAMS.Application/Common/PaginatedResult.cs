using System.Collections;

namespace RAAMS.Application.Common;

public interface IPaginatedResult
{
    IEnumerable Items { get; }
}

public record PaginatedResult<T>(IReadOnlyList<T> Items, int TotalCount, int Page, int PageSize) : IPaginatedResult
{
    IEnumerable IPaginatedResult.Items => Items;
    public int TotalPages => (TotalCount + PageSize - 1) / PageSize;
}
