using System;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using NUnit.Framework;
using pengdows.crud.enums;
using pengdows.crud.fakeDb;

namespace FirebirdSql.EntityFrameworkCore.Firebird.Tests.Migrations;

// Drives FbHistoryRepository (the migrations "__EFMigrationsHistory" table existence check +
// advisory table-lock acquisition, via a Firebird-specific EXECUTE BLOCK / unique-constraint-based
// lock rather than a real advisory-lock primitive) end to end against fakeDb -- real SQL text
// generation, real command execution, and the actual retry-until-acquired loop, all without a real
// Firebird server. The companion, real-server FbHistoryRepositoryTests-style coverage lives in
// Migrations/MigrationsTests.cs.
public class FbHistoryRepositoryFakeDbTests
{
	[Test]
	public void GetBeginIfExistsScript_is_not_supported()
	{
		var repository = BuildRepository(out _);
		Assert.That(() => repository.GetBeginIfExistsScript("0001"), Throws.InstanceOf<NotSupportedException>());
	}

	[Test]
	public void GetBeginIfNotExistsScript_is_not_supported()
	{
		var repository = BuildRepository(out _);
		Assert.That(() => repository.GetBeginIfNotExistsScript("0001"), Throws.InstanceOf<NotSupportedException>());
	}

	[Test]
	public void GetEndIfScript_is_not_supported()
	{
		var repository = BuildRepository(out _);
		Assert.That(repository.GetEndIfScript, Throws.InstanceOf<NotSupportedException>());
	}

	[Test]
	public void LockReleaseBehavior_is_Explicit()
	{
		var repository = BuildRepository(out _);
		Assert.That(repository.LockReleaseBehavior, Is.EqualTo(LockReleaseBehavior.Explicit));
	}

	[Test]
	public void GetCreateIfNotExistsScript_wraps_the_create_script_in_an_EXECUTE_BLOCK_guard()
	{
		var repository = BuildRepository(out _);
		var script = repository.GetCreateIfNotExistsScript();
		Assert.That(script, Does.Contain("EXECUTE BLOCK"));
		Assert.That(script, Does.Contain("EXECUTE STATEMENT"));
	}

	[Test]
	public void Exists_returns_true_when_the_catalog_reports_a_nonzero_row_count()
	{
		var repository = BuildRepository(out var connection);
		connection.EnqueueScalarResult(1L);

		Assert.That(repository.Exists(), Is.True);
	}

	[Test]
	public void Exists_returns_false_when_the_catalog_reports_zero_rows()
	{
		var repository = BuildRepository(out var connection);
		connection.EnqueueScalarResult(0L);

		Assert.That(repository.Exists(), Is.False);
	}

	[Test]
	public void AcquireDatabaseLock_creates_the_lock_table_and_returns_a_lock_on_the_first_successful_insert()
	{
		var repository = BuildRepository(out var connection);
		connection.EnqueueNonQueryResult(1); // CREATE TABLE IF NOT EXISTS guard
		connection.EnqueueScalarResult(1); // INSERT ... ROWS_AFFECTED = 1 -> acquired on the first try

		using var databaseLock = repository.AcquireDatabaseLock();

		Assert.That(databaseLock, Is.Not.Null);
		Assert.That(((FirebirdSql.EntityFrameworkCore.Firebird.Migrations.Internal.FbMigrationDatabaseLock)databaseLock).HistoryRepository, Is.SameAs(repository));

		// Disposing releases the lock -- also exercises FbMigrationDatabaseLock.Dispose().
		connection.EnqueueScalarResult(1);
	}

	[Test]
	public async Task AcquireDatabaseLockAsync_creates_the_lock_table_and_returns_a_lock_on_the_first_successful_insert()
	{
		var repository = BuildRepository(out var connection);
		connection.EnqueueNonQueryResult(1);
		connection.EnqueueScalarResult(1);

		await using var databaseLock = await repository.AcquireDatabaseLockAsync();

		Assert.That(databaseLock, Is.Not.Null);

		connection.EnqueueScalarResult(1);
	}

	static IHistoryRepository BuildRepository(out fakeDbConnection connection)
	{
		var factory = new fakeDbFactory(SupportedDatabase.Firebird);
		connection = (fakeDbConnection)factory.CreateConnection();
		var options = new DbContextOptionsBuilder<ProbeContext>().UseFirebird(connection).Options;
		var db = new ProbeContext(options);
		return db.GetService<IHistoryRepository>();
	}

	sealed class ProbeContext(DbContextOptions<ProbeContext> options) : DbContext(options);
}
