using Data.Agent;
using Data.Domain;
using Enterprise.Agency;
using Enterprise.MessageHub;
using Microsoft.AspNetCore.ResponseCompression;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddRazorPages();
builder.Services.AddServerSideBlazor();
builder.Services.AddSignalR();

builder.Services.AddHostedService<Manager>();

builder.Services.AddSingleton(new[] { typeof(Agent<Model, DataHub, IDataContract>) });

builder.Services.AddResponseCompression(opts =>
{
    opts.MimeTypes = ResponseCompressionDefaults.MimeTypes.Concat(
        new[] { "application/octet-stream" });
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

app.MapHub<ServerHub>(Addresses.SignalR);

app.MapFallbackToPage("/_Host");

await app.RunAsync();
