using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using NUnit.Framework;
using pengdows.crud.enums;
using pengdows.crud.fakeDb;

namespace FirebirdSql.EntityFrameworkCore.Firebird.Tests.Update;

// Exercises FbUpdateSqlGenerator's "anyRead" branches -- the RETURNING/EXECUTE BLOCK shapes it
// emits when SaveChanges needs a value back from the server (a server-generated identity column
// on insert, a concurrency token on update), which FakeDbEfInteropTests.cs's plain
// ValueGeneratedNever entity never exercises.
public class FbUpdateSqlGeneratorFakeDbTests
{
	[Test]
	public async Task SaveChangesAsync_insert_with_a_server_generated_identity_column_reads_it_back_via_RETURNING()
	{
		var factory = new fakeDbFactory(SupportedDatabase.Firebird);
		var connection = (fakeDbConnection)factory.CreateConnection();
		var options = new DbContextOptionsBuilder<GeneratedIdContext>().UseFirebird(connection).Options;
		await using var db = new GeneratedIdContext(options);

		connection.EnqueueReaderResult(new[] { new Dictionary<string, object> { ["Id"] = 42 } });

		var entity = new GeneratedIdEntity { Name = "Ada" };
		db.Add(entity);
		await db.SaveChangesAsync();

		Assert.That(entity.Id, Is.EqualTo(42));
		var command = connection.ExecutedReaderCommands.Single();
		Assert.That(command.CommandText, Does.Contain("RETURNING"));
	}

	[Test]
	public async Task SaveChangesAsync_update_with_a_concurrency_token_reads_the_new_value_back()
	{
		var factory = new fakeDbFactory(SupportedDatabase.Firebird);
		var connection = (fakeDbConnection)factory.CreateConnection();
		var options = new DbContextOptionsBuilder<TokenContext>().UseFirebird(connection).Options;
		await using var db = new TokenContext(options);

		var entity = new TokenEntity { Id = 1, Name = "Ada", Version = 1 };
		db.Attach(entity);
		entity.Name = "Grace";

		// The EXECUTE BLOCK's own RETURNING ... INTO reads the new row version back through the
		// same result set that reports rows-affected -- one row is enough for both.
		connection.EnqueueReaderResult(new[] { new Dictionary<string, object> { ["Version"] = 2 } });

		await db.SaveChangesAsync();

		Assert.That(entity.Version, Is.EqualTo(2));
		var command = connection.ExecutedReaderCommands.Single();
		Assert.That(command.CommandText, Does.Contain("RETURNING"));
		Assert.That(command.CommandText, Does.Contain("SUSPEND"));
	}

	sealed class GeneratedIdContext(DbContextOptions<GeneratedIdContext> options) : DbContext(options)
	{
		public DbSet<GeneratedIdEntity> Entities => Set<GeneratedIdEntity>();

		protected override void OnModelCreating(ModelBuilder modelBuilder)
		{
			modelBuilder.UseIdentityColumns();
		}
	}

	sealed class GeneratedIdEntity
	{
		public int Id { get; set; }
		public string Name { get; set; } = string.Empty;
	}

	sealed class TokenContext(DbContextOptions<TokenContext> options) : DbContext(options)
	{
		public DbSet<TokenEntity> Entities => Set<TokenEntity>();

		protected override void OnModelCreating(ModelBuilder modelBuilder)
		{
			modelBuilder.Entity<TokenEntity>(entity =>
			{
				entity.Property(e => e.Id).ValueGeneratedNever();
				entity.Property(e => e.Version).IsRowVersion();
			});
		}
	}

	sealed class TokenEntity
	{
		public int Id { get; set; }
		public string Name { get; set; } = string.Empty;
		public int Version { get; set; }
	}
}
