using Data.Agent;
using Data.Domain;
using Enterprise.Agency;
using Enterprise.MessageHub;
using Microsoft.AspNetCore.ResponseCompression;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddRazorPages();
builder.Services.AddServerSideBlazor();
builder.Services.AddSignalR();

var workplace = new Workplace(Addresses.Url) with
{
    AgentTypes = [typeof(Agent<Model, DataHub, IDataContract>)],
    HireAgentsPeriod = TimeSpan.FromMinutes(30),
    DecommissionerWaitingTime = TimeSpan.FromSeconds(10),
};

builder.Services.AddHostedService<Manager>()
                .AddSingleton(workplace);

builder.Services.AddResponseCompression(opts =>
{
    opts.MimeTypes = ResponseCompressionDefaults.MimeTypes.Concat(["application/octet-stream"]);
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
