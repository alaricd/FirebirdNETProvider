using System;
using System.Linq;
using Microsoft.EntityFrameworkCore;
using NUnit.Framework;
using pengdows.crud.enums;
using pengdows.crud.fakeDb;

namespace FirebirdSql.EntityFrameworkCore.Firebird.Tests.Query.Translators;

// Drives every LINQ member/method translator in Query/ExpressionTranslators/Internal purely
// through IQueryable.ToQueryString() -- SQL generation from a LINQ expression tree, with no
// command ever executed and no fake rows queued. This is the cheapest possible way to exercise
// this provider's client-side SQL-generation logic without a real Firebird server: the connection
// only needs to exist long enough for EF to pick a SQL dialect, it is never opened.
public class FbExpressionTranslatorFakeDbTests
{
	// ---- string translators ----

	[Test]
	public void Contains_translates_to_POSITION()
	{
		using var db = new WidgetContext(Options());
		var value = "abc";
		var sql = db.Widgets.Where(w => w.Name.Contains(value)).ToQueryString();
		Assert.That(sql, Does.Contain("POSITION"));
	}

	[Test]
	public void StartsWith_translates_to_LIKE_and_LEFT()
	{
		using var db = new WidgetContext(Options());
		var value = "abc";
		var sql = db.Widgets.Where(w => w.Name.StartsWith(value)).ToQueryString();
		Assert.That(sql, Does.Contain("LIKE"));
		Assert.That(sql, Does.Contain("LEFT"));
	}

	[Test]
	public void EndsWith_translates_to_RIGHT()
	{
		using var db = new WidgetContext(Options());
		var value = "abc";
		var sql = db.Widgets.Where(w => w.Name.EndsWith(value)).ToQueryString();
		Assert.That(sql, Does.Contain("RIGHT"));
	}

	[Test]
	public void IndexOf_translates_to_POSITION_minus_one()
	{
		using var db = new WidgetContext(Options());
		var value = "a";
		var sql = db.Widgets.Select(w => w.Name.IndexOf(value)).ToQueryString();
		Assert.That(sql, Does.Contain("POSITION"));
	}

	[Test]
	public void IndexOf_with_starting_position_translates_to_POSITION_minus_one()
	{
		using var db = new WidgetContext(Options());
		var value = "a";
		var start = 2;
		var sql = db.Widgets.Select(w => w.Name.IndexOf(value, start)).ToQueryString();
		Assert.That(sql, Does.Contain("POSITION"));
	}

	[Test]
	public void Replace_translates_to_REPLACE()
	{
		using var db = new WidgetContext(Options());
		var from = "a";
		var to = "b";
		var sql = db.Widgets.Select(w => w.Name.Replace(from, to)).ToQueryString();
		Assert.That(sql, Does.Contain("REPLACE"));
	}

	[Test]
	public void Substring_with_start_only_translates_to_SUBSTRING_FROM()
	{
		using var db = new WidgetContext(Options());
		var start = 1;
		var sql = db.Widgets.Select(w => w.Name.Substring(start)).ToQueryString();
		Assert.That(sql, Does.Contain("SUBSTRING"));
		Assert.That(sql, Does.Contain("FROM"));
	}

	[Test]
	public void Substring_with_start_and_length_translates_to_SUBSTRING_FROM_FOR()
	{
		using var db = new WidgetContext(Options());
		var start = 1;
		var length = 3;
		var sql = db.Widgets.Select(w => w.Name.Substring(start, length)).ToQueryString();
		Assert.That(sql, Does.Contain("SUBSTRING"));
		Assert.That(sql, Does.Contain("FOR"));
	}

	[Test]
	public void ToLower_translates_to_LOWER()
	{
		using var db = new WidgetContext(Options());
		var sql = db.Widgets.Select(w => w.Name.ToLower()).ToQueryString();
		Assert.That(sql, Does.Contain("LOWER"));
	}

	[Test]
	public void ToUpper_translates_to_UPPER()
	{
		using var db = new WidgetContext(Options());
		var sql = db.Widgets.Select(w => w.Name.ToUpper()).ToQueryString();
		Assert.That(sql, Does.Contain("UPPER"));
	}

	[Test]
	public void Trim_without_args_translates_to_TRIM_BOTH_FROM()
	{
		using var db = new WidgetContext(Options());
		var sql = db.Widgets.Select(w => w.Name.Trim()).ToQueryString();
		Assert.That(sql, Does.Contain("TRIM"));
		Assert.That(sql, Does.Contain("BOTH"));
	}

