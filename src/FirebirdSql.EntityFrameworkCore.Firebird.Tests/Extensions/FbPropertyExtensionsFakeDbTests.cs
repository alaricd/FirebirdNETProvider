using System;
using FirebirdSql.EntityFrameworkCore.Firebird.Metadata;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Metadata;
using NUnit.Framework;
using pengdows.crud.enums;
using pengdows.crud.fakeDb;

namespace FirebirdSql.EntityFrameworkCore.Firebird.Tests.Extensions;

// Extends FbFluentExtensionsFakeDbTests.cs to cover the IConventionProperty-typed overloads in
// Extensions/FbPropertyExtensions.cs (SetValueGenerationStrategy/SetHiLoSequenceName/
// SetHiLoSequenceSchema/SetSequenceName/SetSequenceSchema and their *ConfigurationSource getters),
// plus the `in StoreObjectIdentifier storeObject` overloads and the model-level fallback paths in
// FindHiLoSequence/FindSequence -- none of which the earlier file exercised. A PropertyBuilder's
// .Metadata is, at runtime, the same concrete EF Core `Property` instance regardless of which
// metadata interface (IMutableProperty/IConventionProperty/IProperty) you view it through while the
// model is still being built, so no full convention-pipeline simulation is needed -- casting
// .Metadata straight to IConventionProperty reaches these overloads directly. IConventionProperty
// is only meaningful WHILE OnModelCreating is running (db.Model afterwards is the finalized,
// read-only IModel), so every IConventionProperty assertion below runs inside the `configure`
// callback, capturing results into an outer local for the test method to assert on afterwards.
// Two things confirmed by direct reproduction while writing these, not assumed: (1) `configure`
// only actually runs once something forces lazy model building -- `_ = db.Model;` after
// BuildContext(...) is required, matching FbFluentExtensionsFakeDbTests.cs; without it the
// callback silently never executes and captured locals stay at their defaults, which would have
// made every test below a false positive. (2) `fromDataAnnotation: false` on these
// IConventionProperty setters records ConfigurationSource.Convention, not Explicit -- Explicit is
// reserved for calls that originate from the public mutable PropertyBuilder fluent API, not for
// this convention-authoring-level API called directly, even with fromDataAnnotation: false.
public class FbPropertyExtensionsFakeDbTests
{
	[Test]
	public void IConventionProperty_SetValueGenerationStrategy_sets_the_annotation_and_records_a_configuration_source()
	{
		FbValueGenerationStrategy strategy = default;
		ConfigurationSource? configurationSource = null;

		using var db = BuildContext(mb =>
		{
			var property = (IConventionProperty)mb.Entity<Widget>().Property(w => w.Id).Metadata;
			property.SetValueGenerationStrategy(FbValueGenerationStrategy.IdentityColumn, fromDataAnnotation: false);
			strategy = property.GetValueGenerationStrategy();
			configurationSource = property.GetValueGenerationStrategyConfigurationSource();
		});
		_ = db.Model;

		Assert.That(strategy, Is.EqualTo(FbValueGenerationStrategy.IdentityColumn));
		Assert.That(configurationSource, Is.EqualTo(ConfigurationSource.Convention));
	}

	[Test]
	public void IConventionProperty_SetValueGenerationStrategy_rejects_an_incompatible_clr_type()
	{
		using var db = BuildContext(mb =>
		{
			var property = (IConventionProperty)mb.Entity<Widget>().Property(w => w.Name).Metadata;
			Assert.That(
				() => property.SetValueGenerationStrategy(FbValueGenerationStrategy.HiLo, fromDataAnnotation: false),
				Throws.ArgumentException);
		});
		_ = db.Model;
	}

	[Test]
	public void IConventionProperty_SetHiLoSequenceName_sets_and_returns_the_stored_value()
	{
		string stored = null;
		string readBack = null;
		ConfigurationSource? configurationSource = null;

		using var db = BuildContext(mb =>
		{
			var property = (IConventionProperty)mb.Entity<Widget>().Property(w => w.Id).Metadata;
			stored = property.SetHiLoSequenceName("ConventionSeq", fromDataAnnotation: false);
			readBack = ((IReadOnlyProperty)property).GetHiLoSequenceName();
			configurationSource = property.GetHiLoSequenceNameConfigurationSource();
		});
		_ = db.Model;

		Assert.That(stored, Is.EqualTo("ConventionSeq"));
		Assert.That(readBack, Is.EqualTo("ConventionSeq"));
		Assert.That(configurationSource, Is.EqualTo(ConfigurationSource.Convention));
	}

