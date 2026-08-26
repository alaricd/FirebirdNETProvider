using System.Data.Common;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using NUnit.Framework;
using pengdows.crud.enums;
using pengdows.crud.fakeDb;

namespace FirebirdSql.EntityFrameworkCore.Firebird.Tests.Architecture;

[TestFixture]
public class FakeDbProviderFactoryTests
{
	[Test]
	public void EfCore_uses_a_substituted_factory_connection_without_opening_a_Firebird_connection()
	{
		var factory = new fakeDbFactory(SupportedDatabase.Firebird);
		var services = new ServiceCollection().AddSingleton<DbProviderFactory>(factory);
		services.AddEntityFrameworkFirebird();

		using var serviceProvider = services.BuildServiceProvider();
		var options = new DbContextOptionsBuilder<SubstitutionContext>()
			.UseInternalServiceProvider(serviceProvider)
			.UseFirebird("database=fake")
			.Options;

		using var context = new SubstitutionContext(options);
		var connection = context.Database.GetDbConnection();

		Assert.That(connection, Is.TypeOf<fakeDbConnection>());
		Assert.That(connection.ConnectionString, Is.EqualTo("database=fake"));
	}

	[Test]
	public void EfCore_keeps_the_substituted_factory_when_registering_provider_services()
	{
		var factory = new fakeDbFactory(SupportedDatabase.Firebird);
		var services = new ServiceCollection().AddSingleton<DbProviderFactory>(factory);
		services.AddEntityFrameworkFirebird();

		using var serviceProvider = services.BuildServiceProvider();

		Assert.That(serviceProvider.GetRequiredService<DbProviderFactory>(), Is.SameAs(factory));
	}

	[Test]
	public void EfCore_executes_a_query_and_binds_parameters_through_the_injected_fakeDb_factory()
	{
		var factory = new fakeDbFactory(SupportedDatabase.Firebird);
		var connection = new CapturingFakeDbConnection();
		connection.EnqueueReaderResult(new[] { new Dictionary<string, object> { ["Id"] = 7 } });
		factory.Connections.Add(connection);

		var services = new ServiceCollection().AddSingleton<DbProviderFactory>(factory);
		services.AddEntityFrameworkFirebird();
		using var serviceProvider = services.BuildServiceProvider();
		var options = new DbContextOptionsBuilder<SubstitutionContext>()
			.UseInternalServiceProvider(serviceProvider)
			.UseFirebird("database=fake")
			.Options;

		using var context = new SubstitutionContext(options);
		var name = "Ada";
		var ids = context.Customers.Where(customer => customer.Name == name).Select(customer => customer.Id).ToList();

		Assert.That(ids, Is.EqualTo(new[] { 7 }));
		var command = connection.ExecutedReaderCommand;
		Assert.That(command.Parameters, Has.Some.Matches<CapturedParameter>(parameter => parameter.Value is string value && value == name));
	}

	[Test]
	public async Task EfCore_executes_an_async_query_through_the_injected_fakeDb_factory()
	{
		var factory = new fakeDbFactory(SupportedDatabase.Firebird);
		var connection = new CapturingFakeDbConnection();
		connection.EnqueueReaderResult(new[] { new Dictionary<string, object> { ["Id"] = 8 } });
		factory.Connections.Add(connection);
		using var serviceProvider = CreateServiceProvider(factory);
		using var context = CreateContext(serviceProvider);

		var ids = await context.Customers.Select(customer => customer.Id).ToListAsync();

		Assert.That(ids, Is.EqualTo(new[] { 8 }));
		Assert.That(connection.ExecutedReaderCommand.CommandText, Does.Contain("SELECT"));
	}

