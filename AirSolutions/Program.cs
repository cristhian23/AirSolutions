var builder = WebApplication.CreateBuilder(args);

var app = builder.Build();

app.UseHttpsRedirection();

// Primero DefaultFiles (para que "/" pida index.html), luego StaticFiles para servirla
app.UseDefaultFiles();
app.UseStaticFiles();

app.UseRouting();

app.MapFallbackToFile("index.html");

app.Run();
