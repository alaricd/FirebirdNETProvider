using System;
using FirebirdSql.EntityFrameworkCore.Firebird.Extensions;
using FirebirdSql.EntityFrameworkCore.Firebird.Infrastructure.Internal;
using FirebirdSql.EntityFrameworkCore.Firebird.Metadata;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Metadata;
using NUnit.Framework;
using pengdows.crud.enums;
using pengdows.crud.fakeDb;

namespace FirebirdSql.EntityFrameworkCore.Firebird.Tests.Extensions;

// Exercises the fluent model/property-builder extensions and their IMutableModel/IMutableProperty
// annotation-backed counterparts in Extensions/Fb*Extensions.cs -- pure metadata/annotation
// get-set logic that never touches a connection. A fakeDb-backed context is only used as the
// cheapest way to obtain a real, finalized EF Core model to configure and read back.
public class FbFluentExtensionsFakeDbTests
{
	[Test]
	public void UseIdentityColumns_sets_the_model_value_generation_strategy()
	{
		using var db = BuildContext(mb => mb.UseIdentityColumns());
		Assert.That(db.Model.GetValueGenerationStrategy(), Is.EqualTo(FbValueGenerationStrategy.IdentityColumn));
	}

	[Test]
	public void UseSequenceTriggers_sets_the_model_value_generation_strategy()
	{
		using var db = BuildContext(mb => mb.UseSequenceTriggers());
		Assert.That(db.Model.GetValueGenerationStrategy(), Is.EqualTo(FbValueGenerationStrategy.SequenceTrigger));
	}

	[Test]
	public void UseHiLo_sets_the_model_strategy_and_default_sequence_name()
	{
		using var db = BuildContext(mb => mb.UseHiLo());
		Assert.That(db.Model.GetValueGenerationStrategy(), Is.EqualTo(FbValueGenerationStrategy.HiLo));
		Assert.That(db.Model.GetHiLoSequenceName(), Is.EqualTo("EntityFrameworkHiLoSequence"));
	}

	[Test]
	public void UseHiLo_with_a_name_sets_a_custom_sequence_name()
	{
		using var db = BuildContext(mb => mb.UseHiLo("CustomSeq"));
		Assert.That(db.Model.GetHiLoSequenceName(), Is.EqualTo("CustomSeq"));
		Assert.That(db.Model.FindSequence("CustomSeq"), Is.Not.Null);
	}

	[Test]
	public void Model_HiLoSequenceSchema_round_trips()
	{
		using var db = BuildContext(mb => mb.Model.SetHiLoSequenceSchema("myschema"));
		Assert.That(db.Model.GetHiLoSequenceSchema(), Is.EqualTo("myschema"));
	}

	// The four tests below hit the IMutableProperty overload of GetValueGenerationStrategy
	// specifically -- a near-duplicate of the IProperty overload already covered above, reading
	// it back mid-build (via .Metadata, which is IMutableProperty) rather than from the
	// finalized model (where .FindProperty returns IProperty).

	[Test]
	public void IMutableProperty_GetValueGenerationStrategy_falls_back_to_the_model_strategy()
	{
		FbValueGenerationStrategy? observed = null;
		using var db = BuildContext(mb =>
		{
			mb.UseIdentityColumns();
			observed = mb.Entity<Widget>().Property(w => w.Id).Metadata.GetValueGenerationStrategy();
		});
		_ = db.Model;
		Assert.That(observed, Is.EqualTo(FbValueGenerationStrategy.IdentityColumn));
	}

	[Test]
	public void IMutableProperty_GetValueGenerationStrategy_model_level_SequenceTrigger_falls_back()
	{
		FbValueGenerationStrategy? observed = null;
		using var db = BuildContext(mb =>
		{
			mb.UseSequenceTriggers();
			observed = mb.Entity<Widget>().Property(w => w.Id).Metadata.GetValueGenerationStrategy();
		});
		_ = db.Model;
		Assert.That(observed, Is.EqualTo(FbValueGenerationStrategy.SequenceTrigger));
	}

	[Test]
	public void IMutableProperty_GetValueGenerationStrategy_model_level_HiLo_falls_back()
	{
		FbValueGenerationStrategy? observed = null;
		using var db = BuildContext(mb =>
		{
			mb.UseHiLo();
			observed = mb.Entity<Widget>().Property(w => w.Id).Metadata.GetValueGenerationStrategy();
		});
		_ = db.Model;
		Assert.That(observed, Is.EqualTo(FbValueGenerationStrategy.HiLo));
	}

