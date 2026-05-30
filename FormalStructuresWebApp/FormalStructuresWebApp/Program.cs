using FormalStructuresWebApp.Services.AI;
using FormalStructuresWebApp.Services.Automaton;
using FormalStructuresWebApp.Services.Interfaces;
using FormalStructuresWebApp.Services.LStar;
using FormalStructuresWebApp.Services.Pda;
using FormalStructuresWebApp.Services.Session;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllersWithViews();
builder.Services.AddHttpContextAccessor(); 
builder.Services.AddSession();             
builder.Services.AddDistributedMemoryCache(); 

builder.Services.AddScoped<IAiGenerationService, AiGenerationService>();
builder.Services.AddScoped<IAutomatonValidationService, AutomatonValidationService>();
builder.Services.AddScoped<IAutomatonAnalysisService, AutomatonAnalysisService>();
builder.Services.AddSingleton<IAutomatonSessionService, AutomatonSessionService>();
builder.Services.AddScoped<IAutomatonEditorService, AutomatonEditorService>();
builder.Services.AddScoped<IPdaSessionService, PdaSessionService>();
builder.Services.AddScoped<IPdaEditorService, PdaEditorService>();
builder.Services.AddHttpClient<IOllamaService, OllamaService>(client =>
{
    client.Timeout = TimeSpan.FromMinutes(10);
});
builder.Services.AddScoped<LStarService>();


var app = builder.Build();

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseStaticFiles();

app.UseSession();
app.UseRouting();

app.UseAuthorization();

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}");

app.Run();