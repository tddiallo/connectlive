using Hangfire;
using Hangfire.PostgreSql;
using ConnectLive.Application.Messaging;
using ConnectLive.Notification.Api.Extensions;
using ConnectLive.Notification.Api;

var builder = MyCustomWebHosting.CreateWebHostBuilder<ClassInfo>(args);

// Add services to the container.

builder.Services.AddControllers();
// Learn more about configuring Swagger/OpenAPI at https://aka.ms/aspnetcore/swashbuckle
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

builder.Services.AddHangfire(x => x.UsePostgreSqlStorage(options => options.UseNpgsqlConnection(builder.Configuration.GetConnectionString("ConnectLiveContext"))));
builder.Services.AddHangfireServer(options => options.Queues = ["email"]);

builder.Services.AddConnectLiveMessaging(builder.Configuration, consume: true);

var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();

app.UseAuthorization();

app.MapControllers();

app.Run();
