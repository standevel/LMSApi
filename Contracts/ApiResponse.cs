namespace LMS.Api.Contracts;

public sealed record ApiError(string Code, string Message, string? Target = null);

public sealed class PaginationMeta
{
    public int PageNumber { get; set; }
    public int PageSize { get; set; }
    public long TotalCount { get; set; }
    public int TotalPages { get; set; }
    public bool HasPreviousPage { get; set; }
    public bool HasNextPage { get; set; }

    public PaginationMeta() { }

    public PaginationMeta(int pageNumber, int pageSize, long totalCount, int totalPages, bool hasPreviousPage, bool hasNextPage)
    {
        PageNumber = pageNumber;
        PageSize = pageSize;
        TotalCount = totalCount;
        TotalPages = totalPages;
        HasPreviousPage = hasPreviousPage;
        HasNextPage = hasNextPage;
    }
}

public sealed class ApiResponse<T>
{
    public bool Success { get; set; }
    public int Status { get; set; }
    public string Message { get; set; } = string.Empty;
    public T? Data { get; set; }
    public ApiError? Error { get; set; }
    public IReadOnlyList<ApiError>? Errors { get; set; }
    public PaginationMeta? Pagination { get; set; }

    public ApiResponse() { }

    public ApiResponse(
        bool success,
        int status,
        string message,
        T? data = default,
        ApiError? error = null,
        IReadOnlyList<ApiError>? errors = null,
        PaginationMeta? pagination = null)
    {
        Success = success;
        Status = status;
        Message = message;
        Data = data;
        Error = error;
        Errors = errors;
        Pagination = pagination;
    }

    public static ApiResponse<T> Ok(T data, string message = "Request successful", int status = StatusCodes.Status200OK) =>
        new(true, status, message, data);

    public static ApiResponse<T> Created(T data, string message = "Resource created", int status = StatusCodes.Status201Created) =>
        new(true, status, message, data);

    public static ApiResponse<T> Fail(
        string message,
        int status = StatusCodes.Status400BadRequest,
        ApiError? error = null,
        IReadOnlyList<ApiError>? errors = null) =>
        new(false, status, message, default, error, errors);

    public static ApiResponse<IReadOnlyList<TItem>> Paged<TItem>(
        IReadOnlyList<TItem> items,
        int pageNumber,
        int pageSize,
        long totalCount,
        string message = "Request successful",
        int status = StatusCodes.Status200OK)
    {
        var totalPages = totalCount <= 0 ? 0 : (int)Math.Ceiling(totalCount / (double)pageSize);
        var pagination = new PaginationMeta(
            pageNumber,
            pageSize,
            totalCount,
            totalPages,
            pageNumber > 1,
            pageNumber < totalPages);

        return new ApiResponse<IReadOnlyList<TItem>>(true, status, message, items, pagination: pagination);
    }
}
