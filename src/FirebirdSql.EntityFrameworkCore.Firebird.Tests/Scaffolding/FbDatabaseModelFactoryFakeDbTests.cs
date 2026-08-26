using System;
using System.Collections.Generic;
using System.Data;
using System.Data.Common;
using FirebirdSql.EntityFrameworkCore.Firebird.Metadata.Internal;
using FirebirdSql.EntityFrameworkCore.Firebird.Scaffolding.Internal;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.EntityFrameworkCore.Scaffolding;
using Microsoft.EntityFrameworkCore.Scaffolding.Metadata;
using NUnit.Framework;
using pengdows.crud.enums;
using pengdows.crud.fakeDb;

namespace FirebirdSql.EntityFrameworkCore.Firebird.Tests.Scaffolding;

// Drives FbDatabaseModelFactory's hand-written rdb$ system-catalog queries end to end through a
// fakeDbConnection queued with rows shaped exactly like Firebird's catalog tables -- no real
// server, but real code: real ADO.NET reads, real column/PK/index/FK reconstruction logic. The
// existing, real-server ScaffoldingTests.cs in this test project covers the same feature against
// an actual database; this file proves the row-parsing logic in isolation.
//
// fakeDb's reader-result queue is FIFO and shared by every command on the connection, so rows
// must be enqueued in the exact order FbDatabaseModelFactory issues its queries: GetTablesQuery
// once, then for each table in turn: GetColumnsQuery, GetPrimaryKeysQuery, GetIndexesQuery,
// GetConstraintsQuery.
public class FbDatabaseModelFactoryFakeDbTests
{
	[Test]
	public void Create_reconstructs_a_table_with_columns_primary_key_index_and_a_self_referencing_foreign_key()
	{
		var factory = new fakeDbFactory(SupportedDatabase.Firebird);
		var connection = (fakeDbConnection)factory.CreateConnection();
		// FbDatabaseModelFactory.MajorVersionNumber comes from parsing DbConnection.ServerVersion
		// via FbServerProperties.ParseServerVersion, which requires this "XX-Vn.n.n.n" shape --
		// fakeDb's default emulated Firebird version ("4.0.0") does not match it and would parse
		// to null, so this must be set explicitly.
		connection.SetServerVersion("WI-V4.0.0.2496 Firebird 4.0");

		// 1) GetTablesQuery -- one table, a real comment (exercises the non-null Comment branch),
		// relation_type = 0 (a table, not a view).
		connection.EnqueueReaderResult(new[]
		{
			new Dictionary<string, object> { ["c0"] = "WIDGETS", ["c1"] = "a table", ["c2"] = 0 }
		});

		// 2) GetColumnsQuery for WIDGETS -- ID is a plain, non-nullable, non-identity column with
		// no domain/charset/collation/default/computed/comment annotations (all the "skip this
		// annotation" branches); NAME exercises every annotation branch at once: identity,
		// charset, collation, domain, blob segment size, default value stripping, computed
		// column, and comment.
		connection.EnqueueReaderResult(new[]
		{
			new Dictionary<string, object>
			{
				["COLUMN_NAME"] = "ID",
				["COLUMN_REQUIRED"] = true,
				["COLUMN_STORE_TYPE"] = "INTEGER",
				["COLUMN_DOMAIN"] = "RDB$INTEGER",
				["CHARACTER_SET_NAME"] = "",
				["COLLATION_NAME"] = "",
				["SEGMENT_LENGTH"] = 0,
				["COLUMN_DEFAULT"] = "",
				["COLUMN_COMPUTED_SOURCE"] = "",
				["COLUMN_COMMENT"] = "",
				["IDENTITY_TYPE"] = -1,
				["IDENTITY_START"] = 1,
				["IDENTITY_INCREMENT"] = 1,
			},
			new Dictionary<string, object>
			{
				["COLUMN_NAME"] = "NAME",
				["COLUMN_REQUIRED"] = false,
				["COLUMN_STORE_TYPE"] = "VARCHAR(100)",
				["COLUMN_DOMAIN"] = "MY_DOMAIN",
				["CHARACTER_SET_NAME"] = "UTF8",
				["COLLATION_NAME"] = "UNICODE",
				["SEGMENT_LENGTH"] = 80,
				["COLUMN_DEFAULT"] = "DEFAULT 'x'",
				["COLUMN_COMPUTED_SOURCE"] = "1+1",
				["COLUMN_COMMENT"] = "a column",
				["IDENTITY_TYPE"] = 1,
				["IDENTITY_START"] = 5,
				["IDENTITY_INCREMENT"] = 10,
			}
		});

		// 3) GetPrimaryKeysQuery -- a single-column PK on ID.
		connection.EnqueueReaderResult(new[]
		{
			new Dictionary<string, object> { ["c0"] = "PK_WIDGETS", ["c1"] = "ID" }
		});

		// 4) GetIndexesQuery -- one row with a null/empty COLUMNS list (the "continue" skip
		// branch) and one real unique, descending index over NAME.
		connection.EnqueueReaderResult(new[]
		{
			new Dictionary<string, object> { ["c0"] = "IX_EMPTY", ["c1"] = 0, ["c2"] = 0, ["c3"] = DBNull.Value },
			new Dictionary<string, object> { ["c0"] = "IX_NAME", ["c1"] = true, ["c2"] = true, ["c3"] = "NAME" }
		});

		// 5) GetConstraintsQuery -- a self-referencing foreign key (WIDGETS.NAME -> WIDGETS.ID),
		// the simplest shape that stays within a single-table scenario, with a CASCADE delete
		// rule to exercise ConvertToReferentialAction's mapped branch.
		connection.EnqueueReaderResult(new[]
		{
			new Dictionary<string, object>
			{
				["c0"] = "FK_SELF",
				["c1"] = "WIDGETS",
				["c2"] = "WIDGETS",
				["c3"] = "NAME|ID",
				["c4"] = "CASCADE",
			}
		});

		var databaseModel = new FbDatabaseModelFactory(factory).Create(connection, new DatabaseModelFactoryOptions());

		Assert.That(databaseModel.Tables, Has.Count.EqualTo(1));
		var table = databaseModel.Tables[0];
		Assert.That(table.Name, Is.EqualTo("WIDGETS"));
		Assert.That(table.Comment, Is.EqualTo("a table"));

		Assert.That(table.Columns, Has.Count.EqualTo(2));
		var idColumn = table.Columns[0];
		Assert.That(idColumn.Name, Is.EqualTo("ID"));
		Assert.That(idColumn.IsNullable, Is.False);
		Assert.That(idColumn[FbAnnotationNames.DomainName], Is.Null);

		var nameColumn = table.Columns[1];
		Assert.That(nameColumn.Name, Is.EqualTo("NAME"));
		Assert.That(nameColumn.IsNullable, Is.True);
		Assert.That(nameColumn.DefaultValueSql, Is.EqualTo("'x'"));
		Assert.That(nameColumn.ComputedColumnSql, Is.EqualTo("1+1"));
		Assert.That(nameColumn.Comment, Is.EqualTo("a column"));
		Assert.That(nameColumn[FbAnnotationNames.CharacterSet], Is.EqualTo("UTF8"));
		Assert.That(nameColumn[FbAnnotationNames.DomainName], Is.EqualTo("MY_DOMAIN"));
		Assert.That(nameColumn[FbAnnotationNames.BlobSegmentSize], Is.EqualTo(80));
		Assert.That(nameColumn[FbAnnotationNames.IdentityType], Is.EqualTo(1));
		Assert.That(nameColumn[FbAnnotationNames.IdentityStart], Is.EqualTo(5));
		Assert.That(nameColumn[FbAnnotationNames.IdentityIncrement], Is.EqualTo(10));

		Assert.That(table.PrimaryKey, Is.Not.Null);
		Assert.That(table.PrimaryKey.Columns, Has.Count.EqualTo(1));
		Assert.That(table.PrimaryKey.Columns[0].Name, Is.EqualTo("ID"));

		Assert.That(table.Indexes, Has.Count.EqualTo(1));
		var index = table.Indexes[0];
		Assert.That(index.Name, Is.EqualTo("IX_NAME"));
		Assert.That(index.IsUnique, Is.True);
		Assert.That(index.Columns, Has.Count.EqualTo(1));
		Assert.That(index.IsDescending, Is.Not.Null);
		Assert.That(index.IsDescending[0], Is.True);

		Assert.That(table.ForeignKeys, Has.Count.EqualTo(1));
		var fk = table.ForeignKeys[0];
		Assert.That(fk.Name, Is.EqualTo("FK_SELF"));
		Assert.That(fk.PrincipalTable, Is.SameAs(table));
		Assert.That(fk.OnDelete, Is.EqualTo(ReferentialAction.Cascade));
	}

