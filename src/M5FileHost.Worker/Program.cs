using M5FileHost.Infrastructure;
using M5FileHost.Worker;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

var builder = Host.CreateApplicationBuilder(args);
builder.Services.AddM5Infrastructure(builder.Configuration);
builder.Services.AddHostedService<ProcessingWorker>();
await builder.Build().RunAsync();
