using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PortalIUPA.Application.UseCases.Dashboard;

namespace PortalIUPA.Api.Controllers;

[Authorize]
[Route("api/dashboard")]
public sealed class DashboardController : ApiControllerBase
{
    private readonly IMediator _mediator;

    public DashboardController(IMediator mediator) => _mediator = mediator;

    [HttpGet("empleado")]
    public async Task<IActionResult> Empleado() =>
        Ok(await _mediator.Send(new GetDashboardEmpleadoQuery(EmpleadoId)));

    [Authorize(Policy = "Responsable")]
    [HttpGet("empleador")]
    public async Task<IActionResult> Empleador() =>
        Ok(await _mediator.Send(new GetDashboardEmpleadorQuery(EmpleadoId)));
}