using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using NUnit.Framework;
using pengdows.crud.enums;
using pengdows.crud.fakeDb;

namespace FirebirdSql.EntityFrameworkCore.Firebird.Tests.Storage;

// Drives the ConfigureParameter branches of the Firebird-specific type mappings -- the ADO.NET
// DbParameter.DbType assignment that only runs when a real DbCommand/DbParameter is actually built
// for execution, which IQueryable.ToQueryString() alone does not exercise (that produces printable
// SQL text without necessarily populating a real parameter). These tests execute real queries
// against fakeDb with a captured local variable as the predicate value -- which is what forces EF
// to bind a real parameter instead of inlining a literal -- and assert both that the query
// completes (proving ConfigureParameter didn't throw for an unmapped FbDbType) and that the
// correct value was actually bound.
public class FbTypeMappingParameterFakeDbTests
{
	[Test]
	public async Task Guid_parameter_binds_via_FbGuidTypeMapping_ConfigureParameter()
	{
		var factory = new fakeDbFactory(SupportedDatabase.Firebird);
		var connection = (fakeDbConnection)factory.CreateConnection();
		using var db = new WidgetContext(new DbContextOptionsBuilder<WidgetContext>().UseFirebird(connection).Options);

		// Column order: the primary key (Id) first, then the remaining columns alphabetically --
		// the physical position EF's shaper reads by (fixed at query compile time), confirmed by
		// the equivalent finding in FakeDbEfInteropTests.cs.
		var id = Guid.Parse("11111111-2222-3333-4444-555555555555");
		connection.EnqueueReaderResult(new[]
		{
			new Dictionary<string, object> { ["Id"] = 1, ["CreatedAt"] = DateTime.UtcNow, ["UniqueId"] = id }
		});

		var results = await db.Widgets.Where(w => w.UniqueId == id).ToListAsync();

		Assert.That(results, Has.Count.EqualTo(1));
		Assert.That(results[0].UniqueId, Is.EqualTo(id));

		var command = connection.ExecutedReaderCommands.Single();
		var bound = command.Parameters.Single(p => Equals(p.Value, id));
		Assert.That(bound.Value, Is.EqualTo(id));
	}

	[Test]
	public async Task DateTime_parameter_mapped_to_TIMESTAMP_binds_via_FbDateTimeTypeMapping_ConfigureParameter()
	{
		var factory = new fakeDbFactory(SupportedDatabase.Firebird);
		var connection = (fakeDbConnection)factory.CreateConnection();
		using var db = new WidgetContext(new DbContextOptionsBuilder<WidgetContext>().UseFirebird(connection).Options);

		var createdAt = new DateTime(2020, 6, 15, 8, 30, 0);
		connection.EnqueueReaderResult(new[]
		{
			new Dictionary<string, object> { ["Id"] = 1, ["CreatedAt"] = createdAt, ["UniqueId"] = Guid.Empty }
		});

		var results = await db.Widgets.Where(w => w.CreatedAt == createdAt).ToListAsync();

		Assert.That(results, Has.Count.EqualTo(1));

		var command = connection.ExecutedReaderCommands.Single();
		var bound = command.Parameters.Single(p => Equals(p.Value, createdAt));
		Assert.That(bound.Value, Is.EqualTo(createdAt));
	}

	[Test]
	public async Task DateTime_parameter_mapped_to_DATE_column_type_binds_via_FbDateTimeTypeMapping_ConfigureParameter()
	{
		var factory = new fakeDbFactory(SupportedDatabase.Firebird);
		var connection = (fakeDbConnection)factory.CreateConnection();
		using var db = new DateOnlyColumnContext(new DbContextOptionsBuilder<DateOnlyColumnContext>().UseFirebird(connection).Options);

		var occurredOn = new DateTime(2020, 6, 15);
		connection.EnqueueReaderResult(new[]
		{
			new Dictionary<string, object> { ["Id"] = 1, ["OccurredOn"] = occurredOn }
		});

		var results = await db.Events.Where(e => e.OccurredOn == occurredOn).ToListAsync();

		Assert.That(results, Has.Count.EqualTo(1));

		var command = connection.ExecutedReaderCommands.Single();
		var bound = command.Parameters.Single(p => Equals(p.Value, occurredOn));
		Assert.That(bound.Value, Is.EqualTo(occurredOn));
	}

	[Test]
	public async Task TimeSpan_parameter_binds_via_FbTimeSpanTypeMapping_ConfigureParameter()
	{
		var factory = new fakeDbFactory(SupportedDatabase.Firebird);
		var connection = (fakeDbConnection)factory.CreateConnection();
		using var db = new TimeSpanContext(new DbContextOptionsBuilder<TimeSpanContext>().UseFirebird(connection).Options);

		var duration = new TimeSpan(1, 2, 3);
		connection.EnqueueReaderResult(new[]
		{
			new Dictionary<string, object> { ["Id"] = 1, ["Duration"] = duration }
		});

		var results = await db.Shifts.Where(s => s.Duration == duration).ToListAsync();

		Assert.That(results, Has.Count.EqualTo(1));

		var command = connection.ExecutedReaderCommands.Single();
		var bound = command.Parameters.Single(p => Equals(p.Value, duration));
		Assert.That(bound.Value, Is.EqualTo(duration));
	}

	sealed class TimeSpanContext(DbContextOptions<TimeSpanContext> options) : DbContext(options)
	{
		public DbSet<Shift> Shifts => Set<Shift>();

		protected override void OnModelCreating(ModelBuilder modelBuilder)
		{
			modelBuilder.Entity<Shift>().Property(s => s.Id).ValueGeneratedNever();
		}
	}

	sealed class Shift
	{
		public int Id { get; set; }
		public TimeSpan Duration { get; set; }
	}

	sealed class WidgetContext(DbContextOptions<WidgetContext> options) : DbContext(options)
	{
		public DbSet<Widget> Widgets => Set<Widget>();

		protected override void OnModelCreating(ModelBuilder modelBuilder)
		{
			modelBuilder.Entity<Widget>().Property(w => w.Id).ValueGeneratedNever();
		}
	}

	sealed class Widget
	{
		public int Id { get; set; }
		public Guid UniqueId { get; set; }
		public DateTime CreatedAt { get; set; }
	}

	sealed class DateOnlyColumnContext(DbContextOptions<DateOnlyColumnContext> options) : DbContext(options)
	{
		public DbSet<Event> Events => Set<Event>();

		protected override void OnModelCreating(ModelBuilder modelBuilder)
		{
			modelBuilder.Entity<Event>(e =>
			{
				e.Property(x => x.Id).ValueGeneratedNever();
				e.Property(x => x.OccurredOn).HasColumnType("DATE");
			});
		}
	}

	sealed class Event
	{
		public int Id { get; set; }
		public DateTime OccurredOn { get; set; }
	}
}