	[Test]
	public void TrimStart_without_args_translates_to_TRIM_LEADING_FROM()
	{
		using var db = new WidgetContext(Options());
		var sql = db.Widgets.Select(w => w.Name.TrimStart()).ToQueryString();
		Assert.That(sql, Does.Contain("TRIM"));
		Assert.That(sql, Does.Contain("LEADING"));
	}

	[Test]
	public void TrimEnd_without_args_translates_to_TRIM_TRAILING_FROM()
	{
		using var db = new WidgetContext(Options());
		var sql = db.Widgets.Select(w => w.Name.TrimEnd()).ToQueryString();
		Assert.That(sql, Does.Contain("TRIM"));
		Assert.That(sql, Does.Contain("TRAILING"));
	}

	[Test]
	public void Trim_with_char_arg_translates_to_TRIM_BOTH_FROM()
	{
		using var db = new WidgetContext(Options());
		var ch = 'x';
		var sql = db.Widgets.Select(w => w.Name.Trim(ch)).ToQueryString();
		Assert.That(sql, Does.Contain("TRIM"));
		Assert.That(sql, Does.Contain("BOTH"));
	}

	[Test]
	public void Length_translates_to_CHAR_LENGTH()
	{
		using var db = new WidgetContext(Options());
		var sql = db.Widgets.Select(w => w.Name.Length).ToQueryString();
		Assert.That(sql, Does.Contain("CHAR_LENGTH"));
	}

	[Test]
	public void IsNullOrWhiteSpace_translates_to_a_trimmed_length_check()
	{
		using var db = new WidgetContext(Options());
		var sql = db.Widgets.Where(w => string.IsNullOrWhiteSpace(w.Name)).ToQueryString();
		Assert.That(sql, Does.Contain("TRIM"));
		Assert.That(sql, Does.Contain("CHAR_LENGTH"));
	}

	[Test]
	public void Enumerable_FirstOrDefault_over_chars_translates_to_LEFT()
	{
		using var db = new WidgetContext(Options());
		var sql = db.Widgets.Select(w => w.Name.FirstOrDefault()).ToQueryString();
		Assert.That(sql, Does.Contain("LEFT"));
	}

	[Test]
	public void Enumerable_LastOrDefault_over_chars_translates_to_RIGHT()
	{
		using var db = new WidgetContext(Options());
		var sql = db.Widgets.Select(w => w.Name.LastOrDefault()).ToQueryString();
		Assert.That(sql, Does.Contain("RIGHT"));
	}

	// ---- math translators ----

	[Test]
	public void Math_Abs_translates_to_ABS()
	{
		using var db = new WidgetContext(Options());
		var sql = db.Widgets.Select(w => Math.Abs(w.LongValue)).ToQueryString();
		Assert.That(sql, Does.Contain("ABS"));
	}

	[Test]
	public void Math_Ceiling_translates_to_CEILING()
	{
		using var db = new WidgetContext(Options());
		var sql = db.Widgets.Select(w => Math.Ceiling(w.DoubleValue)).ToQueryString();
		Assert.That(sql, Does.Contain("CEILING"));
	}

	[Test]
	public void Math_Floor_translates_to_FLOOR()
	{
		using var db = new WidgetContext(Options());
		var sql = db.Widgets.Select(w => Math.Floor(w.DoubleValue)).ToQueryString();
		Assert.That(sql, Does.Contain("FLOOR"));
	}

	[Test]
	public void Math_Pow_translates_to_POWER()
	{
		using var db = new WidgetContext(Options());
		var exponent = 2.0;
		var sql = db.Widgets.Select(w => Math.Pow(w.DoubleValue, exponent)).ToQueryString();
		Assert.That(sql, Does.Contain("POWER"));
	}

	[Test]
	public void Math_Exp_translates_to_EXP()
	{
		using var db = new WidgetContext(Options());
		var sql = db.Widgets.Select(w => Math.Exp(w.DoubleValue)).ToQueryString();
		Assert.That(sql, Does.Contain("EXP"));
	}

	[Test]
	public void Math_Log10_translates_to_LOG10()
	{
		using var db = new WidgetContext(Options());
		var sql = db.Widgets.Select(w => Math.Log10(w.DoubleValue)).ToQueryString();
		Assert.That(sql, Does.Contain("LOG10"));
	}

	[Test]
	public void Math_Log_translates_to_LN()
	{
		using var db = new WidgetContext(Options());
		var sql = db.Widgets.Select(w => Math.Log(w.DoubleValue)).ToQueryString();
		Assert.That(sql, Does.Contain("LN"));
	}

