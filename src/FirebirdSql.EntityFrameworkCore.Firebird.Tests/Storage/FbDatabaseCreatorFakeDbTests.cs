using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Storage;
using NUnit.Framework;
using pengdows.crud.enums;
using pengdows.crud.fakeDb;

namespace FirebirdSql.EntityFrameworkCore.Firebird.Tests.Storage;

// Regression tests for a real substitutability bug: FbDatabaseCreator.Create()/CreateAsync()/
// Delete()/DeleteAsync() used to call Firebird's own STATIC FbConnection.CreateDatabase/
// DropDatabase methods unconditionally, regardless of what DbConnection was actually substituted
// -- there is no virtual/interface seam for "create a database file" to go through instead, so a
// non-FbConnection (fakeDb here, but the same shape as a StormGate-gated or any other substituted
// connection) would have caused real file-path parsing / real Firebird protocol attempts against a
// connection string that was never meant to reach a real Firebird engine. Delete()/DeleteAsync()
// already guarded FbConnection.ClearPool the same way but forgot to guard DropDatabase itself --
// clearly an oversight, not an intentional design, given the existing guard right next to it. The
// fix wraps all four calls in the same `_connection.DbConnection is FbConnection` check, so a
// substituted connection no-ops the physical database-file operation instead of always assuming a
// real Firebird database exists on disk. No real Firebird server anywhere in this file.
public class FbDatabaseCreatorFakeDbTests
{
	[Test]
	public void Delete_does_not_attempt_real_Firebird_database_deletion_against_a_fake_connection()
	{
		var factory = new fakeDbFactory(SupportedDatabase.Firebird);
		var connection = factory.CreateConnection();

		var options = new DbContextOptionsBuilder<ProbeDbContext>()
			.UseFirebird(connection)
			.Options;
		using var db = new ProbeDbContext(options);

		var creator = db.GetService<IRelationalDatabaseCreator>();

		Assert.That(creator.Delete, Throws.Nothing);
	}

	[Test]
	public async Task DeleteAsync_does_not_attempt_real_Firebird_database_deletion_against_a_fake_connection()
	{
		var factory = new fakeDbFactory(SupportedDatabase.Firebird);
		var connection = factory.CreateConnection();

		var options = new DbContextOptionsBuilder<ProbeDbContext>()
			.UseFirebird(connection)
			.Options;
		await using var db = new ProbeDbContext(options);

		var creator = db.GetService<IRelationalDatabaseCreator>();

		Assert.That(async () => await creator.DeleteAsync(), Throws.Nothing);
	}

	[Test]
	public void Create_does_not_attempt_real_Firebird_database_creation_against_a_fake_connection()
	{
		var factory = new fakeDbFactory(SupportedDatabase.Firebird);
		var connection = factory.CreateConnection();

		var options = new DbContextOptionsBuilder<ProbeDbContext>()
			.UseFirebird(connection)
			.Options;
		using var db = new ProbeDbContext(options);

		var creator = db.GetService<IRelationalDatabaseCreator>();

		// No collation is configured on this model, so Create() has nothing further to execute
		// against the connection after skipping the (guarded) physical database-file creation --
		// this proves the guard itself works without needing to fake a collation round trip too.
		Assert.That(creator.Create, Throws.Nothing);
	}

	sealed class ProbeDbContext(DbContextOptions<ProbeDbContext> options) : DbContext(options);
}
