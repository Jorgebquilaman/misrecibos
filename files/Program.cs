using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using ZkAttendanceService;
using ZkAttendanceService.Data;
using ZkAttendanceService.Options;

var builder = Host.CreateApplicationBuilder(args);

var deviceOptions = new ZkDeviceOptions();
builder.Configuration.GetSection("ZkDevice").Bind(deviceOptions);
builder.Services.AddSingleton(deviceOptions);

builder.Services.AddSingleton(sp => new AttendanceRepository(
    deviceOptions.SqlConnectionString,
    sp.GetRequiredService<ILogger<AttendanceRepository>>()));

builder.Services.AddHostedService<Worker>();

var host = builder.Build();
host.Run();
