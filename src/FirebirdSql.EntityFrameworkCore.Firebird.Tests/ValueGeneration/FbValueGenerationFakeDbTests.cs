using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using FirebirdSql.EntityFrameworkCore.Firebird.ValueGeneration.Internal;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.ValueGeneration;
using NUnit.Framework;
using pengdows.crud.enums;
using pengdows.crud.fakeDb;

namespace FirebirdSql.EntityFrameworkCore.Firebird.Tests.ValueGeneration;

// Exercises FbValueGeneratorSelector/FbSequenceValueGeneratorFactory/FbValueGeneratorCache -- the
// pieces responsible for picking (and constructing) the right ValueGenerator for a HiLo-strategy
// property, plus this provider's SequentialGuidValueGenerator/TemporaryGuidValueGenerator choice
// for Guid keys. Most of these tests never open a connection since generator SELECTION and
// CONSTRUCTION only touches metadata -- the exception is the round-trip test below, which drives a
// HiLo generator's actual sequence read (FbSequenceHiLoValueGenerator.GetNewLowValue), previously
// entirely unexercised by this file.
public class FbValueGenerationFakeDbTests
{
	[Test]
	public void TrySelect_a_HiLo_long_property_creates_an_FbSequenceHiLoValueGenerator()
	{
		using var db = BuildContext(mb => mb.Entity<SeqEntity>().Property(e => e.LongId).UseHiLo("LongSeq"));
		var property = db.Model.FindEntityType(typeof(SeqEntity)).FindProperty(nameof(SeqEntity.LongId));
		var selector = db.GetService<IValueGeneratorSelector>();

		var found = selector.TrySelect(property, property.DeclaringType, out var generator);

		Assert.That(found, Is.True);
		Assert.That(generator, Is.InstanceOf<FbSequenceHiLoValueGenerator<long>>());
	}

	[Test]
	public void TrySelect_a_HiLo_int_property_creates_an_FbSequenceHiLoValueGenerator()
	{
		using var db = BuildContext(mb => mb.Entity<SeqEntity>().Property(e => e.IntId).UseHiLo("IntSeq"));
		var property = db.Model.FindEntityType(typeof(SeqEntity)).FindProperty(nameof(SeqEntity.IntId));
		var selector = db.GetService<IValueGeneratorSelector>();

		var found = selector.TrySelect(property, property.DeclaringType, out var generator);

		Assert.That(found, Is.True);
		Assert.That(generator, Is.InstanceOf<FbSequenceHiLoValueGenerator<int>>());
	}

	[Test]
	public void TrySelect_a_HiLo_decimal_property_creates_an_FbSequenceHiLoValueGenerator()
	{
		using var db = BuildContext(mb => mb.Entity<SeqEntity>().Property(e => e.DecimalId).UseHiLo("DecimalSeq"));
		var property = db.Model.FindEntityType(typeof(SeqEntity)).FindProperty(nameof(SeqEntity.DecimalId));
		var selector = db.GetService<IValueGeneratorSelector>();

		var found = selector.TrySelect(property, property.DeclaringType, out var generator);

		Assert.That(found, Is.True);
		Assert.That(generator, Is.InstanceOf<FbSequenceHiLoValueGenerator<decimal>>());
	}

	[Test]
	public void TrySelect_a_HiLo_short_property_creates_an_FbSequenceHiLoValueGenerator()
	{
		using var db = BuildContext(mb => mb.Entity<SeqEntity>().Property(e => e.ShortId).UseHiLo("ShortSeq"));
		var property = db.Model.FindEntityType(typeof(SeqEntity)).FindProperty(nameof(SeqEntity.ShortId));
		var selector = db.GetService<IValueGeneratorSelector>();

		Assert.That(selector.TrySelect(property, property.DeclaringType, out var generator), Is.True);
		Assert.That(generator, Is.InstanceOf<FbSequenceHiLoValueGenerator<short>>());
	}

	[Test]
	public void TrySelect_a_HiLo_byte_property_creates_an_FbSequenceHiLoValueGenerator()
	{
		using var db = BuildContext(mb => mb.Entity<SeqEntity>().Property(e => e.ByteId).UseHiLo("ByteSeq"));
		var property = db.Model.FindEntityType(typeof(SeqEntity)).FindProperty(nameof(SeqEntity.ByteId));
		var selector = db.GetService<IValueGeneratorSelector>();

		Assert.That(selector.TrySelect(property, property.DeclaringType, out var generator), Is.True);
		Assert.That(generator, Is.InstanceOf<FbSequenceHiLoValueGenerator<byte>>());
	}

	[Test]
	public void TrySelect_a_HiLo_uint_property_creates_an_FbSequenceHiLoValueGenerator()
	{
		using var db = BuildContext(mb => mb.Entity<SeqEntity>().Property(e => e.UIntId).UseHiLo("UIntSeq"));
		var property = db.Model.FindEntityType(typeof(SeqEntity)).FindProperty(nameof(SeqEntity.UIntId));
		var selector = db.GetService<IValueGeneratorSelector>();

		Assert.That(selector.TrySelect(property, property.DeclaringType, out var generator), Is.True);
		Assert.That(generator, Is.InstanceOf<FbSequenceHiLoValueGenerator<uint>>());
	}

