using Microsoft.Extensions.Hosting;
using LockNListen.Domain.Services;
using LockNListen.Infrastructure.Sound;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container
builder.Services.Configure<SoundClassifierOptions>(builder.Configuration.GetSection("SoundClassifier"));
builder.Services.AddSingleton<ISoundClassifier, OnnxSoundClassifier>();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

var app = builder.Build();

// Configure the HTTP request pipeline
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.MapClassifyEndpoints();

app.Run();