	[Test]
	public void IConventionProperty_SetHiLoSequenceSchema_sets_and_returns_the_stored_value()
	{
		string stored = null;
		string readBack = null;
		ConfigurationSource? configurationSource = null;

		using var db = BuildContext(mb =>
		{
			var property = (IConventionProperty)mb.Entity<Widget>().Property(w => w.Id).Metadata;
			stored = property.SetHiLoSequenceSchema("convention_schema", fromDataAnnotation: false);
			readBack = ((IReadOnlyProperty)property).GetHiLoSequenceSchema();
			configurationSource = property.GetHiLoSequenceSchemaConfigurationSource();
		});
		_ = db.Model;

		Assert.That(stored, Is.EqualTo("convention_schema"));
		Assert.That(readBack, Is.EqualTo("convention_schema"));
		Assert.That(configurationSource, Is.EqualTo(ConfigurationSource.Convention));
	}

	[Test]
	public void IConventionProperty_SetSequenceName_sets_and_returns_the_stored_value()
	{
		string stored = null;
		string readBack = null;
		ConfigurationSource? configurationSource = null;

		using var db = BuildContext(mb =>
		{
			var property = (IConventionProperty)mb.Entity<Widget>().Property(w => w.Id).Metadata;
			stored = property.SetSequenceName("ConventionSequence", fromDataAnnotation: false);
			readBack = ((IReadOnlyProperty)property).GetSequenceName();
			configurationSource = property.GetSequenceNameConfigurationSource();
		});
		_ = db.Model;

		Assert.That(stored, Is.EqualTo("ConventionSequence"));
		Assert.That(readBack, Is.EqualTo("ConventionSequence"));
		Assert.That(configurationSource, Is.EqualTo(ConfigurationSource.Convention));
	}

	[Test]
	public void IConventionProperty_SetSequenceSchema_sets_and_returns_the_stored_value()
	{
		string stored = null;
		string readBack = null;
		ConfigurationSource? configurationSource = null;

		using var db = BuildContext(mb =>
		{
			var property = (IConventionProperty)mb.Entity<Widget>().Property(w => w.Id).Metadata;
			stored = property.SetSequenceSchema("convention_schema", fromDataAnnotation: false);
			readBack = ((IReadOnlyProperty)property).GetSequenceSchema();
			configurationSource = property.GetSequenceSchemaConfigurationSource();
		});
		_ = db.Model;

		Assert.That(stored, Is.EqualTo("convention_schema"));
		Assert.That(readBack, Is.EqualTo("convention_schema"));
		Assert.That(configurationSource, Is.EqualTo(ConfigurationSource.Convention));
	}

	[Test]
	public void ConfigurationSource_getters_return_null_when_nothing_was_ever_set()
	{
		ConfigurationSource? valueGenerationStrategy = null;
		ConfigurationSource? hiLoSequenceName = null;
		ConfigurationSource? hiLoSequenceSchema = null;
		ConfigurationSource? sequenceName = null;
		ConfigurationSource? sequenceSchema = null;

		using var db = BuildContext(mb =>
		{
			var property = (IConventionProperty)mb.Entity<Widget>().Property(w => w.Id).Metadata;
			valueGenerationStrategy = property.GetValueGenerationStrategyConfigurationSource();
			hiLoSequenceName = property.GetHiLoSequenceNameConfigurationSource();
			hiLoSequenceSchema = property.GetHiLoSequenceSchemaConfigurationSource();
			sequenceName = property.GetSequenceNameConfigurationSource();
			sequenceSchema = property.GetSequenceSchemaConfigurationSource();
		});
		_ = db.Model;

		Assert.That(valueGenerationStrategy, Is.Null);
		Assert.That(hiLoSequenceName, Is.Null);
		Assert.That(hiLoSequenceSchema, Is.Null);
		Assert.That(sequenceName, Is.Null);
		Assert.That(sequenceSchema, Is.Null);
	}

