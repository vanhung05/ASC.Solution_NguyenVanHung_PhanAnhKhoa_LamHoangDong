using ASC.Model.Models;

namespace ASC.Business.Interfaces
{
    public interface IServiceRequestOperations
    {
        Task CreateServiceRequestAsync(ServiceRequest request);

        ServiceRequest UpdateServiceRequest(ServiceRequest request);

        Task<ServiceRequest> UpdateServiceRequestStatusAsync(
            string rowKey,
            string partitionKey,
            string status);

        Task<List<ServiceRequest>> GetServiceRequestsByRequestedDateAndStatus(DateTime? requestedDate,
            List<string> status = null, string email = "", string serviceEngineerEmail = "");
    }
}