	[Test]
	public void Math_Log_with_new_base_translates_to_LOG()
	{
		using var db = new WidgetContext(Options());
		var newBase = 2.0;
		var sql = db.Widgets.Select(w => Math.Log(w.DoubleValue, newBase)).ToQueryString();
		Assert.That(sql, Does.Contain("LOG"));
	}

	[Test]
	public void Math_Sqrt_translates_to_SQRT()
	{
		using var db = new WidgetContext(Options());
		var sql = db.Widgets.Select(w => Math.Sqrt(w.DoubleValue)).ToQueryString();
		Assert.That(sql, Does.Contain("SQRT"));
	}

	[Test]
	public void Math_Acos_translates_to_ACOS()
	{
		using var db = new WidgetContext(Options());
		var sql = db.Widgets.Select(w => Math.Acos(w.DoubleValue)).ToQueryString();
		Assert.That(sql, Does.Contain("ACOS"));
	}

	[Test]
	public void Math_Asin_translates_to_ASIN()
	{
		using var db = new WidgetContext(Options());
		var sql = db.Widgets.Select(w => Math.Asin(w.DoubleValue)).ToQueryString();
		Assert.That(sql, Does.Contain("ASIN"));
	}

	[Test]
	public void Math_Atan_translates_to_ATAN()
	{
		using var db = new WidgetContext(Options());
		var sql = db.Widgets.Select(w => Math.Atan(w.DoubleValue)).ToQueryString();
		Assert.That(sql, Does.Contain("ATAN"));
	}

	[Test]
	public void Math_Atan2_translates_to_ATAN2()
	{
		using var db = new WidgetContext(Options());
		var other = 1.0;
		var sql = db.Widgets.Select(w => Math.Atan2(w.DoubleValue, other)).ToQueryString();
		Assert.That(sql, Does.Contain("ATAN2"));
	}

	[Test]
	public void Math_Cos_translates_to_COS()
	{
		using var db = new WidgetContext(Options());
		var sql = db.Widgets.Select(w => Math.Cos(w.DoubleValue)).ToQueryString();
		Assert.That(sql, Does.Contain("COS"));
	}

	[Test]
	public void Math_Sin_translates_to_SIN()
	{
		using var db = new WidgetContext(Options());
		var sql = db.Widgets.Select(w => Math.Sin(w.DoubleValue)).ToQueryString();
		Assert.That(sql, Does.Contain("SIN"));
	}

	[Test]
	public void Math_Tan_translates_to_TAN()
	{
		using var db = new WidgetContext(Options());
		var sql = db.Widgets.Select(w => Math.Tan(w.DoubleValue)).ToQueryString();
		Assert.That(sql, Does.Contain("TAN"));
	}

	[Test]
	public void Math_Sign_translates_to_SIGN()
	{
		using var db = new WidgetContext(Options());
		var sql = db.Widgets.Select(w => Math.Sign(w.LongValue)).ToQueryString();
		Assert.That(sql, Does.Contain("SIGN"));
	}

	[Test]
	public void Math_Truncate_translates_to_TRUNC()
	{
		using var db = new WidgetContext(Options());
		var sql = db.Widgets.Select(w => Math.Truncate(w.DoubleValue)).ToQueryString();
		Assert.That(sql, Does.Contain("TRUNC"));
	}

	[Test]
	public void Math_Round_without_digits_translates_to_ROUND()
	{
		using var db = new WidgetContext(Options());
		var sql = db.Widgets.Select(w => Math.Round(w.DoubleValue)).ToQueryString();
		Assert.That(sql, Does.Contain("ROUND"));
	}

	[Test]
	public void Math_Round_with_digits_translates_to_ROUND()
	{
		using var db = new WidgetContext(Options());
		var digits = 2;
		var sql = db.Widgets.Select(w => Math.Round(w.DoubleValue, digits)).ToQueryString();
		Assert.That(sql, Does.Contain("ROUND"));
	}

	// ---- Convert translators ----

	[Test]
	public void Convert_ToInt32_translates_to_a_CAST()
	{
		using var db = new WidgetContext(Options());
		var sql = db.Widgets.Select(w => Convert.ToInt32(w.DoubleValue)).ToQueryString();
		Assert.That(sql, Does.Contain("CAST"));
	}

	[Test]
	public void Convert_ToString_translates_to_a_CAST()
	{
		using var db = new WidgetContext(Options());
		var sql = db.Widgets.Select(w => Convert.ToString(w.LongValue)).ToQueryString();
		Assert.That(sql, Does.Contain("CAST"));
	}

	[Test]
	public void Convert_ToBoolean_translates_to_a_CAST()
	{
		using var db = new WidgetContext(Options());
		var sql = db.Widgets.Select(w => Convert.ToBoolean(w.ByteValue)).ToQueryString();
		Assert.That(sql, Does.Contain("CAST"));
	}