	[Test]
	public void Create_reconstructs_a_view_with_no_columns_keys_indexes_or_constraints()
	{
		var factory = new fakeDbFactory(SupportedDatabase.Firebird);
		var connection = (fakeDbConnection)factory.CreateConnection();
		connection.SetServerVersion("WI-V4.0.0.2496 Firebird 4.0");

		// relation_type = 1 -> DatabaseView; empty description -> null Comment.
		connection.EnqueueReaderResult(new[]
		{
			new Dictionary<string, object> { ["c0"] = "MY_VIEW", ["c1"] = "", ["c2"] = 1 }
		});
		connection.EnqueueReaderResult(Array.Empty<Dictionary<string, object>>());
		connection.EnqueueReaderResult(Array.Empty<Dictionary<string, object>>());
		connection.EnqueueReaderResult(Array.Empty<Dictionary<string, object>>());
		connection.EnqueueReaderResult(Array.Empty<Dictionary<string, object>>());

		var databaseModel = new FbDatabaseModelFactory(factory).Create(connection, new DatabaseModelFactoryOptions());

		var table = databaseModel.Tables[0];
		Assert.That(table, Is.InstanceOf<DatabaseView>());
		Assert.That(table.Comment, Is.Null);
		Assert.That(table.Columns, Is.Empty);
		Assert.That(table.PrimaryKey, Is.Null);
	}

