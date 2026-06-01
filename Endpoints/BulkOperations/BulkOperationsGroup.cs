using FastEndpoints;

namespace LMS.Api.Endpoints.BulkOperations;

public class BulkOperationsGroup : Group
{
    public BulkOperationsGroup()
    {
        Configure("bulk-operations", ep =>
        {
            // No specific configuration needed for now
        });
    }
}