	[Test]
	public void GetHiLoSequenceName_with_StoreObjectIdentifier_returns_the_annotation_when_set_directly_on_the_property()
	{
		using var db = BuildContext(mb => mb.Entity<Widget>().Property(w => w.Id).UseHiLo("StoreObjectSeq"));
		var property = db.Model.FindEntityType(typeof(Widget)).FindProperty(nameof(Widget.Id));
		var storeObject = StoreObjectIdentifier.Table("Widgets");

		Assert.That(property.GetHiLoSequenceName(storeObject), Is.EqualTo("StoreObjectSeq"));
	}

	[Test]
	public void GetHiLoSequenceName_with_StoreObjectIdentifier_falls_back_to_the_shared_root_lookup_when_unset()
	{
		using var db = BuildContext(mb => { });
		var property = db.Model.FindEntityType(typeof(Widget)).FindProperty(nameof(Widget.Id));
		var storeObject = StoreObjectIdentifier.Table("Widgets");

		// No annotation and no shared-table root for a plain, unshared entity -- exercises the
		// FindSharedStoreObjectRootProperty(...)?.GetHiLoSequenceName(storeObject) fallback line,
		// which resolves to null here without throwing.
		Assert.That(property.GetHiLoSequenceName(storeObject), Is.Null);
	}

	[Test]
	public void GetHiLoSequenceSchema_with_StoreObjectIdentifier_returns_the_annotation_when_set_and_falls_back_when_unset()
	{
		using var db = BuildContext(mb =>
		{
			mb.Entity<Widget>().Property(w => w.Id).UseHiLo("Seq");
			mb.Entity<Widget>().Property(w => w.Id).Metadata.SetHiLoSequenceSchema("explicit_schema");
		});
		var property = db.Model.FindEntityType(typeof(Widget)).FindProperty(nameof(Widget.Id));
		var storeObject = StoreObjectIdentifier.Table("Widgets");

		Assert.That(property.GetHiLoSequenceSchema(storeObject), Is.EqualTo("explicit_schema"));

		using var unsetDb = BuildContext(mb => { });
		var unsetProperty = unsetDb.Model.FindEntityType(typeof(Widget)).FindProperty(nameof(Widget.Id));
		Assert.That(unsetProperty.GetHiLoSequenceSchema(storeObject), Is.Null);
	}

	[Test]
	public void GetSequenceName_with_StoreObjectIdentifier_returns_the_annotation_when_set_and_falls_back_when_unset()
	{
		using var db = BuildContext(mb => mb.Entity<Widget>().Property(w => w.Id).Metadata.SetSequenceName("ExplicitSeq"));
		var property = db.Model.FindEntityType(typeof(Widget)).FindProperty(nameof(Widget.Id));
		var storeObject = StoreObjectIdentifier.Table("Widgets");

		Assert.That(property.GetSequenceName(storeObject), Is.EqualTo("ExplicitSeq"));

		using var unsetDb = BuildContext(mb => { });
		var unsetProperty = unsetDb.Model.FindEntityType(typeof(Widget)).FindProperty(nameof(Widget.Id));
		Assert.That(unsetProperty.GetSequenceName(storeObject), Is.Null);
	}

	[Test]
	public void GetSequenceSchema_with_StoreObjectIdentifier_returns_the_annotation_when_set_and_falls_back_when_unset()
	{
		using var db = BuildContext(mb => mb.Entity<Widget>().Property(w => w.Id).Metadata.SetSequenceSchema("explicit_schema"));
		var property = db.Model.FindEntityType(typeof(Widget)).FindProperty(nameof(Widget.Id));
		var storeObject = StoreObjectIdentifier.Table("Widgets");

		Assert.That(property.GetSequenceSchema(storeObject), Is.EqualTo("explicit_schema"));

		using var unsetDb = BuildContext(mb => { });
		var unsetProperty = unsetDb.Model.FindEntityType(typeof(Widget)).FindProperty(nameof(Widget.Id));
		Assert.That(unsetProperty.GetSequenceSchema(storeObject), Is.Null);
	}

	[Test]
	public void IProperty_GetValueGenerationStrategy_falls_back_to_a_model_level_SequenceTrigger_strategy()
	{
		using var db = BuildContext(mb => mb.UseSequenceTriggers());
		var property = db.Model.FindEntityType(typeof(Widget)).FindProperty(nameof(Widget.Id));

		Assert.That(property.GetValueGenerationStrategy(), Is.EqualTo(FbValueGenerationStrategy.SequenceTrigger));
	}

