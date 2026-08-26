using System;
using System.Collections.Generic;
using System.Linq;
using FirebirdSql.EntityFrameworkCore.Firebird.Metadata;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.EntityFrameworkCore.Migrations.Operations;
using NUnit.Framework;
using pengdows.crud.enums;
using pengdows.crud.fakeDb;

namespace FirebirdSql.EntityFrameworkCore.Firebird.Tests.Migrations;

// Drives FbMigrationsSqlGenerator directly through IMigrationsSqlGenerator.Generate() -- pure
// client-side DDL text generation from MigrationOperation objects, with no connection ever opened
// and no command executed. Distinct from the existing, real-server MigrationsTests.cs in this same
// folder, which applies migrations end to end against a live database; this file only proves what
// SQL text this provider emits for each operation shape.
public class FbMigrationsSqlGeneratorFakeDbTests
{
	[Test]
	public void CreateTable_generates_a_CREATE_TABLE_statement()
	{
		var operation = new CreateTableOperation
		{
			Name = "Widgets",
			Columns =
			{
				new AddColumnOperation { Name = "Id", ClrType = typeof(int), ColumnType = "INTEGER", IsNullable = false, Table = "Widgets" }
			}
		};

		var sql = Generate(operation);

		Assert.That(sql, Does.Contain("CREATE TABLE"));
		Assert.That(sql, Does.Contain("Widgets"));
	}

	[Test]
	public void DropTable_generates_a_DROP_TABLE_statement()
	{
		var sql = Generate(new DropTableOperation { Name = "Widgets" });
		Assert.That(sql, Does.Contain("DROP TABLE"));
	}

	[Test]
	public void RenameTable_is_not_supported_by_Firebird()
	{
		Assert.That(
			() => Generate(new RenameTableOperation { Name = "Widgets", NewName = "Gadgets" }),
			Throws.InstanceOf<NotSupportedException>());
	}

	[Test]
	public void AddColumn_generates_an_ALTER_TABLE_ADD_statement()
	{
		var operation = new AddColumnOperation
		{
			Table = "Widgets",
			Name = "Label",
			ClrType = typeof(string),
			ColumnType = "VARCHAR(100)",
			IsNullable = true
		};

		var sql = Generate(operation);

		Assert.That(sql, Does.Contain("ALTER TABLE"));
		Assert.That(sql, Does.Contain("Label"));
	}

	[Test]
	public void DropColumn_generates_an_ALTER_TABLE_DROP_statement()
	{
		var sql = Generate(new DropColumnOperation { Table = "Widgets", Name = "Label" });

		Assert.That(sql, Does.Contain("ALTER TABLE"));
		Assert.That(sql, Does.Contain("DROP"));
		Assert.That(sql, Does.Contain("Label"));
	}

	[Test]
	public void RenameColumn_generates_an_ALTER_COLUMN_TO_statement()
	{
		var operation = new RenameColumnOperation { Table = "Widgets", Name = "Label", NewName = "Title" };

		var sql = Generate(operation);

		Assert.That(sql, Does.Contain("ALTER COLUMN"));
		Assert.That(sql, Does.Contain(" TO "));
	}

	[Test]
	public void AlterColumn_generates_DROP_NOT_NULL_and_TYPE_statements()
	{
		var operation = new AlterColumnOperation
		{
			Table = "Widgets",
			Name = "Label",
			ClrType = typeof(string),
			ColumnType = "VARCHAR(200)",
			IsNullable = true,
			OldColumn = new AddColumnOperation { ClrType = typeof(string), ColumnType = "VARCHAR(100)", IsNullable = true }
		};

		var sql = Generate(operation);

		Assert.That(sql, Does.Contain("DROP NOT NULL"));
		Assert.That(sql, Does.Contain("TYPE"));
		Assert.That(sql, Does.Contain("VARCHAR(200)"));
	}

	[Test]
	public void AlterColumn_removing_identity_throws()
	{
		var oldColumn = new AddColumnOperation { ClrType = typeof(int), ColumnType = "INTEGER", IsNullable = false };
		oldColumn[FbAnnotationName] = FbValueGenerationStrategy.IdentityColumn;

		var operation = new AlterColumnOperation
		{
			Table = "Widgets",
			Name = "Id",
			ClrType = typeof(int),
			ColumnType = "INTEGER",
			IsNullable = false,
			OldColumn = oldColumn
		};

		Assert.That(() => Generate(operation), Throws.InstanceOf<InvalidOperationException>());
	}

