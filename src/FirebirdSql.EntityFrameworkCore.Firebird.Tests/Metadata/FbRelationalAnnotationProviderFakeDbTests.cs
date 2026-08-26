using FirebirdSql.EntityFrameworkCore.Firebird.Metadata;
using FirebirdSql.EntityFrameworkCore.Firebird.Metadata.Internal;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Storage;
using NUnit.Framework;
using pengdows.crud.enums;
using pengdows.crud.fakeDb;

namespace FirebirdSql.EntityFrameworkCore.Firebird.Tests.Metadata;

// FbRelationalAnnotationProvider.For(IColumn, designTime) only yields the Firebird value-
// generation-strategy annotation for the DESIGN-TIME relational model (used by migrations
// scaffolding / script generation to know a column is an identity/HiLo/sequence-trigger column) --
// the runtime relational model (designTime: false) always yields nothing early, which every other
// fakeDb test in this project exercises just by building an ordinary DbContext. Reaching the
// design-time branch needs IDesignTimeModel.Model specifically, no real server required.
public class FbRelationalAnnotationProviderFakeDbTests
{
	[Test]
	public void For_yields_the_value_generation_strategy_annotation_on_the_design_time_model()
	{
		using var db = BuildContext(mb => mb.Entity<Widget>().Property(w => w.Id).UseIdentityColumn());

		var designTimeModel = db.GetService<IDesignTimeModel>().Model;
		var table = designTimeModel.GetRelationalModel().FindTable("Widgets", schema: null);
		var column = table.FindColumn(nameof(Widget.Id));

		var annotation = column.FindAnnotation(FbAnnotationNames.ValueGenerationStrategy);

		Assert.That(annotation, Is.Not.Null);
		Assert.That(annotation.Value, Is.EqualTo(FbValueGenerationStrategy.IdentityColumn));
	}

	[Test]
	public void For_yields_nothing_for_a_column_with_no_value_generation_strategy()
	{
		using var db = BuildContext(mb => mb.Entity<Widget>().Property(w => w.Id).ValueGeneratedNever());

		var designTimeModel = db.GetService<IDesignTimeModel>().Model;
		var table = designTimeModel.GetRelationalModel().FindTable("Widgets", schema: null);
		var column = table.FindColumn(nameof(Widget.Id));

		Assert.That(column.FindAnnotation(FbAnnotationNames.ValueGenerationStrategy), Is.Null);
	}

	static WidgetContext BuildContext(System.Action<ModelBuilder> configure)
	{
		var factory = new fakeDbFactory(SupportedDatabase.Firebird);
		var connection = factory.CreateConnection();
		var options = new DbContextOptionsBuilder<WidgetContext>()
			.UseFirebird(connection)
			// EF Core's default IModelCacheKeyFactory keys the compiled model cache by DbContext
			// TYPE alone, not by what OnModelCreating actually did -- both tests here use the same
			// WidgetContext type with a different configure lambda, which would otherwise make the
			// second test silently reuse the first test's cached (and differently-configured)
			// model. Confirmed by direct reproduction: without this, whichever test happened to
			// run second read back the FIRST test's column annotations.
			.ReplaceService<IModelCacheKeyFactory, NeverCacheModelCacheKeyFactory>()
			.Options;
		return new WidgetContext(options, configure);
	}

	sealed class WidgetContext(DbContextOptions<WidgetContext> options, System.Action<ModelBuilder> configure) : DbContext(options)
	{
		public DbSet<Widget> Widgets => Set<Widget>();

		protected override void OnModelCreating(ModelBuilder modelBuilder)
			=> configure(modelBuilder);
	}

	sealed class Widget
	{
		public int Id { get; set; }
	}

	sealed class NeverCacheModelCacheKeyFactory : IModelCacheKeyFactory
	{
		public object Create(DbContext context, bool designTime) => new object();
	}
}