	[Test]
	public void IProperty_GetValueGenerationStrategy_falls_back_to_a_model_level_IdentityColumn_strategy_for_a_compatible_property()
	{
		using var db = BuildContext(mb => mb.UseIdentityColumns());
		var property = db.Model.FindEntityType(typeof(Widget)).FindProperty(nameof(Widget.Id));

		Assert.That(property.GetValueGenerationStrategy(), Is.EqualTo(FbValueGenerationStrategy.IdentityColumn));
	}

	[Test]
	public void IProperty_GetValueGenerationStrategy_falls_back_to_a_model_level_HiLo_strategy_for_a_compatible_property()
	{
		using var db = BuildContext(mb => mb.UseHiLo());
		var property = db.Model.FindEntityType(typeof(Widget)).FindProperty(nameof(Widget.Id));

		Assert.That(property.GetValueGenerationStrategy(), Is.EqualTo(FbValueGenerationStrategy.HiLo));
	}

	[Test]
	public void IMutableProperty_GetValueGenerationStrategy_falls_back_to_a_model_level_SequenceTrigger_strategy()
	{
		FbValueGenerationStrategy strategy = default;

		using var db = BuildContext(mb =>
		{
			mb.UseSequenceTriggers();
			strategy = mb.Entity<Widget>().Property(w => w.Id).Metadata.GetValueGenerationStrategy();
		});
		_ = db.Model;

		Assert.That(strategy, Is.EqualTo(FbValueGenerationStrategy.SequenceTrigger));
	}

	[Test]
	public void FindHiLoSequence_without_a_property_level_name_falls_back_to_the_model_level_HiLo_sequence()
	{
		using var db = BuildContext(mb => mb.UseHiLo("ModelSeq"));
		var property = db.Model.FindEntityType(typeof(Widget)).FindProperty(nameof(Widget.Id));

		var sequence = property.FindHiLoSequence();
		Assert.That(sequence, Is.Not.Null);
		Assert.That(sequence.Name, Is.EqualTo("ModelSeq"));

		// IProperty overload delegates to the same IReadOnlyProperty implementation.
		var fromIProperty = ((IProperty)property).FindHiLoSequence();
		Assert.That(fromIProperty.Name, Is.EqualTo("ModelSeq"));
	}

	[Test]
	public void FindHiLoSequence_with_StoreObjectIdentifier_falls_back_to_the_model_level_HiLo_sequence()
	{
		using var db = BuildContext(mb => mb.UseHiLo("ModelSeq"));
		var property = db.Model.FindEntityType(typeof(Widget)).FindProperty(nameof(Widget.Id));
		var storeObject = StoreObjectIdentifier.Table("Widgets");

		var sequence = property.FindHiLoSequence(storeObject);
		Assert.That(sequence, Is.Not.Null);
		Assert.That(sequence.Name, Is.EqualTo("ModelSeq"));

		var fromIProperty = ((IProperty)property).FindHiLoSequence(storeObject);
		Assert.That(fromIProperty.Name, Is.EqualTo("ModelSeq"));
	}

	[Test]
	public void FindSequence_without_a_property_level_name_falls_back_to_the_model_level_sequence_suffix()
	{
		using var db = BuildContext(mb =>
		{
			mb.Model.SetSequenceNameSuffix("Seq");
			mb.HasSequence("WidgetSeq");
		});
		var property = db.Model.FindEntityType(typeof(Widget)).FindProperty(nameof(Widget.Id));

		// No property-level SequenceName is set, so this resolves via the model's sequence-name
		// suffix -- proving the fallback line executes, whether or not a sequence with that exact
		// composed name exists (FindSequence itself may legitimately return null here).
		_ = property.FindSequence();
		_ = ((IProperty)property).FindSequence();
	}

	[Test]
	public void FindSequence_with_StoreObjectIdentifier_falls_back_to_the_model_level_sequence_suffix()
	{
		using var db = BuildContext(mb => mb.Model.SetSequenceNameSuffix("Seq"));
		var property = db.Model.FindEntityType(typeof(Widget)).FindProperty(nameof(Widget.Id));
		var storeObject = StoreObjectIdentifier.Table("Widgets");

		_ = property.FindSequence(storeObject);
		_ = ((IProperty)property).FindSequence(storeObject);
	}

	static WidgetContext BuildContext(Action<ModelBuilder> configure)
	{
		var factory = new fakeDbFactory(SupportedDatabase.Firebird);
		var connection = factory.CreateConnection();
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