	[Test]
	public void TrySelect_a_HiLo_ulong_property_creates_an_FbSequenceHiLoValueGenerator()
	{
		using var db = BuildContext(mb => mb.Entity<SeqEntity>().Property(e => e.ULongId).UseHiLo("ULongSeq"));
		var property = db.Model.FindEntityType(typeof(SeqEntity)).FindProperty(nameof(SeqEntity.ULongId));
		var selector = db.GetService<IValueGeneratorSelector>();

		Assert.That(selector.TrySelect(property, property.DeclaringType, out var generator), Is.True);
		Assert.That(generator, Is.InstanceOf<FbSequenceHiLoValueGenerator<ulong>>());
	}

	[Test]
	public void TrySelect_a_HiLo_ushort_property_creates_an_FbSequenceHiLoValueGenerator()
	{
		using var db = BuildContext(mb => mb.Entity<SeqEntity>().Property(e => e.UShortId).UseHiLo("UShortSeq"));
		var property = db.Model.FindEntityType(typeof(SeqEntity)).FindProperty(nameof(SeqEntity.UShortId));
		var selector = db.GetService<IValueGeneratorSelector>();

		Assert.That(selector.TrySelect(property, property.DeclaringType, out var generator), Is.True);
		Assert.That(generator, Is.InstanceOf<FbSequenceHiLoValueGenerator<ushort>>());
	}

	[Test]
	public void TrySelect_a_HiLo_sbyte_property_creates_an_FbSequenceHiLoValueGenerator()
	{
		using var db = BuildContext(mb => mb.Entity<SeqEntity>().Property(e => e.SByteId).UseHiLo("SByteSeq"));
		var property = db.Model.FindEntityType(typeof(SeqEntity)).FindProperty(nameof(SeqEntity.SByteId));
		var selector = db.GetService<IValueGeneratorSelector>();

		Assert.That(selector.TrySelect(property, property.DeclaringType, out var generator), Is.True);
		Assert.That(generator, Is.InstanceOf<FbSequenceHiLoValueGenerator<sbyte>>());
	}

	[Test]
	public void TrySelect_a_HiLo_char_property_creates_an_FbSequenceHiLoValueGenerator()
	{
		using var db = BuildContext(mb => mb.Entity<SeqEntity>().Property(e => e.CharId).UseHiLo("CharSeq"));
		var property = db.Model.FindEntityType(typeof(SeqEntity)).FindProperty(nameof(SeqEntity.CharId));
		var selector = db.GetService<IValueGeneratorSelector>();

		Assert.That(selector.TrySelect(property, property.DeclaringType, out var generator), Is.True);
		Assert.That(generator, Is.InstanceOf<FbSequenceHiLoValueGenerator<char>>());
	}

	// FbValueGeneratorSelector.TrySelect has a fallback branch for when a HiLo property's own CLR
	// type isn't directly supported by FbSequenceValueGeneratorFactory.TryCreate but a
	// ValueConverter's ProviderClrType is. Investigated, not exercised: FbPropertyExtensions.
	// IsCompatibleHiLoColumn (checked by SetValueGenerationStrategy, which UseHiLo always goes
	// through) only allows a property to have the HiLo strategy set at all when its OWN CLR type is
	// already IsInteger() or decimal -- and TryCreate already directly handles every one of those
	// types. So no CLR type can simultaneously (a) pass the compatibility gate and (b) still need
	// the converter fallback -- confirmed by direct reproduction (a float-typed HasConversion<long>
	// HiLo property throws ArgumentException at model-build time, before FbValueGeneratorSelector
	// is ever reached). Genuinely unreachable through any real API path; not forced into a test.

	[Test]
#pragma warning disable CS0618 // Select is [Obsolete] in favor of TrySelect -- deliberately testing the obsolete member.
	public void Select_returns_the_same_generator_TrySelect_would_have_returned()
	{
		using var db = BuildContext(mb => mb.Entity<SeqEntity>().Property(e => e.IntId).UseHiLo("SelectSeq"));
		var property = db.Model.FindEntityType(typeof(SeqEntity)).FindProperty(nameof(SeqEntity.IntId));
		var selector = db.GetService<IValueGeneratorSelector>();

		var generator = selector.Select(property, property.DeclaringType);

		Assert.That(generator, Is.InstanceOf<FbSequenceHiLoValueGenerator<int>>());
	}
#pragma warning restore CS0618