	[Test]
	public void IMutableProperty_GetValueGenerationStrategy_returns_None_for_an_incompatible_property_type()
	{
		FbValueGenerationStrategy? observed = null;
		using var db = BuildContext(mb =>
		{
			mb.UseIdentityColumns();
			observed = mb.Entity<Widget>().Property(w => w.Name).Metadata.GetValueGenerationStrategy();
		});
		_ = db.Model;
		Assert.That(observed, Is.EqualTo(FbValueGenerationStrategy.None));
	}

	[Test]
	public void IMutableProperty_GetValueGenerationStrategy_returns_None_for_a_property_that_never_generates_a_value()
	{
		FbValueGenerationStrategy? observed = null;
		using var db = BuildContext(mb =>
		{
			mb.UseIdentityColumns();
			mb.Entity<Widget>().Property(w => w.Id).ValueGeneratedNever();
			observed = mb.Entity<Widget>().Property(w => w.Id).Metadata.GetValueGenerationStrategy();
		});
		_ = db.Model;
		Assert.That(observed, Is.EqualTo(FbValueGenerationStrategy.None));
	}

	[Test]
	public void Model_SequenceNameSuffix_round_trips_with_a_default()
	{
		using var defaultDb = BuildContext(_ => { });
		Assert.That(defaultDb.Model.GetSequenceNameSuffix(), Is.EqualTo("Sequence"));

		using var customDb = BuildContext(mb => mb.Model.SetSequenceNameSuffix("Seq"));
		Assert.That(customDb.Model.GetSequenceNameSuffix(), Is.EqualTo("Seq"));
	}

	[Test]
	public void Model_SequenceSchema_round_trips()
	{
		using var db = BuildContext(mb => mb.Model.SetSequenceSchema("myschema"));
		Assert.That(db.Model.GetSequenceSchema(), Is.EqualTo("myschema"));
	}

	// FbModelExtensions also exposes an IConventionModel-typed overload of every one of the
	// IMutableModel setters above, for use inside EF Core's own convention pipeline (conventions
	// operate on the convention-stage model, not the finalized IReadOnlyModel/IMutableModel). EF
	// Core's internal Model implementation satisfies all three interfaces simultaneously, so this
	// exercises them directly by casting mb.Model during OnModelCreating, without needing to write
	// a custom IConvention* to trigger them through the real pipeline.
	[Test]
	public void Model_IConventionModel_setters_round_trip_and_report_the_explicit_configuration_source()
	{
		ConfigurationSource? hiLoNameSource = null;
		ConfigurationSource? hiLoSchemaSource = null;
		ConfigurationSource? suffixSource = null;
		ConfigurationSource? schemaSource = null;

		using var db = BuildContext(mb =>
		{
			// IConventionModel is only valid during OnModelCreating -- db.Model afterwards is the
			// finalized RuntimeModel, which does not implement it, so the configuration-source
			// getters (a convention-stage-only concept) must be read here, not after finalization.
			var conventionModel = (IConventionModel)mb.Model;

			conventionModel.SetValueGenerationStrategy(FbValueGenerationStrategy.SequenceTrigger, fromDataAnnotation: true);
			conventionModel.SetHiLoSequenceName("ConventionSeq", fromDataAnnotation: true);
			conventionModel.SetHiLoSequenceSchema("conv_schema", fromDataAnnotation: true);
			conventionModel.SetSequenceNameSuffix("ConvSuffix", fromDataAnnotation: true);
			conventionModel.SetSequenceSchema("conv_seq_schema", fromDataAnnotation: true);

			hiLoNameSource = conventionModel.GetHiLoSequenceNameConfigurationSource();
			hiLoSchemaSource = conventionModel.GetHiLoSequenceSchemaConfigurationSource();
			suffixSource = conventionModel.GetSequenceNameSuffixConfigurationSource();
			schemaSource = conventionModel.GetSequenceSchemaConfigurationSource();
		});

		var model = db.Model;
		Assert.That(model.GetValueGenerationStrategy(), Is.EqualTo(FbValueGenerationStrategy.SequenceTrigger));
		Assert.That(model.GetHiLoSequenceName(), Is.EqualTo("ConventionSeq"));
		Assert.That(model.GetHiLoSequenceSchema(), Is.EqualTo("conv_schema"));
		Assert.That(model.GetSequenceNameSuffix(), Is.EqualTo("ConvSuffix"));
		Assert.That(model.GetSequenceSchema(), Is.EqualTo("conv_seq_schema"));

		Assert.That(hiLoNameSource, Is.EqualTo(ConfigurationSource.DataAnnotation));
		Assert.That(hiLoSchemaSource, Is.EqualTo(ConfigurationSource.DataAnnotation));
		Assert.That(suffixSource, Is.EqualTo(ConfigurationSource.DataAnnotation));
		Assert.That(schemaSource, Is.EqualTo(ConfigurationSource.DataAnnotation));
	}