	[Test]
	public void Convert_ToDecimal_translates_to_a_CAST()
	{
		using var db = new WidgetContext(Options());
		var sql = db.Widgets.Select(w => Convert.ToDecimal(w.DoubleValue)).ToQueryString();
		Assert.That(sql, Does.Contain("CAST"));
	}

	[Test]
	public void Convert_ToInt64_translates_to_a_CAST()
	{
		using var db = new WidgetContext(Options());
		var sql = db.Widgets.Select(w => Convert.ToInt64(w.ShortValue)).ToQueryString();
		Assert.That(sql, Does.Contain("CAST"));
	}

	// ---- DateTime translators ----

	[Test]
	public void DateTime_AddYears_translates_to_DATEADD_YEAR()
	{
		using var db = new WidgetContext(Options());
		var n = 1;
		var sql = db.Widgets.Select(w => w.CreatedAt.AddYears(n)).ToQueryString();
		Assert.That(sql, Does.Contain("DATEADD"));
		Assert.That(sql, Does.Contain("YEAR"));
	}

	[Test]
	public void DateTime_AddDays_translates_to_DATEADD_DAY()
	{
		using var db = new WidgetContext(Options());
		var n = 1.0;
		var sql = db.Widgets.Select(w => w.CreatedAt.AddDays(n)).ToQueryString();
		Assert.That(sql, Does.Contain("DATEADD"));
		Assert.That(sql, Does.Contain("DAY"));
	}

	[Test]
	public void DateTime_AddHours_translates_to_DATEADD_HOUR()
	{
		using var db = new WidgetContext(Options());
		var n = 1.0;
		var sql = db.Widgets.Select(w => w.CreatedAt.AddHours(n)).ToQueryString();
		Assert.That(sql, Does.Contain("DATEADD"));
		Assert.That(sql, Does.Contain("HOUR"));
	}

	[Test]
	public void DateTime_AddMinutes_translates_to_DATEADD_MINUTE()
	{
		using var db = new WidgetContext(Options());
		var n = 1.0;
		var sql = db.Widgets.Select(w => w.CreatedAt.AddMinutes(n)).ToQueryString();
		Assert.That(sql, Does.Contain("DATEADD"));
		Assert.That(sql, Does.Contain("MINUTE"));
	}

	[Test]
	public void DateTime_AddSeconds_translates_to_DATEADD_SECOND()
	{
		using var db = new WidgetContext(Options());
		var n = 1.0;
		var sql = db.Widgets.Select(w => w.CreatedAt.AddSeconds(n)).ToQueryString();
		Assert.That(sql, Does.Contain("DATEADD"));
		Assert.That(sql, Does.Contain("SECOND"));
	}

	[Test]
	public void DateTime_AddMilliseconds_translates_to_DATEADD_MILLISECOND()
	{
		using var db = new WidgetContext(Options());
		var n = 1.0;
		var sql = db.Widgets.Select(w => w.CreatedAt.AddMilliseconds(n)).ToQueryString();
		Assert.That(sql, Does.Contain("DATEADD"));
		Assert.That(sql, Does.Contain("MILLISECOND"));
	}

	[Test]
	public void DateTime_Year_translates_to_EXTRACT_YEAR()
	{
		using var db = new WidgetContext(Options());
		var sql = db.Widgets.Select(w => w.CreatedAt.Year).ToQueryString();
		Assert.That(sql, Does.Contain("EXTRACT"));
		Assert.That(sql, Does.Contain("YEAR"));
	}

	[Test]
	public void DateTime_Month_translates_to_EXTRACT_MONTH()
	{
		using var db = new WidgetContext(Options());
		var sql = db.Widgets.Select(w => w.CreatedAt.Month).ToQueryString();
		Assert.That(sql, Does.Contain("EXTRACT"));
		Assert.That(sql, Does.Contain("MONTH"));
	}

	[Test]
	public void DateTime_Day_translates_to_EXTRACT_DAY()
	{
		using var db = new WidgetContext(Options());
		var sql = db.Widgets.Select(w => w.CreatedAt.Day).ToQueryString();
		Assert.That(sql, Does.Contain("EXTRACT"));
		Assert.That(sql, Does.Contain("DAY"));
	}

	[Test]
	public void DateTime_Hour_translates_to_EXTRACT_HOUR()
	{
		using var db = new WidgetContext(Options());
		var sql = db.Widgets.Select(w => w.CreatedAt.Hour).ToQueryString();
		Assert.That(sql, Does.Contain("EXTRACT"));
		Assert.That(sql, Does.Contain("HOUR"));
	}