	[Test]
	public void CreateIndex_generates_a_CREATE_INDEX_statement()
	{
		var operation = new CreateIndexOperation
		{
			Name = "IX_Widgets_Label",
			Table = "Widgets",
			Columns = new[] { "Label" },
			IsUnique = false
		};

		var sql = Generate(operation);

		Assert.That(sql, Does.Contain("CREATE"));
		Assert.That(sql, Does.Contain("INDEX"));
		Assert.That(sql, Does.Contain("IX_Widgets_Label"));
	}

	[Test]
	public void CreateIndex_unique_generates_a_CREATE_UNIQUE_INDEX_statement()
	{
		var operation = new CreateIndexOperation
		{
			Name = "IX_Widgets_Label",
			Table = "Widgets",
			Columns = new[] { "Label" },
			IsUnique = true
		};

		var sql = Generate(operation);

		Assert.That(sql, Does.Contain("UNIQUE"));
	}

	[Test]
	public void CreateIndex_with_mixed_column_ordering_is_not_supported()
	{
		var operation = new CreateIndexOperation
		{
			Name = "IX_Widgets_Label",
			Table = "Widgets",
			Columns = new[] { "Label", "Name" },
			IsDescending = new[] { true, false }
		};

		Assert.That(() => Generate(operation), Throws.InstanceOf<NotSupportedException>());
	}

	[Test]
	public void DropIndex_generates_a_DROP_INDEX_statement()
	{
		var sql = Generate(new DropIndexOperation { Name = "IX_Widgets_Label", Table = "Widgets" });

		Assert.That(sql, Does.Contain("DROP"));
		Assert.That(sql, Does.Contain("INDEX"));
	}

	[Test]
	public void RenameIndex_is_not_supported_by_Firebird()
	{
		var operation = new RenameIndexOperation { Name = "IX_Old", NewName = "IX_New", Table = "Widgets" };
		Assert.That(() => Generate(operation), Throws.InstanceOf<NotSupportedException>());
	}

	[Test]
	public void CreateSequence_generates_a_CREATE_SEQUENCE_statement()
	{
		var operation = new CreateSequenceOperation { Name = "MySequence", StartValue = 1, IncrementBy = 10 };

		var sql = Generate(operation);

		Assert.That(sql, Does.Contain("CREATE SEQUENCE"));
		Assert.That(sql, Does.Contain("START WITH"));
		Assert.That(sql, Does.Contain("INCREMENT BY"));
	}

	[Test]
	public void AlterSequence_generates_an_ALTER_SEQUENCE_statement()
	{
		var operation = new AlterSequenceOperation { Name = "MySequence", IncrementBy = 20 };

		var sql = Generate(operation);

		Assert.That(sql, Does.Contain("ALTER SEQUENCE"));
		Assert.That(sql, Does.Contain("RESTART"));
	}

	[Test]
	public void RestartSequence_generates_an_ALTER_SEQUENCE_RESTART_statement()
	{
		var operation = new RestartSequenceOperation { Name = "MySequence", StartValue = 5 };

		var sql = Generate(operation);

		Assert.That(sql, Does.Contain("ALTER SEQUENCE"));
		Assert.That(sql, Does.Contain("RESTART"));
		Assert.That(sql, Does.Contain("WITH"));
	}

	[Test]
	public void RenameSequence_is_not_supported_by_Firebird()
	{
		var operation = new RenameSequenceOperation { Name = "OldSeq", NewName = "NewSeq" };
		Assert.That(() => Generate(operation), Throws.InstanceOf<NotSupportedException>());
	}

	[Test]
	public void DropSequence_generates_a_DROP_SEQUENCE_statement()
	{
		var sql = Generate(new DropSequenceOperation { Name = "MySequence" });
		Assert.That(sql, Does.Contain("DROP SEQUENCE"));
	}

	[Test]
	public void AddPrimaryKey_generates_an_ALTER_TABLE_ADD_CONSTRAINT_statement()
	{
		var operation = new AddPrimaryKeyOperation { Table = "Widgets", Name = "PK_Widgets", Columns = new[] { "Id" } };

		var sql = Generate(operation);

		Assert.That(sql, Does.Contain("ALTER TABLE"));
		Assert.That(sql, Does.Contain("PRIMARY KEY"));
	}

	[Test]
	public void DropPrimaryKey_generates_an_ALTER_TABLE_DROP_CONSTRAINT_statement()
	{
		var sql = Generate(new DropPrimaryKeyOperation { Table = "Widgets", Name = "PK_Widgets" });

		Assert.That(sql, Does.Contain("ALTER TABLE"));
		Assert.That(sql, Does.Contain("DROP CONSTRAINT"));
	}

