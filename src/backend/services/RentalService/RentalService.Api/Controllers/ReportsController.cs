using FleetFlow.Contracts;
using FleetService.Api.Dtos;
using FleetService.Api.Entities;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using RentalService.Api.Data;
using RentalService.Api.Services;
namespace RentalService.Api.Controllers;
[ApiController][Route("internal/reports")][Authorize(Policy=ServiceScopes.RentalReportsRead)][ServiceFilter(typeof(RentalActivationFilter))]
public sealed class ReportsController(RentalDbContext db,TimeProvider clock):ControllerBase
{
 [HttpGet("bookings")][ProducesResponseType(typeof(BookingReportResponse),200)]public async Task<IActionResult> Bookings([FromQuery]BookingReportQuery query,CancellationToken ct)
 {
  if((long)(query.Page-1)*query.PageSize>int.MaxValue)return BadRequest();
  var rows=db.Bookings.AsNoTracking().AsQueryable();if(query.Status.HasValue)rows=rows.Where(b=>b.Status==query.Status.Value);
  var count=await rows.CountAsync(ct);var page=await rows.OrderByDescending(b=>b.CreatedAt).ThenBy(b=>b.Id).Skip((query.Page-1)*query.PageSize).Take(query.PageSize).ToListAsync(ct);
  var result=page.Select(b=>{var vehicle=BookingCoordinator.Map(b).Vehicle;return new BookingReportRow(b.Id,b.CustomerId,b.VehicleId,vehicle?.LicensePlate,vehicle?.Make,vehicle?.Model,b.Status,b.StartDateTime,b.EndDateTime,b.TotalCost,b.CreatedAt,b.UpdatedAt);}).ToList();
  return Ok(new BookingReportResponse(clock.GetUtcNow().UtcDateTime,result,count,query.Page,query.PageSize,(int)Math.Ceiling(count/(decimal)query.PageSize)));
 }
 [HttpGet("statistics")][ProducesResponseType(typeof(RentalStatisticsResponse),200)]public async Task<IActionResult> Statistics(CancellationToken ct)
 {
  var now=clock.GetUtcNow().UtcDateTime;
  var groups=await db.Bookings.AsNoTracking().GroupBy(b=>b.Status).Select(g=>new{Status=g.Key,Count=g.Count(),Value=g.Sum(b=>b.TotalCost)}).ToListAsync(ct);
  int Count(BookingStatus status)=>groups.FirstOrDefault(g=>g.Status==status)?.Count??0;
  var stats=new BookingStatistics(groups.Sum(g=>g.Count),Count(BookingStatus.Pending),Count(BookingStatus.Confirmed),Count(BookingStatus.Cancelled),Count(BookingStatus.Completed),Count(BookingStatus.Pending)+Count(BookingStatus.Confirmed),groups.Where(g=>g.Status!=BookingStatus.Cancelled).Sum(g=>g.Value));
  var ids=await db.Bookings.AsNoTracking().Where(b=>b.Status==BookingStatus.Confirmed&&(b.StartDateTime<=now&&b.EndDateTime>now||db.Pickups.Any(p=>p.BookingId==b.Id&&p.State==RentalLifecycleState.Active))).Select(b=>b.VehicleId).Distinct().ToListAsync(ct);
  return Ok(new RentalStatisticsResponse(stats,ids));
 }
}
