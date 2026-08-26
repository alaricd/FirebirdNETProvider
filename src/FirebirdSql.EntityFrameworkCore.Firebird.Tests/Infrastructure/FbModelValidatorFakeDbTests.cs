using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using NUnit.Framework;
using pengdows.crud.enums;
using pengdows.crud.fakeDb;

namespace FirebirdSql.EntityFrameworkCore.Firebird.Tests.Infrastructure;

// FbModelValidator.ValidateValueGeneration overrides the base relational validator to warn when a
// table-per-concrete-type (TPC) hierarchy's root key property uses Firebird's IdentityColumn
// value-generation strategy -- store-generated identity values don't work sensibly across TPC's
// separate per-type tables. Model validation runs entirely client-side at model-finalization time
// (triggered by first access to DbContext.Model), so this needs no database connection at all,
// fake or real, to exercise -- but we still route through fakeDb/UseFirebird for consistency with
// the rest of this test project and because OnConfiguring still needs a valid provider selected.
public class FbModelValidatorFakeDbTests
{
	[Test]
	public void ValidateValueGeneration_warns_when_a_TPC_root_key_uses_an_identity_column()
	{
		var connection = new fakeDbFactory(SupportedDatabase.Firebird).CreateConnection();

		var optionsBuilder = new DbContextOptionsBuilder<TpcIdentityContext>()
			.UseFirebird(connection)
			.ConfigureWarnings(w => w.Throw(RelationalEventId.TpcStoreGeneratedIdentityWarning));
		using var db = new TpcIdentityContext(optionsBuilder.Options);

		Assert.That(() => db.Model, Throws.Exception);
	}

	[Test]
	public void ValidateValueGeneration_does_not_warn_for_a_non_TPC_hierarchy_using_an_identity_column()
	{
		var connection = new fakeDbFactory(SupportedDatabase.Firebird).CreateConnection();

		var optionsBuilder = new DbContextOptionsBuilder<NonTpcIdentityContext>()
			.UseFirebird(connection)
			.ConfigureWarnings(w => w.Throw(RelationalEventId.TpcStoreGeneratedIdentityWarning));
		using var db = new NonTpcIdentityContext(optionsBuilder.Options);

		Assert.That(() => db.Model, Throws.Nothing);
	}

	class BaseEntity
	{
		public int Id { get; set; }
	}

	sealed class DerivedEntity : BaseEntity
	{
		public string Name { get; set; } = string.Empty;
	}

	sealed class TpcIdentityContext(DbContextOptions<TpcIdentityContext> options) : DbContext(options)
	{
		public DbSet<BaseEntity> BaseEntities => Set<BaseEntity>();
		public DbSet<DerivedEntity> DerivedEntities => Set<DerivedEntity>();

		protected override void OnModelCreating(ModelBuilder modelBuilder)
		{
			modelBuilder.Entity<BaseEntity>(b =>
			{
				b.UseTpcMappingStrategy();
				b.Property(e => e.Id).UseIdentityColumn();
			});
			modelBuilder.Entity<DerivedEntity>();
		}
	}

	sealed class NonTpcIdentityContext(DbContextOptions<NonTpcIdentityContext> options) : DbContext(options)
	{
		public DbSet<BaseEntity> BaseEntities => Set<BaseEntity>();

		protected override void OnModelCreating(ModelBuilder modelBuilder)
		{
			modelBuilder.Entity<BaseEntity>().Property(e => e.Id).UseIdentityColumn();
		}
	}
}