	// HasHiLoSequence/CanSetHiLoSequence/HasValueGenerationStrategy in FbPropertyBuilderExtensions
	// are IConventionPropertyBuilder-typed, the same convention-stage-only pattern as the
	// IConventionModel test above -- IConventionProperty.Builder gives the matching
	// IConventionPropertyBuilder while still inside OnModelCreating.
	[Test]
	public void IConventionPropertyBuilder_HasHiLoSequence_sets_the_sequence_name_and_creates_the_sequence()
	{
		using var db = BuildContext(mb =>
		{
			var conventionProperty = (IConventionProperty)mb.Entity<Widget>().Property(w => w.Id).Metadata;
			var propertyBuilder = conventionProperty.Builder;

			Assert.That(propertyBuilder.CanSetHiLoSequence("ConvHiLoSeq"), Is.True);

			var sequenceBuilder = propertyBuilder.HasHiLoSequence("ConvHiLoSeq");

			Assert.That(sequenceBuilder, Is.Not.Null);
		});

		var property = db.Model.FindEntityType(typeof(Widget)).FindProperty(nameof(Widget.Id));
		Assert.That(property.GetHiLoSequenceName(), Is.EqualTo("ConvHiLoSeq"));
		Assert.That(db.Model.FindSequence("ConvHiLoSeq"), Is.Not.Null);
	}

	[Test]
	public void IConventionPropertyBuilder_HasValueGenerationStrategy_sets_the_strategy_when_allowed()
	{
		using var db = BuildContext(mb =>
		{
			var conventionProperty = (IConventionProperty)mb.Entity<Widget>().Property(w => w.Id).Metadata;
			var propertyBuilder = conventionProperty.Builder;

			var result = propertyBuilder.HasValueGenerationStrategy(FbValueGenerationStrategy.SequenceTrigger);

			Assert.That(result, Is.Not.Null);
		});

		var property = db.Model.FindEntityType(typeof(Widget)).FindProperty(nameof(Widget.Id));
		Assert.That(property.GetValueGenerationStrategy(), Is.EqualTo(FbValueGenerationStrategy.SequenceTrigger));
	}

	[Test]
	public void UseIdentityColumn_on_a_property_sets_its_value_generation_strategy()
	{
		using var db = BuildContext(mb => mb.Entity<Widget>().Property(w => w.Id).UseIdentityColumn());
		var property = db.Model.FindEntityType(typeof(Widget)).FindProperty(nameof(Widget.Id));
		Assert.That(property.GetValueGenerationStrategy(), Is.EqualTo(FbValueGenerationStrategy.IdentityColumn));
	}

	[Test]
	public void UseSequenceTrigger_on_a_property_sets_its_value_generation_strategy()
	{
		using var db = BuildContext(mb => mb.Entity<Widget>().Property(w => w.Id).UseSequenceTrigger());
		var property = db.Model.FindEntityType(typeof(Widget)).FindProperty(nameof(Widget.Id));
		Assert.That(property.GetValueGenerationStrategy(), Is.EqualTo(FbValueGenerationStrategy.SequenceTrigger));
	}

	[Test]
	public void UseHiLo_on_a_property_sets_its_value_generation_strategy_and_sequence()
	{
		using var db = BuildContext(mb => mb.Entity<Widget>().Property(w => w.Id).UseHiLo("WidgetSeq"));
		var property = db.Model.FindEntityType(typeof(Widget)).FindProperty(nameof(Widget.Id));
		Assert.That(property.GetValueGenerationStrategy(), Is.EqualTo(FbValueGenerationStrategy.HiLo));
		Assert.That(property.GetHiLoSequenceName(), Is.EqualTo("WidgetSeq"));
		Assert.That(db.Model.FindSequence("WidgetSeq"), Is.Not.Null);
	}

	[Test]
	public void SetValueGenerationStrategy_IdentityColumn_on_a_string_property_throws()
	{
		// IMutableProperty mutation must happen while the model is still being built (during
		// OnModelCreating) -- db.Model returns the finalized, read-only model. The exception is
		// raised the first time the model is built, i.e. the first access of db.Model below.
		using var db = BuildContext(mb =>
			mb.Entity<Widget>().Property(w => w.Name).Metadata.SetValueGenerationStrategy(FbValueGenerationStrategy.IdentityColumn));

		Assert.That(() => db.Model, Throws.ArgumentException);
	}

