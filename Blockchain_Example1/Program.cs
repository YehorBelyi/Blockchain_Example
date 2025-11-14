using Blockchain_Example1.Services;

var builder = WebApplication.CreateBuilder(args);



// Add services to the container.
builder.Services.AddControllersWithViews();
builder.Services.AddBlockchainSerivce(builder.Configuration.GetConnectionString("Blockchain"));

var app = builder.Build();
var blockchain = app.Services.GetRequiredService<BlockchainService>();
await blockchain.InitializeAsync();

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
}
app.UseRouting();

app.UseAuthorization();

app.MapStaticAssets();

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Blockchain}/{action=Index}/{id?}")
    .WithStaticAssets();


app.Run();