	[Test]
	public async Task EfCore_executes_raw_sql_and_binds_a_parameter_through_fakeDb()
	{
		var factory = new fakeDbFactory(SupportedDatabase.Firebird);
		var connection = new CapturingFakeDbConnection();
		connection.EnqueueNonQueryResult(1);
		factory.Connections.Add(connection);
		using var serviceProvider = CreateServiceProvider(factory);
		using var context = CreateContext(serviceProvider);

		var affected = await context.Database.ExecuteSqlRawAsync("UPDATE CUSTOMERS SET NAME = {0}", "Ada");

		Assert.That(affected, Is.EqualTo(1));
		Assert.That(connection.ExecutedNonQueryCommand.Parameters, Has.Some.Matches<CapturedParameter>(parameter => parameter.Value is string value && value == "Ada"));
	}

	[Test]
	public void EfCore_opens_and_closes_a_factory_created_fakeDb_connection()
	{
		var factory = new fakeDbFactory(SupportedDatabase.Firebird);
		var connection = new CapturingFakeDbConnection();
		factory.Connections.Add(connection);
		using var serviceProvider = CreateServiceProvider(factory);
		using var context = CreateContext(serviceProvider);

		context.Database.OpenConnection();
		Assert.That(connection.State, Is.EqualTo(System.Data.ConnectionState.Open));

		context.Database.CloseConnection();
		Assert.That(connection.State, Is.EqualTo(System.Data.ConnectionState.Closed));
	}

	[Test]
	public void EfCore_starts_and_commits_a_transaction_on_a_factory_created_fakeDb_connection()
	{
		var factory = new fakeDbFactory(SupportedDatabase.Firebird);
		var connection = new CapturingFakeDbConnection();
		factory.Connections.Add(connection);
		using var serviceProvider = CreateServiceProvider(factory);
		using var context = CreateContext(serviceProvider);

		using var transaction = context.Database.BeginTransaction();
		Assert.That(connection.State, Is.EqualTo(System.Data.ConnectionState.Open));
		transaction.Commit();
	}

	private static ServiceProvider CreateServiceProvider(DbProviderFactory factory)
	{
		var services = new ServiceCollection().AddSingleton<DbProviderFactory>(factory);
		services.AddEntityFrameworkFirebird();
		return services.BuildServiceProvider();
	}

	private static SubstitutionContext CreateContext(ServiceProvider serviceProvider)
	{
		var options = new DbContextOptionsBuilder<SubstitutionContext>()
			.UseInternalServiceProvider(serviceProvider)
			.UseFirebird("database=fake")
			.Options;
		return new SubstitutionContext(options);
	}

	private sealed class SubstitutionContext(DbContextOptions<SubstitutionContext> options) : DbContext(options)
	{
		public DbSet<Customer> Customers => Set<Customer>();
	}

	private sealed class Customer
	{
		public int Id { get; set; }
		public string Name { get; set; } = string.Empty;
	}

	private sealed class CapturingFakeDbConnection : fakeDbConnection
	{
		public CapturedCommand ExecutedReaderCommand { get; private set; }
		public CapturedCommand ExecutedNonQueryCommand { get; private set; }

		protected override DbCommand CreateDbCommand() => new CapturingFakeDbCommand(this);

		private sealed class CapturingFakeDbCommand(CapturingFakeDbConnection connection) : fakeDbCommand(connection)
		{
			protected override DbDataReader ExecuteDbDataReader(System.Data.CommandBehavior behavior)
			{
				connection.ExecutedReaderCommand = new CapturedCommand(
					CommandText,
					Parameters.Cast<DbParameter>().Select(parameter => new CapturedParameter(parameter.ParameterName, parameter.Value)).ToList());
				return base.ExecuteDbDataReader(behavior);
			}

			public override int ExecuteNonQuery()
			{
				connection.ExecutedNonQueryCommand = Capture();
				return base.ExecuteNonQuery();
			}

			private CapturedCommand Capture()
				=> new(
					CommandText,
					Parameters.Cast<DbParameter>().Select(parameter => new CapturedParameter(parameter.ParameterName, parameter.Value)).ToList());
		}
	}

	private sealed record CapturedCommand(string CommandText, IReadOnlyList<CapturedParameter> Parameters);
	private sealed record CapturedParameter(string Name, object Value);
}
