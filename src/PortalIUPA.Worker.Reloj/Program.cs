using PortalIUPA.Worker.Reloj;
using PortalIUPA.Worker.Reloj.Device;

var builder = Host.CreateApplicationBuilder(args);
builder.Services.Configure<RelojWorkerOptions>(builder.Configuration.GetSection("Reloj"));
builder.Services.AddHostedService<Worker>();

var host = builder.Build();
host.Run();
