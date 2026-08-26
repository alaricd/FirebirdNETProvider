using System.Data.Common;
using System.Threading.Tasks;
using FirebirdSql.Data.FirebirdClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using NUnit.Framework;
using pengdows.crud.enums;
using pengdows.crud.fakeDb;

namespace FirebirdSql.EntityFrameworkCore.Firebird.Tests.Storage;

// FbRelationalConnection.CreateDbConnection() is the exact method f579c933 ("Fix ADO.NET provider
// substitutionality") changed from `new FbConnection(ConnectionString)` to using the injected
// DbProviderFactory instead -- but every other fakeDb test in this project configures EF via
// UseFirebird(DbConnection instance) rather than UseFirebird(connectionString), which never calls
// CreateDbConnection() at all (EF only calls it when it owns connection creation itself, i.e. the
// connection-string path). This is the one place that path needs exercising: a substituted
// DbProviderFactory registered ahead of AddEntityFrameworkFirebird()'s TryAddSingleton (so ours
// wins) via UseInternalServiceProvider, proving CreateDbConnection() actually goes through it
// instead of constructing a real FbConnection.
public class FbRelationalConnectionFakeDbTests
{
	[Test]
	public async Task CreateDbConnection_uses_the_substituted_DbProviderFactory_for_the_connection_string_path()
	{
		var fakeFactory = new fakeDbFactory(SupportedDatabase.Firebird);

		var services = new ServiceCollection();
		services.AddSingleton<DbProviderFactory>(fakeFactory);
		services.AddEntityFrameworkFirebird();
		await using var serviceProvider = services.BuildServiceProvider();

		var optionsBuilder = new DbContextOptionsBuilder<ProbeDbContext>()
			.UseInternalServiceProvider(serviceProvider)
			.UseFirebird("server=fake;database=fake;user=fake;password=fake;");

		await using var db = new ProbeDbContext(optionsBuilder.Options);

		var connection = db.Database.GetDbConnection();

		Assert.That(connection, Is.InstanceOf<fakeDbConnection>());
		Assert.That(connection, Is.Not.InstanceOf<FbConnection>());
		Assert.That(connection.ConnectionString, Is.EqualTo("server=fake;database=fake;user=fake;password=fake;"));
	}

	sealed class ProbeDbContext(DbContextOptions<ProbeDbContext> options) : DbContext(options);
}
