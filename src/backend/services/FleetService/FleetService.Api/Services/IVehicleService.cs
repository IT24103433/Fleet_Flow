using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using FleetService.Api.Dtos;
using FleetService.Api.Entities;

namespace FleetService.Api.Services;

public interface IVehicleService
{
    Task<IEnumerable<VehicleResponse>> GetVehiclesAsync(
        string? category = null,
        VehicleStatus? status = null,
        string? fuel = null,
        string? searchTerm = null,
        int page = 1,
        int pageSize = 20);

    Task<VehicleResponse?> GetVehicleByIdAsync(Guid id);

    Task<VehicleResponse> CreateVehicleAsync(CreateVehicleRequest request);

    Task<VehicleResponse> UpdateVehicleAsync(Guid id, UpdateVehicleRequest request);

    Task<IEnumerable<VehicleCategoryResponse>> GetCategoriesAsync();
}