	[Test]
	public void SetValueGenerationStrategy_IdentityColumn_on_an_integer_property_succeeds()
	{
		using var db = BuildContext(mb =>
			mb.Entity<Widget>().Property(w => w.Id).Metadata.SetValueGenerationStrategy(FbValueGenerationStrategy.IdentityColumn));

		var property = db.Model.FindEntityType(typeof(Widget)).FindProperty(nameof(Widget.Id));
		Assert.That(property.GetValueGenerationStrategy(), Is.EqualTo(FbValueGenerationStrategy.IdentityColumn));
	}

	[Test]
	public void GetValueGenerationStrategy_falls_back_to_the_model_strategy_for_a_compatible_property()
	{
		// No explicit per-property annotation -- the property must inherit IdentityColumn from
		// the model-level strategy because its CLR type (int) is compatible.
		using var db = BuildContext(mb => mb.UseIdentityColumns());
		var property = db.Model.FindEntityType(typeof(Widget)).FindProperty(nameof(Widget.Id));
		Assert.That(property.GetValueGenerationStrategy(), Is.EqualTo(FbValueGenerationStrategy.IdentityColumn));
	}

	[Test]
	public void GetValueGenerationStrategy_model_level_SequenceTrigger_falls_back_onto_a_compatible_property()
	{
		using var db = BuildContext(mb => mb.UseSequenceTriggers());
		var property = db.Model.FindEntityType(typeof(Widget)).FindProperty(nameof(Widget.Id));
		Assert.That(property.GetValueGenerationStrategy(), Is.EqualTo(FbValueGenerationStrategy.SequenceTrigger));
	}

	[Test]
	public void GetValueGenerationStrategy_model_level_HiLo_falls_back_onto_a_compatible_property()
	{
		using var db = BuildContext(mb => mb.UseHiLo());
		var property = db.Model.FindEntityType(typeof(Widget)).FindProperty(nameof(Widget.Id));
		Assert.That(property.GetValueGenerationStrategy(), Is.EqualTo(FbValueGenerationStrategy.HiLo));
	}

	[Test]
	public void GetValueGenerationStrategy_returns_None_for_a_property_that_never_generates_a_value()
	{
		// ValueGeneratedNever short-circuits before the model-level strategy is even consulted,
		// regardless of an otherwise-compatible CLR type.
		using var db = BuildContext(mb =>
		{
			mb.UseIdentityColumns();
			mb.Entity<Widget>().Property(w => w.Id).ValueGeneratedNever();
		});
		var property = db.Model.FindEntityType(typeof(Widget)).FindProperty(nameof(Widget.Id));
		Assert.That(property.GetValueGenerationStrategy(), Is.EqualTo(FbValueGenerationStrategy.None));
	}

	[Test]
	public void GetValueGenerationStrategy_returns_None_for_a_model_level_strategy_incompatible_with_the_property_type()
	{
		// The model says IdentityColumn, but Widget.Name is a string -- IsCompatibleIdentityColumn
		// rejects it, so it falls through to None rather than inheriting the model strategy.
		using var db = BuildContext(mb => mb.UseIdentityColumns());
		var property = db.Model.FindEntityType(typeof(Widget)).FindProperty(nameof(Widget.Name));
		Assert.That(property.GetValueGenerationStrategy(), Is.EqualTo(FbValueGenerationStrategy.None));
	}

	[Test]
	public void Validate_throws_when_a_value_generation_strategy_conflicts_with_a_default_value()
	{
		using var db = BuildContext(mb =>
		{
			mb.Entity<Widget>().Property(w => w.Id).Metadata.SetValueGenerationStrategy(FbValueGenerationStrategy.IdentityColumn);
			mb.Entity<Widget>().Property(w => w.Id).HasDefaultValue(5);
		});

		Assert.That(() => db.Model, Throws.InvalidOperationException);
	}

	[Test]
	public void Validate_throws_when_a_value_generation_strategy_conflicts_with_a_default_value_sql()
	{
		using var db = BuildContext(mb =>
		{
			mb.Entity<Widget>().Property(w => w.Id).Metadata.SetValueGenerationStrategy(FbValueGenerationStrategy.IdentityColumn);
			mb.Entity<Widget>().Property(w => w.Id).HasDefaultValueSql("1");
		});

		Assert.That(() => db.Model, Throws.InvalidOperationException);
	}