	[Test]
	public void AddForeignKey_generates_a_FOREIGN_KEY_REFERENCES_statement()
	{
		var operation = new AddForeignKeyOperation
		{
			Table = "Widgets",
			Name = "FK_Widgets_Categories",
			Columns = new[] { "CategoryId" },
			PrincipalTable = "Categories",
			PrincipalColumns = new[] { "Id" }
		};

		var sql = Generate(operation);

		Assert.That(sql, Does.Contain("FOREIGN KEY"));
		Assert.That(sql, Does.Contain("REFERENCES"));
	}

	[Test]
	public void DropForeignKey_generates_an_ALTER_TABLE_DROP_CONSTRAINT_statement()
	{
		var sql = Generate(new DropForeignKeyOperation { Table = "Widgets", Name = "FK_Widgets_Categories" });

		Assert.That(sql, Does.Contain("DROP CONSTRAINT"));
	}

	[Test]
	public void AddUniqueConstraint_generates_an_ALTER_TABLE_ADD_CONSTRAINT_UNIQUE_statement()
	{
		var operation = new AddUniqueConstraintOperation { Table = "Widgets", Name = "AK_Widgets_Label", Columns = new[] { "Label" } };

		var sql = Generate(operation);

		Assert.That(sql, Does.Contain("UNIQUE"));
	}

	[Test]
	public void DropUniqueConstraint_generates_an_ALTER_TABLE_DROP_CONSTRAINT_statement()
	{
		var sql = Generate(new DropUniqueConstraintOperation { Table = "Widgets", Name = "AK_Widgets_Label" });

		Assert.That(sql, Does.Contain("DROP CONSTRAINT"));
	}

	[Test]
	public void SqlOperation_passes_the_raw_SQL_through()
	{
		var sql = Generate(new SqlOperation { Sql = "UPDATE Widgets SET Label = 'x'" });
		Assert.That(sql, Does.Contain("UPDATE Widgets SET Label = 'x'"));
	}

	[Test]
	public void EnsureSchema_is_not_supported_by_Firebird()
	{
		Assert.That(() => Generate(new EnsureSchemaOperation { Name = "dbo" }), Throws.InstanceOf<NotSupportedException>());
	}

	[Test]
	public void DropSchema_is_not_supported_by_Firebird()
	{
		Assert.That(() => Generate(new DropSchemaOperation { Name = "dbo" }), Throws.InstanceOf<NotSupportedException>());
	}

	[Test]
	public void InsertDataOperation_generates_an_INSERT_INTO_statement()
	{
		var operation = new InsertDataOperation
		{
			Table = "Widgets",
			Columns = new[] { "Id", "Label" },
			ColumnTypes = new[] { "INTEGER", "VARCHAR(100)" },
			Values = new object[,] { { 1, "Ada" } }
		};

		var sql = Generate(operation);

		Assert.That(sql, Does.Contain("INSERT INTO"));
	}

	[Test]
	public void DeleteDataOperation_generates_a_DELETE_FROM_statement()
	{
		var operation = new DeleteDataOperation
		{
			Table = "Widgets",
			KeyColumns = new[] { "Id" },
			KeyValues = new object[,] { { 1 } }
		};

		var sql = Generate(operation);

		Assert.That(sql, Does.Contain("DELETE FROM"));
	}

	[Test]
	public void UpdateDataOperation_generates_an_UPDATE_statement()
	{
		var operation = new UpdateDataOperation
		{
			Table = "Widgets",
			KeyColumns = new[] { "Id" },
			KeyValues = new object[,] { { 1 } },
			Columns = new[] { "Label" },
			Values = new object[,] { { "Grace" } }
		};

		var sql = Generate(operation);

		Assert.That(sql, Does.Contain("UPDATE"));
	}

	// The following four tests drive FbMigrationSqlGeneratorBehavior, which
	// FbMigrationsSqlGenerator delegates to whenever a CreateTableOperation/AlterColumnOperation
	// column carries the SequenceTrigger value generation strategy -- Firebird has no native
	// auto-increment, so this behavior emits an EXECUTE BLOCK that creates a backing sequence and
	// a BEFORE INSERT trigger that populates the column from it (or removes both on the way back).

	[Test]
	public void CreateTable_with_a_SequenceTrigger_column_emits_a_generator_and_trigger()
	{
		var column = new AddColumnOperation { Name = "Id", ClrType = typeof(int), ColumnType = "INTEGER", IsNullable = false, Table = "Widgets" };
		column[FbAnnotationName] = FbValueGenerationStrategy.SequenceTrigger;

		var operation = new CreateTableOperation { Name = "Widgets", Columns = { column } };

		var sql = Generate(operation, MigrationsSqlGenerationOptions.Default);

		Assert.That(sql, Does.Contain("EXECUTE BLOCK"));
		Assert.That(sql, Does.Contain("CREATE TRIGGER"));
		Assert.That(sql, Does.Contain("rdb$generators"));
	}