	[Test]
	public void DateTime_Minute_translates_to_EXTRACT_MINUTE()
	{
		using var db = new WidgetContext(Options());
		var sql = db.Widgets.Select(w => w.CreatedAt.Minute).ToQueryString();
		Assert.That(sql, Does.Contain("EXTRACT"));
		Assert.That(sql, Does.Contain("MINUTE"));
	}

	[Test]
	public void DateTime_Second_translates_to_EXTRACT_SECOND_and_TRUNC()
	{
		using var db = new WidgetContext(Options());
		var sql = db.Widgets.Select(w => w.CreatedAt.Second).ToQueryString();
		Assert.That(sql, Does.Contain("EXTRACT"));
		Assert.That(sql, Does.Contain("SECOND"));
		Assert.That(sql, Does.Contain("TRUNC"));
	}

	[Test]
	public void DateTime_Millisecond_translates_to_EXTRACT_MILLISECOND_and_TRUNC()
	{
		using var db = new WidgetContext(Options());
		var sql = db.Widgets.Select(w => w.CreatedAt.Millisecond).ToQueryString();
		Assert.That(sql, Does.Contain("EXTRACT"));
		Assert.That(sql, Does.Contain("MILLISECOND"));
		Assert.That(sql, Does.Contain("TRUNC"));
	}

	[Test]
	public void DateTime_DayOfYear_translates_to_EXTRACT_YEARDAY_plus_one()
	{
		using var db = new WidgetContext(Options());
		var sql = db.Widgets.Select(w => w.CreatedAt.DayOfYear).ToQueryString();
		Assert.That(sql, Does.Contain("EXTRACT"));
		Assert.That(sql, Does.Contain("YEARDAY"));
	}

	[Test]
	public void DateTime_DayOfWeek_translates_to_EXTRACT_WEEKDAY()
	{
		using var db = new WidgetContext(Options());
		var sql = db.Widgets.Select(w => w.CreatedAt.DayOfWeek).ToQueryString();
		Assert.That(sql, Does.Contain("EXTRACT"));
		Assert.That(sql, Does.Contain("WEEKDAY"));
	}

	[Test]
	public void DateTime_Date_translates_to_a_CAST_AS_DATE()
	{
		using var db = new WidgetContext(Options());
		var sql = db.Widgets.Select(w => w.CreatedAt.Date).ToQueryString();
		Assert.That(sql, Does.Contain("CAST"));
		Assert.That(sql, Does.Contain("DATE"));
	}

	[Test]
	public void DateTime_Now_translates_to_LOCALTIMESTAMP()
	{
		using var db = new WidgetContext(Options());
		var sql = db.Widgets.Where(w => w.CreatedAt < DateTime.Now).ToQueryString();
		Assert.That(sql, Does.Contain("LOCALTIMESTAMP"));
	}

	[Test]
	public void DateTime_Today_translates_to_CURRENT_DATE()
	{
		using var db = new WidgetContext(Options());
		var sql = db.Widgets.Where(w => w.CreatedAt < DateTime.Today).ToQueryString();
		Assert.That(sql, Does.Contain("CURRENT_DATE"));
	}

	// ---- Guid ----

	[Test]
	public void Guid_NewGuid_translates_to_GEN_UUID()
	{
		using var db = new WidgetContext(Options());
		var sql = db.Widgets.Select(w => Guid.NewGuid()).ToQueryString();
		Assert.That(sql, Does.Contain("GEN_UUID"));
	}

	// ---- object/scalar ToString ----

	[Test]
	public void Int_ToString_translates_to_a_CAST()
	{
		using var db = new WidgetContext(Options());
		var sql = db.Widgets.Select(w => w.LongValue.ToString()).ToQueryString();
		Assert.That(sql, Does.Contain("CAST"));
	}

	[Test]
	public void Guid_ToString_translates_to_UUID_TO_CHAR()
	{
		using var db = new WidgetContext(Options());
		var sql = db.Widgets.Select(w => w.UniqueId.ToString()).ToQueryString();
		Assert.That(sql, Does.Contain("UUID_TO_CHAR"));
	}

	[Test]
	public void Bool_ToString_translates_to_a_CASE_expression()
	{
		using var db = new WidgetContext(Options());
		var sql = db.Widgets.Select(w => w.BoolValue.ToString()).ToQueryString();
		Assert.That(sql, Does.Contain("CASE"));
	}

	// ---- byte[] ----

	[Test]
	public void ByteArray_Contains_translates_to_ASCII_CHAR_and_POSITION()
	{
		using var db = new WidgetContext(Options());
		byte target = 1;
		var sql = db.Widgets.Where(w => w.Data.Contains(target)).ToQueryString();
		Assert.That(sql, Does.Contain("ASCII_CHAR"));
		Assert.That(sql, Does.Contain("POSITION"));
	}

