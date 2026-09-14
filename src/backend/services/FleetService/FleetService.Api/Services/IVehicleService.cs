using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using FleetService.Api.Dtos;
using FleetService.Api.Entities;

namespace FleetService.Api.Services;

public interface IVehicleService
{
    Task<PagedVehicleResult> GetPagedVehiclesAsync(VehicleQueryParameters parameters);

    Task<IEnumerable<VehicleResponse>> GetVehiclesAsync(
        string? category = null,
        VehicleStatus? status = null,
        string? fuel = null,
        string? searchTerm = null,
        int page = 1,
        int pageSize = 20,
        string? transmission = null,
        string? hub = null,
        string? sortBy = null,
        string? sortOrder = null);

    Task<VehicleResponse?> GetVehicleByIdAsync(Guid id);

    Task<VehicleResponse> CreateVehicleAsync(CreateVehicleRequest request);

    Task<VehicleResponse> UpdateVehicleAsync(Guid id, UpdateVehicleRequest request);

    Task<VehicleResponse> UpdateVehicleStatusAsync(Guid id, UpdateVehicleStatusRequest request);

    Task<IEnumerable<VehicleCategoryResponse>> GetCategoriesAsync();
}
