using FormalStructuresWebApp.Services;
using FormalStructuresWebApp.Services.Interfaces;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllersWithViews();

builder.Services.AddScoped<IAiGenerationService, AiGenerationService>();
builder.Services.AddScoped<IAutomatonValidationService, AutomatonValidationService>();
builder.Services.AddScoped<IAutomatonAnalysisService, AutomatonAnalysisService>();

var app = builder.Build();

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseStaticFiles();

app.UseRouting();

app.UseAuthorization();

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}");

app.Run();