	// ---- DateOnly / TimeOnly / TimeSpan ----

	[Test]
	public void DateOnly_Year_translates_to_EXTRACT_YEAR()
	{
		using var db = new WidgetContext(Options());
		var sql = db.Widgets.Select(w => w.BirthDate.Year).ToQueryString();
		Assert.That(sql, Does.Contain("EXTRACT"));
		Assert.That(sql, Does.Contain("YEAR"));
	}

	[Test]
	public void DateOnly_Month_translates_to_EXTRACT_MONTH()
	{
		using var db = new WidgetContext(Options());
		var sql = db.Widgets.Select(w => w.BirthDate.Month).ToQueryString();
		Assert.That(sql, Does.Contain("EXTRACT"));
		Assert.That(sql, Does.Contain("MONTH"));
	}

	[Test]
	public void DateOnly_DayOfYear_translates_to_EXTRACT_YEARDAY()
	{
		using var db = new WidgetContext(Options());
		var sql = db.Widgets.Select(w => w.BirthDate.DayOfYear).ToQueryString();
		Assert.That(sql, Does.Contain("EXTRACT"));
		Assert.That(sql, Does.Contain("YEARDAY"));
	}

	[Test]
	public void DateOnly_DayOfWeek_translates_to_EXTRACT_WEEKDAY()
	{
		using var db = new WidgetContext(Options());
		var sql = db.Widgets.Select(w => w.BirthDate.DayOfWeek).ToQueryString();
		Assert.That(sql, Does.Contain("EXTRACT"));
		Assert.That(sql, Does.Contain("WEEKDAY"));
	}

	[Test]
	public void DateOnly_FromDateTime_translates_to_a_conversion()
	{
		using var db = new WidgetContext(Options());
		var sql = db.Widgets.Select(w => DateOnly.FromDateTime(w.CreatedAt)).ToQueryString();
		Assert.That(sql, Does.Not.Empty);
	}

	[Test]
	public void TimeOnly_FromDateTime_translates_to_a_conversion()
	{
		using var db = new WidgetContext(Options());
		var sql = db.Widgets.Select(w => TimeOnly.FromDateTime(w.CreatedAt)).ToQueryString();
		Assert.That(sql, Does.Not.Empty);
	}

	[Test]
	public void TimeOnly_Hour_translates_to_EXTRACT_HOUR()
	{
		using var db = new WidgetContext(Options());
		var sql = db.Widgets.Select(w => w.StartTime.Hour).ToQueryString();
		Assert.That(sql, Does.Contain("EXTRACT"));
		Assert.That(sql, Does.Contain("HOUR"));
	}

	[Test]
	public void TimeOnly_Second_translates_to_EXTRACT_SECOND_and_TRUNC()
	{
		using var db = new WidgetContext(Options());
		var sql = db.Widgets.Select(w => w.StartTime.Second).ToQueryString();
		Assert.That(sql, Does.Contain("EXTRACT"));
		Assert.That(sql, Does.Contain("TRUNC"));
	}

	[Test]
	public void TimeSpan_Hours_translates_to_EXTRACT_HOUR()
	{
		using var db = new WidgetContext(Options());
		var sql = db.Widgets.Select(w => w.Duration.Hours).ToQueryString();
		Assert.That(sql, Does.Contain("EXTRACT"));
		Assert.That(sql, Does.Contain("HOUR"));
	}

	[Test]
	public void TimeSpan_Seconds_translates_to_EXTRACT_SECOND_and_TRUNC()
	{
		using var db = new WidgetContext(Options());
		var sql = db.Widgets.Select(w => w.Duration.Seconds).ToQueryString();
		Assert.That(sql, Does.Contain("EXTRACT"));
		Assert.That(sql, Does.Contain("TRUNC"));
	}

	// ---- byte[].Length (FbSqlTranslatingExpressionVisitor.VisitUnary) ----

	[Test]
	public void ByteArray_Length_translates_to_OCTET_LENGTH()
	{
		using var db = new WidgetContext(Options());
		var sql = db.Widgets.Select(w => w.Data.Length).ToQueryString();
		Assert.That(sql, Does.Contain("OCTET_LENGTH"));
	}

	// ---- more FbObjectToStringTranslator SupportedTypes, and its nullable-bool branch ----

	[Test]
	public void Byte_ToString_translates_to_a_CAST()
	{
		using var db = new WidgetContext(Options());
		var sql = db.Widgets.Select(w => w.ByteValue.ToString()).ToQueryString();
		Assert.That(sql, Does.Contain("CAST"));
	}