	[Test]
	public void Validate_throws_when_a_value_generation_strategy_conflicts_with_a_computed_column_sql()
	{
		using var db = BuildContext(mb =>
		{
			mb.Entity<Widget>().Property(w => w.Id).Metadata.SetValueGenerationStrategy(FbValueGenerationStrategy.IdentityColumn);
			mb.Entity<Widget>().Property(w => w.Id).HasComputedColumnSql("1");
		});

		Assert.That(() => db.Model, Throws.InvalidOperationException);
	}

	[Test]
	public void Property_SequenceName_and_SequenceSchema_round_trip()
	{
		using var db = BuildContext(mb =>
		{
			var property = mb.Entity<Widget>().Property(w => w.Id).Metadata;
			property.SetSequenceName("WidgetIdSeq");
			property.SetSequenceSchema("myschema");
		});

		var property = db.Model.FindEntityType(typeof(Widget)).FindProperty(nameof(Widget.Id));
		Assert.That(property.GetSequenceName(), Is.EqualTo("WidgetIdSeq"));
		Assert.That(property.GetSequenceSchema(), Is.EqualTo("myschema"));
	}

	[Test]
	public void FindHiLoSequence_resolves_the_sequence_the_property_was_configured_with()
	{
		using var db = BuildContext(mb => mb.Entity<Widget>().Property(w => w.Id).UseHiLo("WidgetSeq"));
		var property = (IProperty)db.Model.FindEntityType(typeof(Widget)).FindProperty(nameof(Widget.Id));

		var sequence = property.FindHiLoSequence();

		Assert.That(sequence, Is.Not.Null);
		Assert.That(sequence.Name, Is.EqualTo("WidgetSeq"));
	}

	[Test]
	public void IsFirebird_is_true_for_a_context_configured_with_UseFirebird()
	{
		using var db = BuildContext(_ => { });
		Assert.That(db.Database.IsFirebird(), Is.True);
	}

	[Test]
	public void WithExplicitParameterTypes_and_WithExplicitStringLiteralTypes_are_stored_on_the_options_extension()
	{
		var factory = new fakeDbFactory(SupportedDatabase.Firebird);
		var connection = factory.CreateConnection();
		var optionsBuilder = new DbContextOptionsBuilder<WidgetContext>()
			.UseFirebird(connection, fb => fb.WithExplicitParameterTypes().WithExplicitStringLiteralTypes());
		using var db = new WidgetContext(optionsBuilder.Options);

		var extension = db.GetService<IDbContextOptions>().FindExtension<FbOptionsExtension>();

		Assert.That(extension.ExplicitParameterTypes, Is.True);
		Assert.That(extension.ExplicitStringLiteralTypes, Is.True);
	}

	static WidgetContext BuildContext(Action<ModelBuilder> configure)
	{
		var factory = new fakeDbFactory(SupportedDatabase.Firebird);
		var connection = factory.CreateConnection();
		// Every test below reuses the same WidgetContext type with a different runtime `configure`
		// lambda, which EF Core's default model cache key (keyed only by context TYPE) can't see --
		// without this, the FIRST test's built model would get cached and silently reused for every
		// later test that happens to run against this same context type.
		var options = new DbContextOptionsBuilder<WidgetContext>()
			.UseFirebird(connection)
			.ReplaceService<IModelCacheKeyFactory, NeverCacheModelCacheKeyFactory>()
			.Options;
		return new WidgetContext(options, configure);
	}

	public sealed class WidgetContext(DbContextOptions<WidgetContext> options, Action<ModelBuilder> configure = null) : DbContext(options)
	{
		public DbSet<Widget> Widgets => Set<Widget>();

		protected override void OnModelCreating(ModelBuilder modelBuilder)
		{
			// Deliberately NOT forcing ValueGeneratedNever here: Widget.Id keeps EF's default
			// ValueGenerated.OnAdd convention for an int primary key, which several tests below
			// rely on to observe the model-level value generation strategy actually falling back
			// onto the property. Tests that need ValueGeneratedNever set it themselves.
			configure?.Invoke(modelBuilder);
		}
	}

	sealed class NeverCacheModelCacheKeyFactory : IModelCacheKeyFactory
	{
		public object Create(DbContext context, bool designTime) => new object();
	}

	public sealed class Widget
	{
		public int Id { get; set; }
		public string Name { get; set; } = string.Empty;
	}
}
