using FirebirdSql.EntityFrameworkCore.Firebird.Metadata.Conventions;
using Microsoft.EntityFrameworkCore;
using NUnit.Framework;
using pengdows.crud.enums;
using pengdows.crud.fakeDb;

namespace FirebirdSql.EntityFrameworkCore.Firebird.Tests.Metadata.Conventions;

// FbStoreGenerationConvention.ProcessPropertyAnnotationChanged stops a property's annotation
// pipeline as soon as one server-generation mechanism (DefaultValue, DefaultValueSql,
// ComputedColumnSql, or Firebird's own IdentityColumn value-generation strategy) is set, so a
// second, conflicting mechanism never gets applied by convention; Validate is the later,
// model-finalization-time backstop that throws if a genuine conflict slipped through anyway (e.g.
// set via explicit data-annotation configuration source, which bypasses the convention-time
// early exit). Model building runs entirely client-side, no database connection needed.
public class FbStoreGenerationConventionFakeDbTests
{
	[Test]
	public void A_property_with_only_a_default_value_builds_without_conflict()
	{
		using var db = new ConflictContext(Options());
		db.Model.GetEntityTypes(); // trigger finalization
		var property = db.Model.FindEntityType(typeof(Widget)).FindProperty(nameof(Widget.Status));
		Assert.That(property.GetDefaultValue(), Is.EqualTo("pending"));
	}

	[Test]
	public void A_property_with_only_a_default_value_sql_builds_without_conflict()
	{
		using var db = new DefaultValueSqlContext(Options<DefaultValueSqlContext>());
		db.Model.GetEntityTypes();
		var property = db.Model.FindEntityType(typeof(Widget)).FindProperty(nameof(Widget.Status));
		Assert.That(property.GetDefaultValueSql(), Is.EqualTo("'pending'"));
	}

	[Test]
	public void A_property_with_only_a_computed_column_sql_builds_without_conflict()
	{
		using var db = new ComputedColumnContext(Options<ComputedColumnContext>());
		db.Model.GetEntityTypes();
		var property = db.Model.FindEntityType(typeof(Widget)).FindProperty(nameof(Widget.Status));
		Assert.That(property.GetComputedColumnSql(), Is.EqualTo("'computed'"));
	}

	[Test]
	public void FbConventionSetBuilder_Build_returns_a_convention_set_without_a_real_server()
	{
		var conventionSet = FbConventionSetBuilder.Build();

		Assert.That(conventionSet, Is.Not.Null);
		Assert.That(conventionSet.ModelFinalizingConventions, Is.Not.Empty);
	}

	static DbContextOptions<ConflictContext> Options()
		=> Options<ConflictContext>();

	static DbContextOptions<TContext> Options<TContext>() where TContext : DbContext
	{
		var factory = new fakeDbFactory(SupportedDatabase.Firebird);
		var connection = factory.CreateConnection();
		return new DbContextOptionsBuilder<TContext>().UseFirebird(connection).Options;
	}

	sealed class ConflictContext(DbContextOptions<ConflictContext> options) : DbContext(options)
	{
		public DbSet<Widget> Widgets => Set<Widget>();

		protected override void OnModelCreating(ModelBuilder modelBuilder)
		{
			modelBuilder.Entity<Widget>(e =>
			{
				e.Property(w => w.Id).ValueGeneratedNever();
				e.Property(w => w.Status).HasDefaultValue("pending");
			});
		}
	}

	sealed class DefaultValueSqlContext(DbContextOptions<DefaultValueSqlContext> options) : DbContext(options)
	{
		public DbSet<Widget> Widgets => Set<Widget>();

		protected override void OnModelCreating(ModelBuilder modelBuilder)
		{
			modelBuilder.Entity<Widget>(e =>
			{
				e.Property(w => w.Id).ValueGeneratedNever();
				e.Property(w => w.Status).HasDefaultValueSql("'pending'");
			});
		}
	}

	sealed class ComputedColumnContext(DbContextOptions<ComputedColumnContext> options) : DbContext(options)
	{
		public DbSet<Widget> Widgets => Set<Widget>();

		protected override void OnModelCreating(ModelBuilder modelBuilder)
		{
			modelBuilder.Entity<Widget>(e =>
			{
				e.Property(w => w.Id).ValueGeneratedNever();
				e.Property(w => w.Status).HasComputedColumnSql("'computed'");
			});
		}
	}

	sealed class Widget
	{
		public int Id { get; set; }
		public string Status { get; set; } = string.Empty;
	}
}
