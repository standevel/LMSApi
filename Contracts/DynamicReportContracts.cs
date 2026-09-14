using System;
using System.Collections.Generic;

namespace LMS.Api.Contracts;

// ==================== METADATA CONTRACTS ====================

public record ReportFieldOptionDto(string Label, string Value);

public record ReportFieldMetadataDto(
    string Id,
    string TableId,
    string DisplayName,
    string DataType, // "string" | "number" | "currency" | "date" | "boolean" | "badge"
    bool IsFilterable,
    bool IsSortable,
    List<string> FilterOperators,
    List<ReportFieldOptionDto>? Options = null
);

public record ReportTableMetadataDto(
    string Id,
    string DisplayName,
    string? Description,
    List<ReportFieldMetadataDto> Fields
);

public record ReportDatasetMetadataDto(
    string Id,
    string DisplayName,
    string Description,
    string Icon,
    string Category,
    List<string> DefaultSelectedFields,
    List<ReportTableMetadataDto> Tables
);

// ==================== QUERY & EXECUTION CONTRACTS ====================

public record ReportFilterRuleDto(
    string FieldId,
    string Operator, // "equals" | "notEquals" | "contains" | "startsWith" | "greaterThan" | "lessThan" | "greaterThanOrEqual" | "lessThanOrEqual" | "between" | "in" | "isNull" | "isNotNull"
    string? Value = null,
    string? ValueTo = null
);

public record ReportSortRuleDto(
    string FieldId,
    bool Descending = false
);

public record DynamicReportQueryRequest(
    string DatasetId,
    List<string> SelectedFields,
    List<ReportFilterRuleDto>? Filters = null,
    List<ReportSortRuleDto>? Sorts = null,
    int Page = 1,
    int PageSize = 50,
    bool ExportAll = false,
    string? SearchTerm = null
);

public record ReportColumnHeaderDto(
    string FieldId,
    string Label,
    string Table,
    string DataType
);

public record DynamicReportResultDto(
    string DatasetId,
    List<ReportColumnHeaderDto> Columns,
    List<Dictionary<string, object?>> Rows,
    int TotalCount,
    int Page,
    int PageSize,
    int TotalPages,
    Dictionary<string, decimal> NumericAggregates,
    long ExecutionTimeMs
);

public record ExportDynamicReportRequest(
    DynamicReportQueryRequest Query,
    string Format = "xlsx", // "xlsx" | "csv"
    string? Title = null
);

// ==================== TEMPLATE CONTRACTS ====================

public record DynamicReportTemplateDto(
    Guid Id,
    string Name,
    string Description,
    string DatasetId,
    List<string> SelectedFields,
    List<ReportFilterRuleDto> Filters,
    List<ReportSortRuleDto> Sorts,
    string CreatedBy,
    DateTime CreatedAt,
    bool IsSystemPreset
);

public record SaveReportTemplateRequest(
    string Name,
    string Description,
    string DatasetId,
    List<string> SelectedFields,
    List<ReportFilterRuleDto>? Filters = null,
    List<ReportSortRuleDto>? Sorts = null,
    bool IsShared = false
);
