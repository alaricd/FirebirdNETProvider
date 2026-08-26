using System;
using System.Collections.Generic;
using System.Data;
using System.Data.Common;
using FirebirdSql.Data.FirebirdClient;
using FirebirdSql.EntityFrameworkCore.Firebird.Storage.Internal;
using NUnit.Framework;
using pengdows.crud.enums;
using pengdows.crud.fakeDb;

namespace FirebirdSql.EntityFrameworkCore.Firebird.Tests.Storage;

[TestFixture]
public class FbTypeMappingSubstitutionTests
{
	[Test]
	public void String_mapping_accepts_a_substituted_parameter()
		=> Assert.DoesNotThrow(() => new ExposedStringMapping().Apply(new SubstituteParameter()));

	[Test]
	public void DateTime_mapping_accepts_a_substituted_parameter()
		=> Assert.DoesNotThrow(() => new ExposedDateTimeMapping().Apply(new SubstituteParameter()));

	[Test]
	public void TimeSpan_mapping_accepts_a_substituted_parameter()
		=> Assert.DoesNotThrow(() => new ExposedTimeSpanMapping().Apply(new SubstituteParameter()));

	[Test]
	public void Guid_mapping_accepts_a_substituted_parameter()
		=> Assert.DoesNotThrow(() => new ExposedGuidMapping().Apply(new SubstituteParameter()));

	[Test]
	public void Provider_mappings_configure_parameters_created_by_fakeDb()
	{
		using var connection = new fakeDbFactory(SupportedDatabase.Firebird).CreateConnection();
		using var command = connection.CreateCommand();
		var parameter = command.CreateParameter();

		new ExposedStringMapping().Apply(parameter);

		Assert.That(parameter.DbType, Is.EqualTo(DbType.AnsiString));
	}

	[Test]
	public void DateTime_mapping_configures_a_fakeDb_parameter()
	{
		using var connection = new fakeDbFactory(SupportedDatabase.Firebird).CreateConnection();
		using var command = connection.CreateCommand();
		var parameter = command.CreateParameter();

		new ExposedDateTimeMapping().Apply(parameter);

		Assert.That(parameter.DbType, Is.EqualTo(DbType.DateTime));
	}

	[Test]
	public void TimeSpan_mapping_configures_a_fakeDb_parameter()
	{
		using var connection = new fakeDbFactory(SupportedDatabase.Firebird).CreateConnection();
		using var command = connection.CreateCommand();
		var parameter = command.CreateParameter();

		new ExposedTimeSpanMapping().Apply(parameter);

		Assert.That(parameter.DbType, Is.EqualTo(DbType.Time));
	}

	[Test]
	public void Guid_mapping_configures_a_fakeDb_parameter()
	{
		using var connection = new fakeDbFactory(SupportedDatabase.Firebird).CreateConnection();
		using var command = connection.CreateCommand();
		var parameter = command.CreateParameter();

		new ExposedGuidMapping().Apply(parameter);

		Assert.That(parameter.DbType, Is.EqualTo(DbType.Guid));
	}

	[TestCaseSource(nameof(FakeDbParameterCases))]
	public void Provider_mappings_accept_a_range_of_values_on_fakeDb_parameters(
		string mappingName, object value, DbType expectedDbType)
	{
		using var connection = new fakeDbFactory(SupportedDatabase.Firebird).CreateConnection();
		using var command = connection.CreateCommand();
		var parameter = command.CreateParameter();
		parameter.ParameterName = "@value";
		parameter.Value = value;

		switch (mappingName)
		{
			case "String":
				new ExposedStringMapping().Apply(parameter);
				break;
			case "DateTime":
				new ExposedDateTimeMapping().Apply(parameter);
				break;
			case "TimeSpan":
				new ExposedTimeSpanMapping().Apply(parameter);
				break;
			case "Guid":
				new ExposedGuidMapping().Apply(parameter);
				break;
			default:
				Assert.Fail($"Unknown mapping: {mappingName}");
				return;
		}

		Assert.That(parameter.DbType, Is.EqualTo(expectedDbType));
		Assert.That(parameter.Value, Is.EqualTo(value));
	}

	public static IEnumerable<TestCaseData> FakeDbParameterCases()
	{
		var strings = new object[]
		{
			string.Empty,
			"Ada",
			"名前",
			"value with spaces",
			"value; DROP TABLE CUSTOMERS;--",
			"@value",
			"SELECT",
			new string('x', 4096),
			new string('x', 65536),
			DBNull.Value
		};
		var dateTimes = new object[] { DateTime.MinValue, DateTime.MaxValue, DBNull.Value };
		var timeSpans = new object[] { TimeSpan.Zero, TimeSpan.MinValue, TimeSpan.MaxValue, DBNull.Value };
		var guids = new object[] { Guid.Empty, Guid.Parse("01234567-89ab-cdef-0123-456789abcdef"), DBNull.Value };

		for (var index = 0; index < 25; index++)
		{
			yield return new TestCaseData("String", strings[index % strings.Length], DbType.AnsiString)
				.SetName($"String_value_{index:00}");
			yield return new TestCaseData("DateTime", index < dateTimes.Length ? dateTimes[index] : new DateTime(2020 + index % 7, 1 + index % 12, 1 + index % 27, index % 24, index % 60, index % 60), DbType.DateTime)
				.SetName($"DateTime_value_{index:00}");
			yield return new TestCaseData("TimeSpan", index < timeSpans.Length ? timeSpans[index] : TimeSpan.FromMinutes(index * 37 + 5), DbType.Time)
				.SetName($"TimeSpan_value_{index:00}");
			yield return new TestCaseData("Guid", index < guids.Length ? guids[index] : Guid.Parse($"00000000-0000-0000-0000-{index + 1:000000000000}"), DbType.Guid)
				.SetName($"Guid_value_{index:00}");
		}
	}

	private sealed class ExposedStringMapping : FbStringTypeMapping
	{
		public ExposedStringMapping() : base("VARCHAR", System.Data.DbType.String, FbDbType.VarChar) { }
		public void Apply(DbParameter parameter) => ConfigureParameter(parameter);
	}

	private sealed class ExposedDateTimeMapping : FbDateTimeTypeMapping
	{
		public ExposedDateTimeMapping() : base("TIMESTAMP", FbDbType.TimeStamp) { }
		public void Apply(DbParameter parameter) => ConfigureParameter(parameter);
	}

	private sealed class ExposedTimeSpanMapping : FbTimeSpanTypeMapping
	{
		public ExposedTimeSpanMapping() : base("TIME", FbDbType.Time) { }
		public void Apply(DbParameter parameter) => ConfigureParameter(parameter);
	}

	private sealed class ExposedGuidMapping : FbGuidTypeMapping
	{
		public void Apply(DbParameter parameter) => ConfigureParameter(parameter);
	}

	private sealed class SubstituteParameter : DbParameter
	{
		public override DbType DbType { get; set; }
		public override ParameterDirection Direction { get; set; }
		public override bool IsNullable { get; set; }
		public override string ParameterName { get; set; }
		public override int Size { get; set; }
		public override string SourceColumn { get; set; }
		public override bool SourceColumnNullMapping { get; set; }
		public override object Value { get; set; }
		public override DataRowVersion SourceVersion { get; set; }
		public override byte Precision { get; set; }
		public override byte Scale { get; set; }

		public override void ResetDbType() { }
	}
}
