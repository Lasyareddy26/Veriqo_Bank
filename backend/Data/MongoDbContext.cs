using Veriqo.Api.Models;
using Veriqo.Api.Settings;
using Microsoft.Extensions.Options;
using MongoDB.Driver;

namespace Veriqo.Api.Data;

public class MongoDbContext
{
    public IMongoDatabase Database { get; }

    public MongoDbContext(IOptions<MongoSettings> options)
    {
        var client = new MongoClient(options.Value.ConnectionString);
        Database = client.GetDatabase(options.Value.Database);
    }

    public IMongoCollection<ApplicationUser> Users => Database.GetCollection<ApplicationUser>("users");
    public IMongoCollection<CustomerProfile> Profiles => Database.GetCollection<CustomerProfile>("profiles");
    public IMongoCollection<Product> Products => Database.GetCollection<Product>("products");
    public IMongoCollection<Application> Applications => Database.GetCollection<Application>("applications");
    public IMongoCollection<KycDocument> Documents => Database.GetCollection<KycDocument>("documents");
    public IMongoCollection<Notification> Notifications => Database.GetCollection<Notification>("notifications");
    public IMongoCollection<AuditLog> AuditLogs => Database.GetCollection<AuditLog>("audit_logs");
    public IMongoCollection<Ticket> Tickets => Database.GetCollection<Ticket>("tickets");
    public IMongoCollection<RegistryRecord> Registry => Database.GetCollection<RegistryRecord>("registry");
    public IMongoCollection<KnowledgeArticle> Knowledge => Database.GetCollection<KnowledgeArticle>("knowledge");
}