	[Test]
	public void Create_from_a_connection_string_opens_and_closes_a_connection_from_the_factory()
	{
		var factory = new fakeDbFactory(SupportedDatabase.Firebird);

		// Pre-seeds the connection fakeDbFactory.CreateConnection() will hand back to
		// FbDatabaseModelFactory.Create(string, options), which builds its own connection
		// internally and so cannot otherwise be configured by the caller.
		var connection = (fakeDbConnection)factory.CreateConnection();
		connection.SetServerVersion("WI-V4.0.0.2496 Firebird 4.0");
		connection.EnqueueReaderResult(Array.Empty<Dictionary<string, object>>());
		factory.Connections.Add(connection);

		var databaseModel = new FbDatabaseModelFactory(factory).Create("server=fake;database=fake;", new DatabaseModelFactoryOptions());

		Assert.That(databaseModel.Tables, Is.Empty);
		Assert.That(connection.State, Is.EqualTo(ConnectionState.Closed));
	}

	[Test]
	public void Create_leaves_an_already_open_connection_open()
	{
		var factory = new fakeDbFactory(SupportedDatabase.Firebird);
		var connection = (fakeDbConnection)factory.CreateConnection();
		connection.SetServerVersion("WI-V4.0.0.2496 Firebird 4.0");
		connection.EnqueueReaderResult(Array.Empty<Dictionary<string, object>>());
		connection.Open();

		new FbDatabaseModelFactory(factory).Create((DbConnection)connection, new DatabaseModelFactoryOptions());

		Assert.That(connection.State, Is.EqualTo(ConnectionState.Open));
	}

	[Test]
	public void Create_only_includes_tables_matching_the_requested_table_filter()
	{
		var factory = new fakeDbFactory(SupportedDatabase.Firebird);
		var connection = (fakeDbConnection)factory.CreateConnection();
		connection.SetServerVersion("WI-V4.0.0.2496 Firebird 4.0");

		connection.EnqueueReaderResult(new[]
		{
			new Dictionary<string, object> { ["c0"] = "WIDGETS", ["c1"] = "", ["c2"] = 0 },
			new Dictionary<string, object> { ["c0"] = "GADGETS", ["c1"] = "", ["c2"] = 0 }
		});
		// One GetColumns/GetPrimaryKeys/GetIndexes/GetConstraints round trip per table returned
		// above, regardless of the requested filter -- filtering happens after fetching.
		for (var i = 0; i < 2; i++)
		{
			connection.EnqueueReaderResult(Array.Empty<Dictionary<string, object>>());
			connection.EnqueueReaderResult(Array.Empty<Dictionary<string, object>>());
			connection.EnqueueReaderResult(Array.Empty<Dictionary<string, object>>());
			connection.EnqueueReaderResult(Array.Empty<Dictionary<string, object>>());
		}

		var options = new DatabaseModelFactoryOptions(new[] { "WIDGETS" });
		var databaseModel = new FbDatabaseModelFactory(factory).Create(connection, options);

		Assert.That(databaseModel.Tables, Has.Count.EqualTo(1));
		Assert.That(databaseModel.Tables[0].Name, Is.EqualTo("WIDGETS"));
	}
}