	[Test]
	public void Double_ToString_translates_to_a_CAST()
	{
		using var db = new WidgetContext(Options());
		var sql = db.Widgets.Select(w => w.DoubleValue.ToString()).ToQueryString();
		Assert.That(sql, Does.Contain("CAST"));
	}

	[Test]
	public void Short_ToString_translates_to_a_CAST()
	{
		using var db = new WidgetContext(Options());
		var sql = db.Widgets.Select(w => w.ShortValue.ToString()).ToQueryString();
		Assert.That(sql, Does.Contain("CAST"));
	}

	[Test]
	public void Decimal_ToString_translates_to_a_CAST()
	{
		using var db = new WidgetContext(Options());
		var sql = db.Widgets.Select(w => w.DecimalValue.ToString()).ToQueryString();
		Assert.That(sql, Does.Contain("CAST"));
	}

	[Test]
	public void DateTime_ToString_translates_to_a_CAST()
	{
		using var db = new WidgetContext(Options());
		var sql = db.Widgets.Select(w => w.CreatedAt.ToString()).ToQueryString();
		Assert.That(sql, Does.Contain("CAST"));
	}

	[Test]
	public void TimeSpan_ToString_translates_to_a_CAST()
	{
		using var db = new WidgetContext(Options());
		var sql = db.Widgets.Select(w => w.Duration.ToString()).ToQueryString();
		Assert.That(sql, Does.Contain("CAST"));
	}

	[Test]
	public void DateOnly_ToString_translates_to_a_CAST()
	{
		using var db = new WidgetContext(Options());
		var sql = db.Widgets.Select(w => w.BirthDate.ToString()).ToQueryString();
		Assert.That(sql, Does.Contain("CAST"));
	}

	[Test]
	public void TimeOnly_ToString_translates_to_a_CAST()
	{
		using var db = new WidgetContext(Options());
		var sql = db.Widgets.Select(w => w.StartTime.ToString()).ToQueryString();
		Assert.That(sql, Does.Contain("CAST"));
	}

	[Test]
	public void Nullable_bool_ToString_translates_to_a_two_branch_CASE_expression()
	{
		using var db = new WidgetContext(Options());
		var sql = db.Widgets.Select(w => w.NullableFlag.ToString()).ToQueryString();
		Assert.That(sql, Does.Contain("CASE"));
	}

	// ---- literal (non-parameterized) type-mapping generation, forced by an inline `new`
	// expression instead of a captured local variable ----

	[Test]
	public void DateTime_literal_comparison_generates_a_CAST_AS_TIMESTAMP_literal()
	{
		using var db = new WidgetContext(Options());
		var sql = db.Widgets.Where(w => w.CreatedAt == new DateTime(2020, 1, 1, 10, 30, 0)).ToQueryString();
		Assert.That(sql, Does.Contain("CAST"));
		Assert.That(sql, Does.Contain("TIMESTAMP"));
	}

	// Regression test for a real bug this test originally discovered: FbTimeSpanTypeMapping.
	// GenerateNonNullSqlLiteral formats the literal with the custom TimeSpan format string
	// "hh\:mm\:ss.ffff" -- the '.' before the fractional-seconds section was NOT escaped. Unlike
	// DateTime custom format strings (where '.' is always a literal), TimeSpan's custom formatter
	// treats an unescaped '.' as part of the format syntax and throws FormatException if what
	// follows isn't a recognized token in that position -- reproduced independently of EF Core/
	// fakeDb with a bare `new TimeSpan(1, 2, 3).ToString(@"hh\:mm\:ss.ffff")` call, which threw the
	// same exception. Fixed by escaping the dot ("hh\\:mm\\:ss\\.ffff"); this test now locks in the
	// correct literal instead.
	[Test]
	public void TimeSpan_literal_comparison_generates_a_CAST_AS_TIME_literal()
	{
		using var db = new WidgetContext(Options());
		var sql = db.Widgets.Where(w => w.Duration == new TimeSpan(1, 2, 3)).ToQueryString();
		Assert.That(sql, Does.Contain("CAST"));
		Assert.That(sql, Does.Contain("01:02:03.0000"));
		Assert.That(sql, Does.Contain("AS TIME)"));
	}

	[Test]
	public void Guid_literal_comparison_generates_a_CHAR_TO_UUID_literal()
	{
		using var db = new WidgetContext(Options());
		var sql = db.Widgets.Where(w => w.UniqueId == new Guid("11111111-2222-3333-4444-555555555555")).ToQueryString();
		Assert.That(sql, Does.Contain("CHAR_TO_UUID('11111111-2222-3333-4444-555555555555')"));
	}

