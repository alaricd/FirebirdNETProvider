using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using FirebirdSql.Data.FirebirdClient;
using Microsoft.EntityFrameworkCore;
using NUnit.Framework;
using pengdows.crud.enums;
using pengdows.crud.fakeDb;

namespace FirebirdSql.EntityFrameworkCore.Firebird.Tests.StormGate;

// Tier 2 of pengdows.crud's two-tier compatibility model (see EfProviders.cs and
// EfProviderDeepTests.cs in pengdows.crud/pengdows.stormgate.EntityFrameworkCore.MultiProvider.Tests):
// real SQL generation, real parameter binding, and SaveChanges round-tripping against fakeDb, with
// zero real Firebird server involved.
//
// Before this branch's f579c933 ("Fix ADO.NET provider substitutionality"), FbStringTypeMapping.
// ConfigureParameter cast every string parameter to the concrete FbParameter type, so ANY
// string-valued parameter -- a WHERE clause or a written column -- threw InvalidCastException
// against a non-FbParameter instance. pengdows.crud's own multi-provider compatibility suite
// documents this as a confirmed, permanent Tier-2 exclusion for Firebird (tested against the
// published FirebirdSql.EntityFrameworkCore.Firebird 11.0.0 NuGet package) -- see
// EfProviderDeepTests.Firebird_CannotBindAnyStringParameter_BecauseItsProviderCastsToItsConcreteType.
// These tests prove that fix actually resolves it for this provider.
public class FakeDbEfInteropTests
{
	[Test]
	public void FakeDb_factory_creates_a_generic_connection()
	{
		var factory = new fakeDbFactory("Sqlite");
		using var connection = factory.CreateConnection();

		Assert.That(connection, Is.Not.Null);
		Assert.That(connection, Is.Not.TypeOf<FbConnection>());
	}

	[Test]
	public async Task String_valued_where_clause_parameters_bind_correctly_against_a_fake_connection()
	{
		var factory = new fakeDbFactory(SupportedDatabase.Firebird);
		var connection = (fakeDbConnection)factory.CreateConnection()!;

		var options = new DbContextOptionsBuilder<CustomerContext>()
			.UseFirebird(connection)
			.Options;
		await using var db = new CustomerContext(options);

		// Column order must match the physical position EF's shaper reads by (fixed at query
		// compile time from the generated SQL's SELECT list -- alphabetical by property name for
		// this entity), not just the column names: Id, IsActive, Name.
		connection.EnqueueReaderResult(new[]
		{
			new Dictionary<string, object> { ["Id"] = 1, ["IsActive"] = true, ["Name"] = "Ada" }
		});

		// Real LINQ-to-SQL translation, including local-variable string parameterization -- the
		// exact shape that used to throw InvalidCastException: FbParameter.
		var name = "Ada";
		var results = await db.Customers.Where(c => c.Name == name).ToListAsync();

		Assert.That(results, Has.Count.EqualTo(1));
		Assert.That(results[0].Name, Is.EqualTo("Ada"));

		// Proof of the actual bound parameter VALUE, not just a name token in the SQL text -- EF
		// Core disposes each DbCommand (clearing its Parameters) before the awaited call returns,
		// so this is only observable because fakeDb snapshots parameters at execution time.
		var selectCommand = connection.ExecutedReaderCommands.Single();
		var bound = selectCommand.Parameters.Single(p => Equals(p.Value, "Ada"));
		Assert.That(bound.Value, Is.EqualTo("Ada"));
	}

	[Test]
	public async Task SaveChangesAsync_inserts_a_row_carrying_a_bound_string_parameter()
	{
		var factory = new fakeDbFactory(SupportedDatabase.Firebird);
		var connection = (fakeDbConnection)factory.CreateConnection()!;

		var options = new DbContextOptionsBuilder<CustomerContext>()
			.UseFirebird(connection)
			.Options;
		await using var db = new CustomerContext(options);

		connection.EnqueueNonQueryResult(1);
		connection.EnqueueReaderResult(new[] { new Dictionary<string, object> { ["Value"] = 1 } }, recordsAffected: 1);

		db.Add(new Customer { Id = 1, Name = "Ada", IsActive = true });
		await db.SaveChangesAsync();

		var allCommands = connection.ExecutedReaderCommands.Concat(connection.ExecutedNonQueryCommands).ToList();
		Assert.That(allCommands, Has.Some.Matches<CapturedCommand>(c => c.CommandText.Contains("INSERT", StringComparison.OrdinalIgnoreCase)));

		var insertCommand = allCommands.Single(c => c.CommandText.Contains("INSERT", StringComparison.OrdinalIgnoreCase));
		var boundName = insertCommand.Parameters.Single(p => Equals(p.Value, "Ada"));
		Assert.That(boundName.Value, Is.EqualTo("Ada"));
	}

	[Test]
	public async Task SaveChangesAsync_throws_DbUpdateConcurrencyException_when_zero_rows_are_affected()
	{
		var factory = new fakeDbFactory(SupportedDatabase.Firebird);
		var connection = (fakeDbConnection)factory.CreateConnection()!;

		var options = new DbContextOptionsBuilder<CustomerContext>()
			.UseFirebird(connection)
			.Options;
		await using var db = new CustomerContext(options);

		var customer = new Customer { Id = 1, Name = "Ada", IsActive = true };
		db.Attach(customer);
		customer.Name = "Grace";

		connection.EnqueueNonQueryResult(0);
		connection.EnqueueReaderResult(Array.Empty<Dictionary<string, object>>());

		Assert.That(
			async () => await db.SaveChangesAsync(),
			Throws.InstanceOf<DbUpdateConcurrencyException>());
	}

	[Test]
	public async Task SaveChangesAsync_throws_DbUpdateException_wrapping_the_provider_failure()
	{
		var factory = new fakeDbFactory(SupportedDatabase.Firebird);
		var connection = (fakeDbConnection)factory.CreateConnection()!;

		var options = new DbContextOptionsBuilder<CustomerContext>()
			.UseFirebird(connection)
			.Options;
		await using var db = new CustomerContext(options);

		var providerFailure = new InvalidOperationException("simulated provider failure");
		connection.SetNonQueryExecuteException(providerFailure);
		connection.EnqueueReaderResult(new fakeDbDataReader(Array.Empty<Dictionary<string, object>>())
		{
			FailAfterReadCount = 0,
			FailException = providerFailure,
			RecordsAffectedException = providerFailure
		});

		db.Add(new Customer { Id = 1, Name = "Ada", IsActive = true });

		var thrown = await CatchAsync(() => db.SaveChangesAsync());
		Assert.That(thrown, Is.TypeOf<DbUpdateException>());
		Assert.That(((DbUpdateException)thrown).InnerException, Is.SameAs(providerFailure));
	}

	static async Task<Exception> CatchAsync(Func<Task> action)
	{
		try
		{
			await action();
			return null;
		}
		catch (Exception ex)
		{
			return ex;
		}
	}

	sealed class CustomerContext(DbContextOptions<CustomerContext> options) : DbContext(options)
	{
		public DbSet<Customer> Customers => Set<Customer>();

		protected override void OnModelCreating(ModelBuilder modelBuilder)
		{
			// Sidesteps Firebird's identity/generator dialect entirely -- this file is about SQL
			// generation, parameter binding, and error translation, not identity-generation syntax.
			modelBuilder.Entity<Customer>().Property(c => c.Id).ValueGeneratedNever();
		}
	}

	sealed class Customer
	{
		public int Id { get; set; }
		public string Name { get; set; } = string.Empty;
		public bool IsActive { get; set; }
	}
}
