using System;
using System.Linq;
using Microsoft.EntityFrameworkCore;
using NUnit.Framework;
using pengdows.crud.enums;
using pengdows.crud.fakeDb;

namespace FirebirdSql.EntityFrameworkCore.Firebird.Tests.Query;

// Drives FbQuerySqlGenerator's own overrides (bitwise operators, MOD, ROWS paging, ordering
// pseudo-FROM handling, explicit parameter/literal typing) through IQueryable.ToQueryString() --
// pure SQL text generation from a LINQ expression tree, no connection ever opened.
public class FbQuerySqlGeneratorFakeDbTests
{
	[Test]
	public void Bitwise_complement_on_a_non_bool_translates_to_BIN_NOT()
	{
		using var db = new WidgetContext(Options());
		var sql = db.Widgets.Select(w => ~w.LongValue).ToQueryString();
		Assert.That(sql, Does.Contain("BIN_NOT"));
	}

	[Test]
	public void Modulo_translates_to_MOD()
	{
		using var db = new WidgetContext(Options());
		var sql = db.Widgets.Select(w => w.LongValue % 3).ToQueryString();
		Assert.That(sql, Does.Contain("MOD("));
	}

	[Test]
	public void Bitwise_And_on_a_non_bool_translates_to_BIN_AND()
	{
		using var db = new WidgetContext(Options());
		var sql = db.Widgets.Select(w => w.LongValue & 3).ToQueryString();
		Assert.That(sql, Does.Contain("BIN_AND"));
	}


	[Test]
	public void Bitwise_Or_on_a_non_bool_translates_to_BIN_OR()
	{
		using var db = new WidgetContext(Options());
		var sql = db.Widgets.Select(w => w.LongValue | 3).ToQueryString();
		Assert.That(sql, Does.Contain("BIN_OR"));
	}


	[Test]
	public void Bitwise_Xor_on_a_non_bool_translates_to_BIN_XOR()
	{
		using var db = new WidgetContext(Options());
		var sql = db.Widgets.Select(w => w.LongValue ^ 3).ToQueryString();
		Assert.That(sql, Does.Contain("BIN_XOR"));
	}

	[Test]
	public void Bitwise_Xor_on_bool_operands_translates_to_an_IIF_BIN_XOR_expression()
	{
		using var db = new WidgetContext(Options());
		var sql = db.Widgets.Select(w => w.BoolValue ^ w.BoolValue).ToQueryString();
		Assert.That(sql, Does.Contain("BIN_XOR"));
		Assert.That(sql, Does.Contain("IIF"));
	}



	[Test]
	public void String_concatenation_translates_to_the_double_pipe_operator()
	{
		using var db = new WidgetContext(Options());
		var suffix = "!";
		var sql = db.Widgets.Select(w => w.Name + suffix).ToQueryString();
		Assert.That(sql, Does.Contain("||"));
	}

	[Test]
	public void Take_alone_translates_to_ROWS_n()
	{
		using var db = new WidgetContext(Options());
		var sql = db.Widgets.OrderBy(w => w.Id).Take(5).ToQueryString();
		Assert.That(sql, Does.Contain("ROWS ("));
	}

	[Test]
	public void Skip_and_Take_together_translate_to_a_ROWS_range()
	{
		using var db = new WidgetContext(Options());
		var sql = db.Widgets.OrderBy(w => w.Id).Skip(10).Take(5).ToQueryString();
		Assert.That(sql, Does.Contain("ROWS ("));
		Assert.That(sql, Does.Contain(" TO ("));
	}

	[Test]
	public void Skip_alone_translates_to_an_open_ended_ROWS_range()
	{
		using var db = new WidgetContext(Options());
		var sql = db.Widgets.OrderBy(w => w.Id).Skip(10).ToQueryString();
		Assert.That(sql, Does.Contain("ROWS ("));
		Assert.That(sql, Does.Contain(long.MaxValue.ToString()));
	}

	[Test]
	public void OrderBy_a_real_column_emits_an_ORDER_BY_clause()
	{
		using var db = new WidgetContext(Options());
		var sql = db.Widgets.OrderBy(w => w.Name).ToQueryString();
		Assert.That(sql, Does.Contain("ORDER BY"));
	}

	[Test]
	public void OrderBy_a_constant_with_Take_keeps_the_ordering_and_uses_a_pseudo_FROM_subquery()
	{
		// With no Limit/Offset, GenerateOrderings strips constant/parameter orderings entirely --
		// adding Take forces the ordering to survive, hitting VisitOrdering's "(SELECT 1 FROM
		// RDB$DATABASE)" branch for a constant ordering key, which in turn calls
		// GeneratePseudoFromClause.
		using var db = new WidgetContext(Options());
		var sql = db.Widgets.OrderBy(w => 1).Take(5).ToQueryString();
		Assert.That(sql, Does.Contain("ORDER BY"));
		Assert.That(sql, Does.Contain("RDB$DATABASE"));
	}

	[Test]
	public void ExplicitParameterTypes_wraps_bound_parameters_in_a_CAST()
	{
		var factory = new fakeDbFactory(SupportedDatabase.Firebird);
		var connection = factory.CreateConnection();
		var options = new DbContextOptionsBuilder<WidgetContext>()
			.UseFirebird(connection, fb => fb.WithExplicitParameterTypes())
			.Options;
		using var db = new WidgetContext(options);

		var value = 5L;
		var sql = db.Widgets.Where(w => w.LongValue == value).ToQueryString();

		Assert.That(sql, Does.Contain("CAST(@"));
	}

	[Test]
	public void ExplicitStringLiteralTypes_wraps_inline_string_literals_in_a_CAST()
	{
		var factory = new fakeDbFactory(SupportedDatabase.Firebird);
		var connection = factory.CreateConnection();
		var options = new DbContextOptionsBuilder<WidgetContext>()
			.UseFirebird(connection, fb => fb.WithExplicitStringLiteralTypes())
			.Options;
		using var db = new WidgetContext(options);

		// An inline literal (not a captured variable) so EF embeds it as a constant rather than
		// parameterizing it, exercising VisitSqlConstant's explicit-literal-type branch.
		var sql = db.Widgets.Where(w => w.Name == "Ada").ToQueryString();

		Assert.That(sql, Does.Contain("CAST(_UTF8'Ada'"));
	}

	[Test]
	public void OrderByDescending_appends_DESC()
	{
		using var db = new WidgetContext(Options());
		var sql = db.Widgets.OrderByDescending(w => w.Id).ToQueryString();
		Assert.That(sql, Does.Contain("DESC"));
	}

	[Test]
	public void Correlated_Any_translates_to_an_EXISTS_subquery_with_a_dummy_projection()
	{
		using var db = new WidgetContext(Options());
		var sql = db.Widgets.Where(w => db.Widgets.Any(x => x.Name == w.Name && x.Id != w.Id)).ToQueryString();
		Assert.That(sql, Does.Contain("EXISTS"));
	}

	static DbContextOptions<WidgetContext> Options()
	{
		var factory = new fakeDbFactory(SupportedDatabase.Firebird);
		var connection = factory.CreateConnection();
		return new DbContextOptionsBuilder<WidgetContext>().UseFirebird(connection).Options;
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
		public string Name { get; set; } = string.Empty;
		public long LongValue { get; set; }
		public bool BoolValue { get; set; }
		public bool OtherFlag { get; set; }
	}
}