	[Test]
	public void TrySelect_repeated_calls_for_the_same_HiLo_property_reuse_the_cached_sequence_state()
	{
		// Proves FbValueGeneratorCache.GetOrAddSequenceState actually caches: two independent
		// selections for the same property must resolve to the same underlying Sequence metadata
		// rather than each creating a fresh, disconnected generator state.
		using var db = BuildContext(mb => mb.Entity<SeqEntity>().Property(e => e.LongId).UseHiLo("LongSeq"));
		var property = db.Model.FindEntityType(typeof(SeqEntity)).FindProperty(nameof(SeqEntity.LongId));
		var selector = (FbValueGeneratorSelector)db.GetService<IValueGeneratorSelector>();

		var state1 = selector.Cache.GetOrAddSequenceState(property, db.GetService<Microsoft.EntityFrameworkCore.Storage.IRelationalConnection>());
		var state2 = selector.Cache.GetOrAddSequenceState(property, db.GetService<Microsoft.EntityFrameworkCore.Storage.IRelationalConnection>());

		Assert.That(state2, Is.SameAs(state1));
		Assert.That(state1.Sequence.Name, Is.EqualTo("LongSeq"));
	}

	[Test]
	public async Task HiLo_int_generator_produces_a_value_via_a_real_sequence_round_trip()
	{
		using var db = BuildContext(mb => mb.Entity<SeqEntity>().Property(e => e.IntId).UseHiLo("IntSeq"));
		var connection = (fakeDbConnection)db.Database.GetDbConnection();

		// GetNewLowValue() issues a raw "next sequence value" scalar command; fakeDb serves queued
		// scalar results FIFO regardless of the exact SQL text.
		connection.EnqueueScalarResult(1L);
		connection.EnqueueNonQueryResult(1);
		connection.EnqueueReaderResult(new[] { new Dictionary<string, object> { ["Value"] = 1 } }, recordsAffected: 1);

		var entity = new SeqEntity { Id = 1, Name = "widget" };
		db.Add(entity);
		await db.SaveChangesAsync();

		Assert.That(entity.IntId, Is.Not.EqualTo(0));
	}

	[Test]
	public void TrySelect_a_Guid_property_that_can_generate_a_value_uses_the_sequential_generator()
	{
		using var db = BuildContext(mb => mb.Entity<SeqEntity>().Property(e => e.GuidId).ValueGeneratedOnAdd());
		var property = db.Model.FindEntityType(typeof(SeqEntity)).FindProperty(nameof(SeqEntity.GuidId));
		var selector = db.GetService<IValueGeneratorSelector>();

		var found = selector.TrySelect(property, property.DeclaringType, out var generator);

		Assert.That(found, Is.True);
		Assert.That(generator, Is.InstanceOf<SequentialGuidValueGenerator>());
	}

	[Test]
	public void TrySelect_a_Guid_property_that_never_generates_uses_the_temporary_generator()
	{
		using var db = BuildContext(mb => mb.Entity<SeqEntity>().Property(e => e.GuidId).ValueGeneratedOnAdd().Metadata.SetDefaultValueSql("GEN_UUID()"));
		var property = db.Model.FindEntityType(typeof(SeqEntity)).FindProperty(nameof(SeqEntity.GuidId));
		var selector = db.GetService<IValueGeneratorSelector>();

		var found = selector.TrySelect(property, property.DeclaringType, out var generator);

		Assert.That(found, Is.True);
		Assert.That(generator, Is.InstanceOf<TemporaryGuidValueGenerator>());
	}

	static SeqContext BuildContext(Action<ModelBuilder> configure)
	{
		var factory = new fakeDbFactory(SupportedDatabase.Firebird);
		var connection = factory.CreateConnection();
		var options = new DbContextOptionsBuilder<SeqContext>()
			.UseFirebird(connection)
			.ReplaceService<IModelCacheKeyFactory, NeverCacheModelCacheKeyFactory>()
			.Options;
		return new SeqContext(options, configure);
	}

	sealed class SeqContext(DbContextOptions<SeqContext> options, Action<ModelBuilder> configure) : DbContext(options)
	{
		public DbSet<SeqEntity> Entities => Set<SeqEntity>();

		protected override void OnModelCreating(ModelBuilder modelBuilder)
		{
			modelBuilder.Entity<SeqEntity>().Property(e => e.Id).ValueGeneratedNever();
			configure(modelBuilder);
		}
	}

	sealed class SeqEntity
	{
		public int Id { get; set; }
		public long LongId { get; set; }
		public int IntId { get; set; }
		public decimal DecimalId { get; set; }
		public short ShortId { get; set; }
		public byte ByteId { get; set; }
		public uint UIntId { get; set; }
		public ulong ULongId { get; set; }
		public ushort UShortId { get; set; }
		public sbyte SByteId { get; set; }
		public char CharId { get; set; }
		public Guid GuidId { get; set; }
		public string Name { get; set; } = string.Empty;
	}

	sealed class NeverCacheModelCacheKeyFactory : IModelCacheKeyFactory
	{
		public object Create(DbContext context, bool designTime) => new object();
	}
}
