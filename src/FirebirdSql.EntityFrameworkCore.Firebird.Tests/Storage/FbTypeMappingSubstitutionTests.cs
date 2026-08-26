using System;
using System.Data;
using System.Data.Common;
using FirebirdSql.Data.FirebirdClient;
using FirebirdSql.EntityFrameworkCore.Firebird.Storage.Internal;
using NUnit.Framework;

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
