
using AIHousingAssistant.Application.Services;
using AIHousingAssistant.Application.Services.Chunk;
using AIHousingAssistant.Application.Services.Embedding;
using AIHousingAssistant.Application.Services.Interfaces;
using AIHousingAssistant.Application.Services.VectorDb;
using AIHousingAssistant.Application.Services.VectorStores;
using AIHousingAssistant.Helper;
using AIHousingAssistant.Infrastructure.Data;
using AIHousingAssistant.Models.Settings;
using Microsoft.EntityFrameworkCore;
using Microsoft.SemanticKernel;
using Qdrant.Client;
using System;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllersWithViews();

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();
builder.Services.Configure<ProviderSettings>(
    builder.Configuration.GetSection("ProviderSettings"));//builder.Services.AddScoped<IHousingService, HousingService>();
//builder.Services.AddScoped<HousingSkill>();
//builder.Services.AddScoped<IChatHistoryService, ChatHistoryService>();
builder.Services.AddScoped<IHousingService, HousingService>();
builder.Services.AddScoped<ISummarizerService, SummarizerService>();
builder.Services.AddSingleton<IChatHistoryService, ChatHistoryService>();
builder.Services.AddScoped<IRagService, RagService>();

builder.Services.AddScoped<IChunkService, ChunkService>();
builder.Services.AddScoped<IEmbeddingService, EmbeddingService>();

builder.Services.AddHttpClient<HttpClientHelper>();

// concrete types
//builder.Services.AddScoped<IInMemoryVectorStore,InMemoryVectorStore>();
//builder.Services.AddScoped<IQDrantVectorStore,QDrantVectorStore_Sdk>();
//builder.Services.AddScoped<IQDrantVectorStoreEF, QDrantVectorStore_SK>();


builder.Services.AddScoped<IVectorDB_Resolver, VectorDB_Resolver>();
builder.Services.AddScoped<IVectorDB, QdrantVectorDb_Http>();
//builder.Services.AddScoped<IVectorDB, QdrantVectorDb_InMemory>();
builder.Services.AddScoped<IVectorDB, QdrantVectorDb_Sdk>();
builder.Services.AddScoped<IVectorStore, VectorStore>();


//builder.Services.AddQdrantVectorStore("localhost"); // Register Qdrant Vector Store


builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowAllOrigins",
        builder =>
        {
            builder.AllowAnyOrigin()
                   .AllowAnyMethod()
                   .AllowAnyHeader();
        });
});



// Add DB Context
builder.Services.AddDbContext<HousingDbContext>(options =>
    options.UseSqlServer(builder.Configuration.GetConnectionString("HousingChatBotDB")));

// Register the HttpContextAccessor service
builder.Services.AddHttpContextAccessor();
builder.Services.AddSession(options =>
{
    options.IdleTimeout = TimeSpan.FromMinutes(30);  // Set session timeout
    options.Cookie.HttpOnly = true;  // Prevent JavaScript access
    options.Cookie.IsEssential = true;  // Required for GDPR compliance
    options.Cookie.SecurePolicy = CookieSecurePolicy.None;//for http
                                                          //  options.Cookie.SecurePolicy = CookieSecurePolicy.Always; for https

});
var app = builder.Build();

app.UseSwagger();
app.UseSwaggerUI();

app.UseStaticFiles();

app.UseRouting();
app.UseSession();
//app.UseAuthorization();


//app.MapControllerRoute(
//    name: "default",
//    pattern: "{controller=Home}/{action=Index}/{id?}");
app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Chat}/{action=Index}/{id?}");

app.UseCors("AllowAllOrigins");

app.Run();
