using FastEndpoints;
 
namespace LMS.Api.Endpoints.BulkOperations;
 
public class BulkOperationsGroup : Group
{
    public BulkOperationsGroup()
    {
        // Empty prefix — individual endpoints define their own routes
        Configure("", ep => { });
    }
}