	[Test]
	public void ByteArray_literal_comparison_generates_a_hex_literal()
	{
		using var db = new WidgetContext(Options());
		var sql = db.Widgets.Where(w => w.Data == new byte[] { 0xAB, 0xCD }).ToQueryString();
		Assert.That(sql, Does.Contain("x'ABCD'"));
	}

	[Test]
	public void DateOnly_literal_comparison_generates_a_CAST_AS_DATE_literal()
	{
		using var db = new WidgetContext(Options());
		var sql = db.Widgets.Where(w => w.BirthDate == new DateOnly(2020, 1, 1)).ToQueryString();
		Assert.That(sql, Does.Contain("CAST('2020-01-01' AS DATE)"));
	}

	[Test]
	public void TimeOnly_literal_comparison_generates_a_CAST_AS_TIME_literal()
	{
		using var db = new WidgetContext(Options());
		var sql = db.Widgets.Where(w => w.StartTime == new TimeOnly(1, 2, 3)).ToQueryString();
		Assert.That(sql, Does.Contain("CAST('01:02:03.0000' AS TIME)"));
	}

	// FbDbType.Date is only ever constructed by FbTypeMappingSource for a DateTime-typed property
	// explicitly mapped with .HasColumnType("DATE") -- the default DateTime mapping is TIMESTAMP
	// (already covered above). A separate small context/entity is used here rather than adding a
	// second DATE-mapped DateTime property onto Widget, to keep that shared entity's default
	// mapping unambiguous for the other ~90 tests in this file.
	[Test]
	public void DateTime_mapped_to_DATE_column_type_literal_comparison_generates_a_CAST_AS_DATE_literal()
	{
		var factory = new fakeDbFactory(SupportedDatabase.Firebird);
		using var db = new DateOnlyColumnContext(new DbContextOptionsBuilder<DateOnlyColumnContext>().UseFirebird(factory.CreateConnection()).Options);
		var sql = db.Events.Where(e => e.OccurredOn == new DateTime(2020, 1, 1)).ToQueryString();
		Assert.That(sql, Does.Contain("CAST('2020-01-01' AS DATE)"));
	}

	// ---- FbSqlTranslatingExpressionVisitor overrides (GenerateGreatest/GenerateLeast) ----

	[Test]
	public void Math_Max_between_two_columns_translates_to_MAXVALUE()
	{
		using var db = new WidgetContext(Options());
		var sql = db.Widgets.Where(w => Math.Max(w.LongValue, w.Id) > 0).ToQueryString();
		Assert.That(sql, Does.Contain("MAXVALUE"));
	}

	[Test]
	public void Math_Min_between_two_columns_translates_to_MINVALUE()
	{
		using var db = new WidgetContext(Options());
		var sql = db.Widgets.Where(w => Math.Min(w.LongValue, w.Id) > 0).ToQueryString();
		Assert.That(sql, Does.Contain("MINVALUE"));
	}

	static DbContextOptions<WidgetContext> Options()
	{
		var factory = new fakeDbFactory(SupportedDatabase.Firebird);
		var connection = factory.CreateConnection();
		return new DbContextOptionsBuilder<WidgetContext>().UseFirebird(connection).Options;
	}

	public sealed class WidgetContext(DbContextOptions<WidgetContext> options) : DbContext(options)
	{
		public DbSet<Widget> Widgets => Set<Widget>();

		protected override void OnModelCreating(ModelBuilder modelBuilder)
		{
			modelBuilder.Entity<Widget>().Property(w => w.Id).ValueGeneratedNever();
		}
	}

	public sealed class Widget
	{
		public int Id { get; set; }
		public string Name { get; set; } = string.Empty;
		public long LongValue { get; set; }
		public decimal DecimalValue { get; set; }
		public double DoubleValue { get; set; }
		public float FloatValue { get; set; }
		public short ShortValue { get; set; }
		public byte ByteValue { get; set; }
		public bool BoolValue { get; set; }
		public bool? NullableFlag { get; set; }
		public DateTime CreatedAt { get; set; }
		public DateOnly BirthDate { get; set; }
		public TimeOnly StartTime { get; set; }
		public TimeSpan Duration { get; set; }
		public Guid UniqueId { get; set; }
		public byte[] Data { get; set; } = Array.Empty<byte>();
	}

	public sealed class DateOnlyColumnContext(DbContextOptions<DateOnlyColumnContext> options) : DbContext(options)
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

	public sealed class Event
	{
		public int Id { get; set; }
		public DateTime OccurredOn { get; set; }
	}
}
