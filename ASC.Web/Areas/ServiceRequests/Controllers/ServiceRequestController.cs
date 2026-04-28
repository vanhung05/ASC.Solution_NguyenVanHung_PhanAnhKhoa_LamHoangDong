using ASC.Business.Interfaces;
using ASC.Model.Models;
using ASC.Utilities;
using ASC.Web.Areas.ServiceRequests.Models;
using ASC.Web.Controllers;
using ASC.Web.Services;
using AutoMapper;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ASC.Web.Areas.ServiceRequests.Controllers
{
    [Area("ServiceRequests")]
    [Authorize(Roles = "User")]
    public class ServiceRequestController : BaseController
    {
        private readonly IServiceRequestOperations _serviceRequestOperations;
        private readonly IMapper _mapper;
        private readonly IMasterDataCacheOperations _masterDataCacheOperations;
        private readonly IMasterDataOperations _masterDataOperations;

        public ServiceRequestController(
            IServiceRequestOperations serviceRequestOperations,
            IMapper mapper,
            IMasterDataCacheOperations masterDataCacheOperations,
            IMasterDataOperations masterDataOperations)
        {
            _serviceRequestOperations = serviceRequestOperations;
            _mapper = mapper;
            _masterDataCacheOperations = masterDataCacheOperations;
            _masterDataOperations = masterDataOperations;
        }

        [HttpGet]
        public async Task<IActionResult> ServiceRequest()
        {
            await LoadMasterDataToViewBag();

            return View(new NewServiceRequestViewModel());
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ServiceRequest(NewServiceRequestViewModel request)
        {
            if (!ModelState.IsValid)
            {
                await LoadMasterDataToViewBag();
                return View(request);
            }

            var currentUser = HttpContext.User.GetCurrentUserDetails();

            var serviceRequest = _mapper.Map<ServiceRequest>(request);

            serviceRequest.PartitionKey = currentUser.Email;
            serviceRequest.RowKey = Guid.NewGuid().ToString();
            serviceRequest.Status = "New";
            serviceRequest.CreatedBy = currentUser.Email;
            serviceRequest.UpdatedBy = currentUser.Email;
            serviceRequest.CreatedDate = DateTime.UtcNow;
            serviceRequest.UpdatedDate = DateTime.UtcNow;

            await _serviceRequestOperations.CreateServiceRequestAsync(serviceRequest);

            return RedirectToAction("Dashboard", "Dashboard", new { area = "ServiceRequests" });
        }

        private async Task LoadMasterDataToViewBag()
        {
            var vehicleTypes = new List<MasterDataValue>();
            var vehicleNames = new List<MasterDataValue>();

            try
            {
                // Try cache first
                var masterData = await _masterDataCacheOperations.GetMasterDataCacheAsync();

                if (masterData != null && masterData.MasterDataValues != null && masterData.MasterDataValues.Any())
                {
                    vehicleTypes = masterData.MasterDataValues
                        .Where(p => p.PartitionKey == "VehicleType" && p.IsActive)
                        .ToList();

                    vehicleNames = masterData.MasterDataValues
                        .Where(p => p.PartitionKey == "VehicleName" && p.IsActive)
                        .ToList();
                }
            }
            catch
            {
                // Cache (Redis) unavailable, ignore
            }

            // Fallback: load directly from DB if cache returned nothing
            if (!vehicleTypes.Any() || !vehicleNames.Any())
            {
                try
                {
                    var allValues = await _masterDataOperations.GetAllMasterValuesAsync();

                    if (allValues != null)
                    {
                        if (!vehicleTypes.Any())
                        {
                            vehicleTypes = allValues
                                .Where(p => p.PartitionKey == "VehicleType" && p.IsActive)
                                .ToList();
                        }

                        if (!vehicleNames.Any())
                        {
                            vehicleNames = allValues
                                .Where(p => p.PartitionKey == "VehicleName" && p.IsActive)
                                .ToList();
                        }
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Error loading master data from DB: {ex.Message}");
                }
            }

            ViewBag.VehicleTypes = vehicleTypes;
            ViewBag.VehicleNames = vehicleNames;
        }
    }
}