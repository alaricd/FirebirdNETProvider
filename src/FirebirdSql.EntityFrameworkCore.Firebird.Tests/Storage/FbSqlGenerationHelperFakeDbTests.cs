using System;
using System.Text;
using FirebirdSql.EntityFrameworkCore.Firebird.Storage.Internal;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.EntityFrameworkCore.Storage;
using NUnit.Framework;
using pengdows.crud.enums;
using pengdows.crud.fakeDb;

namespace FirebirdSql.EntityFrameworkCore.Firebird.Tests.Storage;

// Unit-level exercise of FbSqlGenerationHelper's Firebird-specific query-type/parameter-naming
// helpers, and FbHistoryRepository's __EFMigrationsHistory DDL generation -- both pure text
// generation reachable through a fakeDb-backed context's DI container, no connection opened.
public class FbSqlGenerationHelperFakeDbTests
{
	[Test]
	public void StringLiteralQueryType_returns_a_VARCHAR_sized_to_the_literal_with_a_charset()
	{
		var helper = (FbSqlGenerationHelper)BuildContext().GetService<ISqlGenerationHelper>();

		Assert.That(helper.StringLiteralQueryType("hello"), Is.EqualTo("VARCHAR(5) CHARACTER SET UTF8"));
	}

	[Test]
	public void StringLiteralQueryType_for_an_empty_string_uses_a_minimum_length_of_one()
	{
		var helper = (FbSqlGenerationHelper)BuildContext().GetService<ISqlGenerationHelper>();

		Assert.That(helper.StringLiteralQueryType(""), Is.EqualTo("VARCHAR(1) CHARACTER SET UTF8"));
	}

	[Test]
	public void StringLiteralQueryType_without_unicode_omits_the_charset_clause()
	{
		var helper = (FbSqlGenerationHelper)BuildContext().GetService<ISqlGenerationHelper>();

		Assert.That(helper.StringLiteralQueryType("hello", isUnicode: false), Is.EqualTo("VARCHAR(5)"));
	}

	[Test]
	public void StringParameterQueryType_uses_the_unicode_max_size_when_unicode()
	{
		var helper = (FbSqlGenerationHelper)BuildContext().GetService<ISqlGenerationHelper>();

		Assert.That(helper.StringParameterQueryType(isUnicode: true), Does.StartWith("VARCHAR("));
		Assert.That(helper.StringParameterQueryType(isUnicode: true), Is.Not.EqualTo(helper.StringParameterQueryType(isUnicode: false)));
	}

	[Test]
	public void GenerateBlockParameterName_prefixes_the_name_with_a_colon()
	{
		var helper = (FbSqlGenerationHelper)BuildContext().GetService<ISqlGenerationHelper>();
		var builder = new StringBuilder();

		helper.GenerateBlockParameterName(builder, "p0");

		Assert.That(builder.ToString(), Is.EqualTo(":p0"));
	}

	[Test]
	public void AlternativeStatementTerminator_is_a_tilde()
	{
		var helper = (FbSqlGenerationHelper)BuildContext().GetService<ISqlGenerationHelper>();

		Assert.That(helper.AlternativeStatementTerminator, Is.EqualTo("~"));
	}

	[Test]
	public void GetCreateScript_generates_a_CREATE_TABLE_for_the_migrations_history_table()
	{
		var repository = BuildContext().GetService<IHistoryRepository>();

		var script = repository.GetCreateScript();

		Assert.That(script, Does.Contain("CREATE TABLE"));
		Assert.That(script, Does.Contain("__EFMigrationsHistory"));
	}

	[Test]
	public void GetCreateIfNotExistsScript_wraps_the_create_script_in_an_existence_check()
	{
		var repository = BuildContext().GetService<IHistoryRepository>();

		var script = repository.GetCreateIfNotExistsScript();

		Assert.That(script, Does.Contain("CREATE TABLE"));
		Assert.That(script, Does.Contain("EXECUTE STATEMENT"));
		Assert.That(script, Does.Contain("SQLSTATE"));
	}

	[Test]
	public void LockReleaseBehavior_is_Explicit()
	{
		var repository = BuildContext().GetService<IHistoryRepository>();
		Assert.That(repository.LockReleaseBehavior, Is.EqualTo(LockReleaseBehavior.Explicit));
	}

	[Test]
	public void Idempotent_script_generation_is_not_supported()
	{
		var repository = BuildContext().GetService<IHistoryRepository>();
		Assert.That(() => repository.GetBeginIfExistsScript("20260101000000_Init"), Throws.InstanceOf<NotSupportedException>());
		Assert.That(() => repository.GetBeginIfNotExistsScript("20260101000000_Init"), Throws.InstanceOf<NotSupportedException>());
		Assert.That(() => repository.GetEndIfScript(), Throws.InstanceOf<NotSupportedException>());
	}

	[Test]
	public void AcquireDatabaseLock_creates_the_lock_table_inserts_a_row_and_the_returned_lock_releases_it_on_dispose()
	{
		var factory = new fakeDbFactory(SupportedDatabase.Firebird);
		var connection = (fakeDbConnection)factory.CreateConnection();
		var options = new DbContextOptionsBuilder<PlainContext>().UseFirebird(connection).Options;
		using var db = new PlainContext(options);
		var repository = db.GetService<IHistoryRepository>();

		// 1) CreateLockTableCommand (an EXECUTE BLOCK with no result set) -- ExecuteNonQuery.
		connection.EnqueueNonQueryResult(1);
		// 2) CreateInsertLockCommand -- ExecuteScalar returning ROWS_AFFECTED = 1, so the
		// retry loop (which would otherwise Thread.Sleep) succeeds on its first attempt.
		connection.EnqueueScalarResult(1);

		using var databaseLock = repository.AcquireDatabaseLock();

		Assert.That(databaseLock, Is.Not.Null);

		// Disposing the lock issues the DELETE FROM "...Lock" command releasing it.
		connection.EnqueueNonQueryResult(1);
		databaseLock.Dispose();
	}

	static DbContext BuildContext()
	{
		var factory = new fakeDbFactory(SupportedDatabase.Firebird);
		var connection = factory.CreateConnection();
		var options = new DbContextOptionsBuilder<PlainContext>().UseFirebird(connection).Options;
		return new PlainContext(options);
	}

	sealed class PlainContext(DbContextOptions<PlainContext> options) : DbContext(options);
}
