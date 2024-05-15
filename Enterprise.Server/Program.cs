using Data.Agent;
using Data.Domain;
using Enterprise.Agency;
using Enterprise.MessageHub;
using Microsoft.AspNetCore.ResponseCompression;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddRazorPages();
builder.Services.AddServerSideBlazor();
builder.Services.AddSignalR();

builder.Services.AddAgencyManager("https://localhost:7158", o => o
    .WithAgentTypes([typeof(Agent<Model, DataHub, IDataContract>)])
    .WithHireAgentsPeriod(TimeSpan.FromMinutes(30))
    .WithOnBoardingWaitingTime(TimeSpan.FromSeconds(1))
    .WithOffBoardingWaitingTime(TimeSpan.FromSeconds(1)));

builder.Services.AddResponseCompression(o =>
{
    o.MimeTypes = ResponseCompressionDefaults.MimeTypes.Concat(["application/octet-stream"]);
});

var app = builder.Build();

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error");
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseStaticFiles();
app.UseRouting();
app.MapBlazorHub();

app.MapHub<PostingHub>(Addresses.SignalR);

app.MapFallbackToPage("/_Host");

await app.RunAsync();