	[Test]
	public void CreateTable_with_a_SequenceTrigger_column_as_a_script_wraps_blocks_in_SET_TERM()
	{
		var column = new AddColumnOperation { Name = "Id", ClrType = typeof(int), ColumnType = "INTEGER", IsNullable = false, Table = "Widgets" };
		column[FbAnnotationName] = FbValueGenerationStrategy.SequenceTrigger;

		var operation = new CreateTableOperation { Name = "Widgets", Columns = { column } };

		var sql = Generate(operation, MigrationsSqlGenerationOptions.Script);

		Assert.That(sql, Does.Contain("SET TERM"));
	}

	[Test]
	public void AlterColumn_dropping_a_SequenceTrigger_strategy_drops_the_generator_trigger()
	{
		var oldColumn = new AddColumnOperation { ClrType = typeof(int), ColumnType = "INTEGER", IsNullable = false };
		oldColumn[FbAnnotationName] = FbValueGenerationStrategy.SequenceTrigger;

		var operation = new AlterColumnOperation
		{
			Table = "Widgets",
			Name = "Id",
			ClrType = typeof(int),
			ColumnType = "INTEGER",
			IsNullable = false,
			OldColumn = oldColumn
		};

		var sql = Generate(operation, MigrationsSqlGenerationOptions.Default);

		Assert.That(sql, Does.Contain("rdb$triggers"));
		Assert.That(sql, Does.Contain("drop trigger"));
	}

	[Test]
	public void AlterColumn_dropping_a_SequenceTrigger_strategy_as_a_script_wraps_blocks_in_SET_TERM()
	{
		var oldColumn = new AddColumnOperation { ClrType = typeof(int), ColumnType = "INTEGER", IsNullable = false };
		oldColumn[FbAnnotationName] = FbValueGenerationStrategy.SequenceTrigger;

		var operation = new AlterColumnOperation
		{
			Table = "Widgets",
			Name = "Id",
			ClrType = typeof(int),
			ColumnType = "INTEGER",
			IsNullable = false,
			OldColumn = oldColumn
		};

		var sql = Generate(operation, MigrationsSqlGenerationOptions.Script);

		Assert.That(sql, Does.Contain("SET TERM"));
	}

	const string FbAnnotationName = "Fb:ValueGenerationStrategy";

	static string Generate(MigrationOperation operation, MigrationsSqlGenerationOptions options)
	{
		var factory = new fakeDbFactory(SupportedDatabase.Firebird);
		var connection = factory.CreateConnection();
		var contextOptions = new DbContextOptionsBuilder<MigrationsDbContext>().UseFirebird(connection).Options;
		using var db = new MigrationsDbContext(contextOptions);
		var generator = db.GetService<IMigrationsSqlGenerator>();
		var commands = generator.Generate(new List<MigrationOperation> { operation }, db.Model, options);
		return string.Join(Environment.NewLine, commands.Select(c => c.CommandText));
	}

	static string Generate(MigrationOperation operation)
	{
		var factory = new fakeDbFactory(SupportedDatabase.Firebird);
		var connection = factory.CreateConnection();
		var options = new DbContextOptionsBuilder<MigrationsDbContext>().UseFirebird(connection).Options;
		using var db = new MigrationsDbContext(options);
		var generator = db.GetService<IMigrationsSqlGenerator>();
		// DeleteDataOperation/UpdateDataOperation resolve their WHERE-clause parameter type
		// mappings from a matching entity in the model (unlike InsertDataOperation, which is
		// satisfied by the operation's own ColumnTypes strings alone) -- see
		// FbUpdateSqlGenerator.GetColumnType, which needs IColumnModification.Property. The model
		// below maps a "Widgets" table with "Id"/"Label" columns matching every operation above
		// that targets that table and those column names.
		var commands = generator.Generate(new List<MigrationOperation> { operation }, db.Model);
		return string.Join(Environment.NewLine, commands.Select(c => c.CommandText));
	}

	sealed class MigrationsDbContext(DbContextOptions<MigrationsDbContext> options) : DbContext(options)
	{
		public DbSet<Widget> Widgets => Set<Widget>();

		protected override void OnModelCreating(ModelBuilder modelBuilder)
		{
			modelBuilder.Entity<Widget>(entity =>
			{
				entity.ToTable("Widgets");
				entity.Property(w => w.Id).ValueGeneratedNever();
			});
		}
	}

	sealed class Widget
	{
		public int Id { get; set; }
		public string Label { get; set; } = string.Empty;
	}
}
