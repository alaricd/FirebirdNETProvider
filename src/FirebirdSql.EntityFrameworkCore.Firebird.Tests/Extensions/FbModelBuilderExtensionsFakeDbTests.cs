using System;
using FirebirdSql.EntityFrameworkCore.Firebird.Metadata;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using NUnit.Framework;
using pengdows.crud.enums;
using pengdows.crud.fakeDb;

namespace FirebirdSql.EntityFrameworkCore.Firebird.Tests.Extensions;

// Covers the IConventionModelBuilder-typed overloads in Extensions/FbModelBuilderExtensions.cs
// (HasHiLoSequence/CanSetHiLoSequence/HasValueGenerationStrategy) that FbFluentExtensionsFakeDbTests.cs
// doesn't reach -- it only exercises the mutable ModelBuilder-typed UseIdentityColumns/
// UseSequenceTriggers/UseHiLo. ((IConventionModel)modelBuilder.Model).Builder is how a
// IConventionModelBuilder is obtained mid-OnModelCreating; per the note in
// FbPropertyExtensionsFakeDbTests.cs, `_ = db.Model;` after BuildContext(...) is required to force
// the lazy `configure` callback to actually run.
public class FbModelBuilderExtensionsFakeDbTests
{
	[Test]
	public void HasHiLoSequence_sets_the_sequence_name_and_creates_the_backing_sequence()
	{
		IConventionSequenceBuilder result = null;

		using var db = BuildContext(mb =>
		{
			var conventionModelBuilder = ((IConventionModel)mb.Model).Builder;
			result = conventionModelBuilder.HasHiLoSequence("MySeq", fromDataAnnotation: false);
		});
		_ = db.Model;

		Assert.That(result, Is.Not.Null);
		Assert.That(db.Model.GetHiLoSequenceName(), Is.EqualTo("MySeq"));
		Assert.That(db.Model.FindSequence("MySeq"), Is.Not.Null);
	}

	[Test]
	public void HasHiLoSequence_with_a_null_name_returns_null_without_creating_a_sequence()
	{
		IConventionSequenceBuilder result = null;

		using var db = BuildContext(mb =>
		{
			var conventionModelBuilder = ((IConventionModel)mb.Model).Builder;
			result = conventionModelBuilder.HasHiLoSequence(null, fromDataAnnotation: false);
		});
		_ = db.Model;

		Assert.That(result, Is.Null);
	}

	[Test]
	public void CanSetHiLoSequence_reflects_whether_the_configuration_source_can_override_an_existing_value()
	{
		bool canOverrideWithHigherSource = false;
		bool canOverrideWithLowerSource = true;

		using var db = BuildContext(mb =>
		{
			var conventionModelBuilder = ((IConventionModel)mb.Model).Builder;

			// Establish the annotation from a DataAnnotation-level source first.
			conventionModelBuilder.HasHiLoSequence("First", fromDataAnnotation: true);

			// A Convention-level attempt (fromDataAnnotation: false) cannot override a
			// DataAnnotation-sourced value -- Convention is the lowest-priority configuration
			// source in EF Core's hierarchy.
			canOverrideWithLowerSource = conventionModelBuilder.CanSetHiLoSequence("Second", fromDataAnnotation: false);

			// A second DataAnnotation-level attempt (same source) is allowed to override itself.
			canOverrideWithHigherSource = conventionModelBuilder.CanSetHiLoSequence("Second", fromDataAnnotation: true);
		});
		_ = db.Model;

		Assert.That(canOverrideWithLowerSource, Is.False);
		Assert.That(canOverrideWithHigherSource, Is.True);
	}

	[Test]
	public void HasHiLoSequence_returns_null_when_a_higher_priority_source_already_owns_the_annotation()
	{
		IConventionSequenceBuilder secondAttempt = null;

		using var db = BuildContext(mb =>
		{
			var conventionModelBuilder = ((IConventionModel)mb.Model).Builder;
			conventionModelBuilder.HasHiLoSequence("First", fromDataAnnotation: true);
			secondAttempt = conventionModelBuilder.HasHiLoSequence("Second", fromDataAnnotation: false);
		});
		_ = db.Model;

		Assert.That(secondAttempt, Is.Null);
		Assert.That(db.Model.GetHiLoSequenceName(), Is.EqualTo("First"));
	}

	[Test]
	public void HasValueGenerationStrategy_on_IConventionModelBuilder_sets_the_strategy_for_every_strategy_value()
	{
		using var db = BuildContext(mb =>
		{
			var conventionModelBuilder = ((IConventionModel)mb.Model).Builder;

			// Each call below reaches a different combination of the three
			// `if (valueGenerationStrategy != X)` lines in HasValueGenerationStrategy.
			Assert.That(conventionModelBuilder.HasValueGenerationStrategy(FbValueGenerationStrategy.IdentityColumn, fromDataAnnotation: true), Is.Not.Null);
			Assert.That(conventionModelBuilder.HasValueGenerationStrategy(FbValueGenerationStrategy.SequenceTrigger, fromDataAnnotation: true), Is.Not.Null);
			Assert.That(conventionModelBuilder.HasValueGenerationStrategy(FbValueGenerationStrategy.HiLo, fromDataAnnotation: true), Is.Not.Null);
			Assert.That(conventionModelBuilder.HasValueGenerationStrategy(null, fromDataAnnotation: true), Is.Not.Null);
		});
		_ = db.Model;
	}

	[Test]
	public void HasValueGenerationStrategy_on_IConventionModelBuilder_returns_null_when_it_cannot_override_the_existing_source()
	{
		IConventionModelBuilder secondAttempt = null;

		using var db = BuildContext(mb =>
		{
			var conventionModelBuilder = ((IConventionModel)mb.Model).Builder;
			conventionModelBuilder.HasValueGenerationStrategy(FbValueGenerationStrategy.IdentityColumn, fromDataAnnotation: true);
			secondAttempt = conventionModelBuilder.HasValueGenerationStrategy(FbValueGenerationStrategy.HiLo, fromDataAnnotation: false);
		});
		_ = db.Model;

		Assert.That(secondAttempt, Is.Null);
		Assert.That(db.Model.GetValueGenerationStrategy(), Is.EqualTo(FbValueGenerationStrategy.IdentityColumn));
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
	}
}
